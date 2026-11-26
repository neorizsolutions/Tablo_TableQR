using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.Entities;
using Npgsql;

namespace Tablo.Api.Services;

public class ManagerAuthService
{
    private static readonly SemaphoreSlim _inMemoryLock = new(1, 1);
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ManagerAuthService> _logger;

    public ManagerAuthService(ApplicationDbContext db, ILogger<ManagerAuthService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Validates manager authorization for a sensitive action.
    /// Closes the bypass: an employee ID alone NEVER authorizes an action.
    /// If caller is already an authenticated Admin/Manager, direct approval is permitted.
    /// Otherwise, securely verified manager PIN is strictly required.
    /// </summary>
    public async Task<(bool Success, Employee? Approver, string? ErrorMessage)> ValidateManagerApprovalAsync(
        Guid restaurantId,
        Guid callerEmployeeId,
        string? managerPin,
        Guid? specificManagerId = null)
    {
        // 1. Validate caller identity
        var caller = await _db.Employees
            .Include(e => e.Role)
            .FirstOrDefaultAsync(e => e.Id == callerEmployeeId && e.RestaurantId == restaurantId && !e.IsDeleted && e.IsActive);

        if (caller == null)
        {
            return (false, null, "Authenticated caller account not found, inactive, or belongs to another restaurant.");
        }

        // 2. Direct manager approval: If caller is already an Admin/Manager, they are authorized directly.
        if (IsManagerOrAdmin(caller.Role.Name))
        {
            return (true, caller, null);
        }

        // 3. For non-manager callers (e.g. Waiter, Kitchen), a manager PIN is strictly required.
        if (string.IsNullOrWhiteSpace(managerPin))
        {
            return (false, null, "Manager PIN is strictly required to authorize actions requiring manager approval.");
        }

        // 4. Verify PIN with atomic rate limiting that survives rollbacks
        return await VerifyAndRecordManagerPinAsync(restaurantId, managerPin, specificManagerId);
    }

    /// <summary>
    /// Verifies manager PIN with tenant and manager level rate limiting.
    /// Failed attempts and lockouts are persisted atomically and independently of order transactions.
    /// Never exposes sensitive account details in response messages.
    /// </summary>
    public async Task<(bool Success, Employee? Approver, string? ErrorMessage)> VerifyAndRecordManagerPinAsync(
        Guid restaurantId,
        string pin,
        Guid? specificManagerId = null)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            return (false, null, "Manager PIN cannot be empty.");
        }

        // 1. Check tenant-level rate limit / lockout
        var tenantRateLimit = await _db.ManagerPinRateLimits.AsNoTracking().FirstOrDefaultAsync(r => r.RestaurantId == restaurantId);
        if (tenantRateLimit != null && tenantRateLimit.LockedUntilUtc.HasValue && tenantRateLimit.LockedUntilUtc.Value > DateTime.UtcNow)
        {
            var waitMin = Math.Max(1, Math.Ceiling((tenantRateLimit.LockedUntilUtc.Value - DateTime.UtcNow).TotalMinutes));
            return (false, null, $"Manager PIN verification is locked due to multiple failed attempts. Please wait {waitMin} minute(s).");
        }

        // 2. If a specific manager was targeted, check their lockout
        Employee? targetManager = null;
        if (specificManagerId.HasValue)
        {
            targetManager = await _db.Employees
                .Include(e => e.Role)
                .FirstOrDefaultAsync(e => e.Id == specificManagerId.Value && e.RestaurantId == restaurantId && !e.IsDeleted && e.IsActive);

            if (targetManager == null || !IsManagerOrAdmin(targetManager.Role.Name))
            {
                try
                {
                    await RecordFailedPinAttemptAsync(restaurantId, null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist rate-limit attempt for restaurant {RestaurantId}", restaurantId);
                    return (false, null, "Manager authorization failed: rate-limiting service unavailable. Please try again.");
                }
                return (false, null, "Invalid or locked Manager PIN.");
            }

            if (targetManager.PinLockedUntilUtc.HasValue && targetManager.PinLockedUntilUtc.Value > DateTime.UtcNow)
            {
                var waitMin = Math.Max(1, Math.Ceiling((targetManager.PinLockedUntilUtc.Value - DateTime.UtcNow).TotalMinutes));
                return (false, null, $"Manager PIN verification is locked due to multiple failed attempts. Please wait {waitMin} minute(s).");
            }
        }

        // 3. Perform cryptographic PIN check
        Employee? matchingManager = null;
        if (targetManager != null)
        {
            if (!string.IsNullOrEmpty(targetManager.ManagerPinHash) && VerifyPin(pin, targetManager.ManagerPinHash))
            {
                matchingManager = targetManager;
            }
        }
        else
        {
            var eligibleManagers = await _db.Employees
                .Include(e => e.Role)
                .Where(e => e.RestaurantId == restaurantId && !e.IsDeleted && e.IsActive && !string.IsNullOrEmpty(e.ManagerPinHash))
                .ToListAsync();

            foreach (var mgr in eligibleManagers.Where(m => IsManagerOrAdmin(m.Role.Name)))
            {
                if (mgr.PinLockedUntilUtc.HasValue && mgr.PinLockedUntilUtc.Value > DateTime.UtcNow)
                {
                    continue; // Skip currently locked manager
                }

                if (VerifyPin(pin, mgr.ManagerPinHash!))
                {
                    matchingManager = mgr;
                    break;
                }
            }
        }

        // 4. Handle result
        if (matchingManager == null)
        {
            try
            {
                await RecordFailedPinAttemptAsync(restaurantId, targetManager);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist rate-limit attempt for restaurant {RestaurantId}", restaurantId);
                return (false, null, "Manager authorization failed: rate-limiting service unavailable. Please try again.");
            }
            return (false, null, "Invalid or locked Manager PIN.");
        }

        // Success: Reset failed attempts
        await ResetPinAttemptsAsync(restaurantId, matchingManager);
        return (true, matchingManager, null);
    }

    private async Task RecordFailedPinAttemptAsync(Guid restaurantId, Employee? targetManager)
    {
        DateTime now = DateTime.UtcNow;
        DateTime lockedUntil = now.AddMinutes(15);

        if (_db.Database.IsRelational())
        {
            var connStr = _db.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connStr))
            {
                throw new InvalidOperationException("Database connection string unavailable for rate limiting.");
            }

            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            // 1. Atomic PostgreSQL UPSERT for tenant rate limiting
            const string tenantSql = @"
                INSERT INTO ""ManagerPinRateLimits"" (""RestaurantId"", ""FailedAttempts"", ""LastAttemptAtUtc"", ""LockedUntilUtc"")
                VALUES (@resId, 1, @now, NULL)
                ON CONFLICT (""RestaurantId"") DO UPDATE
                SET ""FailedAttempts"" = CASE
                        WHEN ""ManagerPinRateLimits"".""LockedUntilUtc"" IS NOT NULL AND ""ManagerPinRateLimits"".""LockedUntilUtc"" > @now
                            THEN ""ManagerPinRateLimits"".""FailedAttempts""
                        WHEN ""ManagerPinRateLimits"".""LockedUntilUtc"" IS NOT NULL AND ""ManagerPinRateLimits"".""LockedUntilUtc"" <= @now
                            THEN 1
                        ELSE ""ManagerPinRateLimits"".""FailedAttempts"" + 1
                    END,
                    ""LockedUntilUtc"" = CASE
                        WHEN ""ManagerPinRateLimits"".""LockedUntilUtc"" IS NOT NULL AND ""ManagerPinRateLimits"".""LockedUntilUtc"" > @now
                            THEN ""ManagerPinRateLimits"".""LockedUntilUtc""
                        WHEN ""ManagerPinRateLimits"".""LockedUntilUtc"" IS NOT NULL AND ""ManagerPinRateLimits"".""LockedUntilUtc"" <= @now
                            THEN NULL
                        WHEN ""ManagerPinRateLimits"".""FailedAttempts"" + 1 >= 5
                            THEN @lockedUntil
                        ELSE NULL
                    END,
                    ""LastAttemptAtUtc"" = @now
                RETURNING ""FailedAttempts"", ""LockedUntilUtc"";";

            await using (var cmd = new NpgsqlCommand(tenantSql, conn))
            {
                cmd.Parameters.AddWithValue("resId", restaurantId);
                cmd.Parameters.AddWithValue("now", now);
                cmd.Parameters.AddWithValue("lockedUntil", lockedUntil);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    int failedAttempts = reader.GetInt32(0);
                    if (failedAttempts >= 5)
                    {
                        _logger.LogWarning("Tenant {RestaurantId} manager PIN locked out for 15 minutes after {Attempts} failed attempts.", restaurantId, failedAttempts);
                    }
                }
            }

            // 2. Atomic update for targeted manager if specified
            if (targetManager != null)
            {
                const string mgrSql = @"
                    UPDATE ""Employees""
                    SET ""FailedPinAttempts"" = CASE
                            WHEN ""PinLockedUntilUtc"" IS NOT NULL AND ""PinLockedUntilUtc"" > @now
                                THEN ""FailedPinAttempts""
                            WHEN ""PinLockedUntilUtc"" IS NOT NULL AND ""PinLockedUntilUtc"" <= @now
                                THEN 1
                            ELSE ""FailedPinAttempts"" + 1
                        END,
                        ""PinLockedUntilUtc"" = CASE
                            WHEN ""PinLockedUntilUtc"" IS NOT NULL AND ""PinLockedUntilUtc"" > @now
                                THEN ""PinLockedUntilUtc""
                            WHEN ""PinLockedUntilUtc"" IS NOT NULL AND ""PinLockedUntilUtc"" <= @now
                                THEN NULL
                            WHEN ""FailedPinAttempts"" + 1 >= 5
                                THEN @lockedUntil
                            ELSE NULL
                        END
                    WHERE ""Id"" = @mgrId AND ""RestaurantId"" = @resId;";

                await using var cmd = new NpgsqlCommand(mgrSql, conn);
                cmd.Parameters.AddWithValue("mgrId", targetManager.Id);
                cmd.Parameters.AddWithValue("resId", restaurantId);
                cmd.Parameters.AddWithValue("lockedUntil", lockedUntil);
                cmd.Parameters.AddWithValue("now", now);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        else
        {
            // Thread-safe in-memory fallback for test harnesses
            await _inMemoryLock.WaitAsync();
            try
            {
                var rateLimit = await _db.ManagerPinRateLimits.FirstOrDefaultAsync(r => r.RestaurantId == restaurantId);
                if (rateLimit == null)
                {
                    rateLimit = new ManagerPinRateLimit { RestaurantId = restaurantId, FailedAttempts = 1, LastAttemptAtUtc = now };
                    _db.ManagerPinRateLimits.Add(rateLimit);
                }
                else
                {
                    rateLimit.LastAttemptAtUtc = now;
                    if (rateLimit.LockedUntilUtc.HasValue && rateLimit.LockedUntilUtc.Value > now)
                    {
                        // Active lockout: preserve attempts and existing deadline without extending
                    }
                    else if (rateLimit.LockedUntilUtc.HasValue && rateLimit.LockedUntilUtc.Value <= now)
                    {
                        // Expired lockout: start fresh failure window
                        rateLimit.FailedAttempts = 1;
                        rateLimit.LockedUntilUtc = null;
                    }
                    else
                    {
                        rateLimit.FailedAttempts++;
                        if (rateLimit.FailedAttempts >= 5)
                        {
                            rateLimit.LockedUntilUtc = lockedUntil;
                            _logger.LogWarning("Tenant {RestaurantId} manager PIN locked out for 15 minutes after 5 failed attempts.", restaurantId);
                        }
                    }
                }

                if (targetManager != null)
                {
                    if (targetManager.PinLockedUntilUtc.HasValue && targetManager.PinLockedUntilUtc.Value > now)
                    {
                        // Active lockout: preserve
                    }
                    else if (targetManager.PinLockedUntilUtc.HasValue && targetManager.PinLockedUntilUtc.Value <= now)
                    {
                        targetManager.FailedPinAttempts = 1;
                        targetManager.PinLockedUntilUtc = null;
                    }
                    else
                    {
                        targetManager.FailedPinAttempts++;
                        if (targetManager.FailedPinAttempts >= 5)
                        {
                            targetManager.PinLockedUntilUtc = lockedUntil;
                            _logger.LogWarning("Manager {EmployeeId} locked out for 15 minutes after 5 failed attempts.", targetManager.Id);
                        }
                    }
                }

                await _db.SaveChangesAsync();
            }
            finally
            {
                _inMemoryLock.Release();
            }
        }
    }

    private async Task ResetPinAttemptsAsync(Guid restaurantId, Employee manager)
    {
        DateTime now = DateTime.UtcNow;

        if (_db.Database.IsRelational())
        {
            var connStr = _db.Database.GetConnectionString();
            if (!string.IsNullOrWhiteSpace(connStr))
            {
                try
                {
                    await using var conn = new NpgsqlConnection(connStr);
                    await conn.OpenAsync();

                    // Only reset if no active lockout exists, preventing a racing success from clearing a newer lockout
                    const string sql = @"
                        UPDATE ""ManagerPinRateLimits""
                        SET ""FailedAttempts"" = 0, ""LockedUntilUtc"" = NULL
                        WHERE ""RestaurantId"" = @resId AND (""LockedUntilUtc"" IS NULL OR ""LockedUntilUtc"" <= @now);

                        UPDATE ""Employees""
                        SET ""FailedPinAttempts"" = 0, ""PinLockedUntilUtc"" = NULL
                        WHERE ""Id"" = @mgrId AND ""RestaurantId"" = @resId AND (""PinLockedUntilUtc"" IS NULL OR ""PinLockedUntilUtc"" <= @now);";

                    await using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("resId", restaurantId);
                    cmd.Parameters.AddWithValue("mgrId", manager.Id);
                    cmd.Parameters.AddWithValue("now", now);
                    await cmd.ExecuteNonQueryAsync();
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reset PIN attempts on independent connection for restaurant {RestaurantId}", restaurantId);
                }
            }
        }

        // In-memory or fallback
        await _inMemoryLock.WaitAsync();
        try
        {
            var rateLimit = await _db.ManagerPinRateLimits.FirstOrDefaultAsync(r => r.RestaurantId == restaurantId);
            if (rateLimit != null && (!rateLimit.LockedUntilUtc.HasValue || rateLimit.LockedUntilUtc.Value <= now))
            {
                rateLimit.FailedAttempts = 0;
                rateLimit.LockedUntilUtc = null;
            }

            if (!manager.PinLockedUntilUtc.HasValue || manager.PinLockedUntilUtc.Value <= now)
            {
                manager.FailedPinAttempts = 0;
                manager.PinLockedUntilUtc = null;
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset PIN attempts in DB for restaurant {RestaurantId}", restaurantId);
        }
        finally
        {
            _inMemoryLock.Release();
        }
    }

    public async Task<Employee?> VerifyPinAsync(Guid restaurantId, string pin)
    {
        var (success, approver, _) = await VerifyAndRecordManagerPinAsync(restaurantId, pin, null);
        return success ? approver : null;
    }

    public async Task<bool> IsManagerOrAdminAsync(Guid restaurantId, Guid employeeId)
    {
        var emp = await _db.Employees.Include(e => e.Role).FirstOrDefaultAsync(e => e.Id == employeeId && e.RestaurantId == restaurantId && !e.IsDeleted && e.IsActive);
        return emp != null && IsManagerOrAdmin(emp.Role.Name);
    }

    public async Task<bool> SetPinAsync(Guid restaurantId, Guid employeeId, string pin)
    {
        var emp = await _db.Employees.Include(e => e.Role).FirstOrDefaultAsync(e => e.Id == employeeId && e.RestaurantId == restaurantId && !e.IsDeleted && e.IsActive);
        if (emp == null || !IsManagerOrAdmin(emp.Role.Name)) return false;
        emp.ManagerPinHash = HashPin(pin);
        emp.FailedPinAttempts = 0;
        emp.PinLockedUntilUtc = null;
        await _db.SaveChangesAsync();
        return true;
    }

    public static bool IsManagerOrAdmin(string roleName)
    {
        return string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(roleName, "Manager", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(roleName, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
    }

    public static string HashPin(string pin)
    {
        return BCrypt.Net.BCrypt.HashPassword(pin);
    }

    public static bool VerifyPin(string pin, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(pin, hash);
        }
        catch
        {
            return false;
        }
    }
}
