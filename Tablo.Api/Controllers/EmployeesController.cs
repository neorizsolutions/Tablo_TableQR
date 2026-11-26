using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Extensions;
using Tablo.Api.Hubs;
using Tablo.Api.Services;

namespace Tablo.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,Owner")]
    public class EmployeesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ISupportConnectionRevoker? _revoker;
        private readonly IOrderConnectionRevoker? _orderRevoker;

        // Serializes lifecycle checks across concurrent requests when running under InMemory EF tests.
        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> InMemoryRestaurantLocks = new();

        public EmployeesController(
            ApplicationDbContext context,
            ISupportConnectionRevoker? revoker = null,
            IOrderConnectionRevoker? orderRevoker = null)
        {
            _context = context;
            _revoker = revoker;
            _orderRevoker = orderRevoker;
        }

        private static bool IsAdminOrOwnerRole(string? roleName) =>
            string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleName, "Owner", StringComparison.OrdinalIgnoreCase);

        private async Task<IDisposable?> AcquireRestaurantLockAsync(Guid restaurantId)
        {
            if (_context.Database.IsRelational())
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"Restaurants\" WHERE \"Id\" = {restaurantId} FOR UPDATE");
                return null;
            }

            var sem = InMemoryRestaurantLocks.GetOrAdd(restaurantId, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync();
            return new SemaphoreReleaser(sem);
        }

        private sealed class SemaphoreReleaser(SemaphoreSlim sem) : IDisposable
        {
            public void Dispose() => sem.Release();
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployees([FromQuery] string? status = null)
        {
            var restaurantId = User.GetRestaurantId();
            var normalizedStatus = status?.Trim().ToLowerInvariant();

            if (!string.IsNullOrEmpty(normalizedStatus) &&
                normalizedStatus is not ("active" or "inactive" or "all"))
            {
                return BadRequest(new { message = "Invalid status filter. Use 'active', 'inactive', or 'all'." });
            }

            var query = _context.Employees
                .AsNoTracking()
                .Include(e => e.Role)
                .Where(e => e.RestaurantId == restaurantId && !e.IsDeleted);

            if (normalizedStatus == "active")
            {
                query = query.Where(e => e.IsActive);
            }
            else if (normalizedStatus == "inactive")
            {
                query = query.Where(e => !e.IsActive);
            }

            var employees = await query
                .OrderByDescending(e => e.IsActive)
                .ThenBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .ThenBy(e => e.Email)
                .Select(e => new EmployeeDto
                {
                    Id = e.Id,
                    RestaurantId = e.RestaurantId,
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                    Email = e.Email,
                    Role = e.Role.Name,
                    IsActive = e.IsActive
                })
                .ToListAsync();

            return Ok(employees);
        }

        [HttpGet("counts")]
        public async Task<ActionResult<EmployeeStatusCountsDto>> GetEmployeeCounts()
        {
            var restaurantId = User.GetRestaurantId();
            var activeCount = await _context.Employees
                .AsNoTracking()
                .CountAsync(e => e.RestaurantId == restaurantId && !e.IsDeleted && e.IsActive);
            var inactiveCount = await _context.Employees
                .AsNoTracking()
                .CountAsync(e => e.RestaurantId == restaurantId && !e.IsDeleted && !e.IsActive);

            return Ok(new EmployeeStatusCountsDto
            {
                Active = activeCount,
                Inactive = inactiveCount,
                All = activeCount + inactiveCount
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetEmployee(Guid id)
        {
            var restaurantId = User.GetRestaurantId();

            var employee = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Role)
                .FirstOrDefaultAsync(e => e.Id == id && e.RestaurantId == restaurantId && !e.IsDeleted);

            if (employee == null)
            {
                return NotFound();
            }

            return Ok(new EmployeeDto
            {
                Id = employee.Id,
                RestaurantId = employee.RestaurantId,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                Email = employee.Email,
                Role = employee.Role.Name,
                IsActive = employee.IsActive
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeDto request)
        {
            var restaurantId = User.GetRestaurantId();

            var allowedRoles = new[] { "Admin", "Kitchen", "Waiter", "Cashier" };
            if (!allowedRoles.Contains(request.RoleName))
            {
                return BadRequest("Invalid role. Allowed roles are: Admin, Kitchen, Waiter, Cashier.");
            }

            var normalizedEmail = request.Email.Trim();
            var normalizedEmailLower = normalizedEmail.ToLowerInvariant();
            if (await _context.Employees.AnyAsync(e => !e.IsDeleted && e.Email.ToLower() == normalizedEmailLower))
            {
                return Conflict("Email is already in use.");
            }

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == request.RoleName);
            if (role == null)
            {
                return BadRequest("Role does not exist in the database.");
            }

            var newEmployee = new Employee
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurantId,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = normalizedEmail,
                RoleId = role.Id,
                IsActive = true,
                IsDeleted = false,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            };

            _context.Employees.Add(newEmployee);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetEmployee), new { id = newEmployee.Id }, new EmployeeDto
            {
                Id = newEmployee.Id,
                RestaurantId = newEmployee.RestaurantId,
                FirstName = newEmployee.FirstName,
                LastName = newEmployee.LastName,
                Email = newEmployee.Email,
                Role = role.Name,
                IsActive = newEmployee.IsActive
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEmployee(Guid id, [FromBody] UpdateEmployeeDto request)
        {
            var restaurantId = User.GetRestaurantId();
            var userId = User.GetUserId();

            var allowedRoles = new[] { "Admin", "Kitchen", "Waiter", "Cashier" };
            if (!allowedRoles.Contains(request.RoleName))
            {
                return BadRequest("Invalid role. Allowed roles are: Admin, Kitchen, Waiter, Cashier.");
            }

            var newRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == request.RoleName);
            if (newRole == null)
            {
                return BadRequest("Role does not exist in the database.");
            }

            await using var tx = await TableOrderTransaction.BeginAsync(_context);
            using var memLock = await AcquireRestaurantLockAsync(restaurantId);

            var employee = await _context.Employees
                .Include(e => e.Role)
                .FirstOrDefaultAsync(e => e.Id == id && e.RestaurantId == restaurantId);

            if (employee == null)
            {
                return NotFound();
            }

            if (employee.IsDeleted)
            {
                return BadRequest(new { message = "Deleted staff accounts cannot be modified." });
            }

            if (id == userId && IsAdminOrOwnerRole(employee.Role.Name) && !IsAdminOrOwnerRole(request.RoleName))
            {
                return BadRequest("You cannot demote yourself from Admin.");
            }

            if (id != userId && employee.IsActive && IsAdminOrOwnerRole(employee.Role.Name) && !IsAdminOrOwnerRole(request.RoleName))
            {
                var remainingActiveAdmins = await _context.Employees
                    .CountAsync(e =>
                        e.RestaurantId == restaurantId &&
                        !e.IsDeleted &&
                        e.IsActive &&
                        e.Id != employee.Id &&
                        (e.Role.Name == "Admin" || e.Role.Name == "Owner"));

                if (remainingActiveAdmins == 0)
                {
                    return BadRequest("You cannot demote the only remaining active Admin.");
                }
            }

            var normalizedEmail = request.Email.Trim();
            var normalizedEmailLower = normalizedEmail.ToLowerInvariant();
            if (!string.Equals(employee.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                if (await _context.Employees.AnyAsync(e => !e.IsDeleted && e.Id != id && e.Email.ToLower() == normalizedEmailLower))
                {
                    return Conflict("Email is already in use.");
                }
            }
            employee.Email = normalizedEmail;

            employee.FirstName = request.FirstName;
            employee.LastName = request.LastName;
            bool roleChanged = employee.RoleId != newRole.Id;
            // A prior role's custom menu list can otherwise leave a new role unusable.
            if (roleChanged)
            {
                employee.AllowedMenuIds = null;
                employee.SecurityStamp = Guid.NewGuid().ToString("N");
            }
            employee.RoleId = newRole.Id;

            bool passwordChanged = !string.IsNullOrWhiteSpace(request.NewPassword);
            if (passwordChanged)
            {
                if (request.NewPassword!.Length < 6)
                {
                    return BadRequest("New password must be at least 6 characters.");
                }
                employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                employee.SecurityStamp = Guid.NewGuid().ToString("N");
                employee.ResetOtpHash = null;
                employee.ResetTokenHash = null;
                employee.ResetOtpExpiration = null;
            }

            await _context.SaveChangesAsync();
            if (tx != null)
            {
                await tx.CommitAsync();
            }

            if (_revoker != null && (passwordChanged || (roleChanged && !IsAdminOrOwnerRole(request.RoleName))))
            {
                await _revoker.RevokeEmployeeConnectionsAsync(employee.Id,
                    passwordChanged ? "Session invalidated due to password reset." : "Role permissions changed.");
            }
            if (_orderRevoker != null && (passwordChanged || roleChanged))
            {
                await _orderRevoker.SyncEmployeePermissionsAsync(employee,
                    passwordChanged ? "Session invalidated due to password reset." : "Role permissions changed.");
            }

            return Ok(new EmployeeDto
            {
                Id = employee.Id,
                RestaurantId = employee.RestaurantId,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                Email = employee.Email,
                Role = newRole.Name,
                IsActive = employee.IsActive
            });
        }

        [HttpPost("{id}/reset-password")]
        public async Task<IActionResult> ResetEmployeePassword(Guid id, [FromBody] ResetPasswordDto request)
        {
            var restaurantId = User.GetRestaurantId();

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == id && e.RestaurantId == restaurantId);

            if (employee == null)
            {
                return NotFound();
            }

            if (employee.IsDeleted)
            {
                return BadRequest(new { message = "Cannot reset password for a deleted staff account." });
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            {
                return BadRequest("New password must be at least 6 characters.");
            }

            employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            employee.SecurityStamp = Guid.NewGuid().ToString("N");
            employee.ResetOtpHash = null;
            employee.ResetTokenHash = null;
            employee.ResetOtpExpiration = null;

            await _context.SaveChangesAsync();

            if (_revoker != null)
            {
                await _revoker.RevokeEmployeeConnectionsAsync(employee.Id, "Session invalidated due to password reset.");
            }
            if (_orderRevoker != null)
            {
                await _orderRevoker.SyncEmployeePermissionsAsync(employee, "Session invalidated due to password reset.");
            }

            return Ok(new { message = "Password updated successfully and previous sessions invalidated." });
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateEmployeeStatusDto request)
        {
            var restaurantId = User.GetRestaurantId();
            var userId = User.GetUserId();

            await using var tx = await TableOrderTransaction.BeginAsync(_context);
            using var memLock = await AcquireRestaurantLockAsync(restaurantId);

            var employee = await _context.Employees
                .Include(e => e.Role)
                .FirstOrDefaultAsync(e => e.Id == id && e.RestaurantId == restaurantId);

            if (employee == null)
            {
                return NotFound();
            }

            if (employee.IsDeleted)
            {
                return BadRequest(new { message = "Deleted staff accounts cannot be activated or deactivated." });
            }

            if (id == userId && !request.IsActive)
            {
                return BadRequest("You cannot deactivate your own account.");
            }

            if (!request.IsActive && employee.IsActive && IsAdminOrOwnerRole(employee.Role.Name))
            {
                var remainingActiveAdmins = await _context.Employees
                    .CountAsync(e =>
                        e.RestaurantId == restaurantId &&
                        !e.IsDeleted &&
                        e.IsActive &&
                        e.Id != employee.Id &&
                        (e.Role.Name == "Admin" || e.Role.Name == "Owner"));

                if (remainingActiveAdmins == 0)
                {
                    return BadRequest("You cannot deactivate the only remaining active Admin.");
                }
            }

            employee.IsActive = request.IsActive;
            if (!request.IsActive)
            {
                employee.SecurityStamp = Guid.NewGuid().ToString("N");
            }
            await _context.SaveChangesAsync();
            if (tx != null)
            {
                await tx.CommitAsync();
            }

            if (!request.IsActive && _revoker != null)
            {
                await _revoker.RevokeEmployeeConnectionsAsync(employee.Id, "Account deactivated.");
            }
            if (!request.IsActive && _orderRevoker != null)
            {
                await _orderRevoker.SyncEmployeePermissionsAsync(employee, "Account deactivated.");
            }

            return Ok(new { message = request.IsActive ? "Employee activated." : "Employee deactivated." });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmployee(Guid id)
        {
            var restaurantId = User.GetRestaurantId();
            var userId = User.GetUserId();

            if (id == userId)
            {
                return BadRequest(new { message = "You cannot delete your own account." });
            }

            await using var tx = await TableOrderTransaction.BeginAsync(_context);
            using var memLock = await AcquireRestaurantLockAsync(restaurantId);

            var employee = await _context.Employees
                .Include(e => e.Role)
                .FirstOrDefaultAsync(e => e.Id == id && e.RestaurantId == restaurantId);

            if (employee == null)
            {
                return NotFound();
            }

            if (employee.IsDeleted)
            {
                if (tx != null)
                {
                    await tx.CommitAsync();
                }

                return Ok(new DeleteEmployeeResponseDto
                {
                    Message = "Employee account is already deleted.",
                    EmployeeId = employee.Id,
                    AlreadyDeleted = true,
                    DeletedAtUtc = employee.DeletedAtUtc
                });
            }

            if (employee.IsActive && IsAdminOrOwnerRole(employee.Role.Name))
            {
                var remainingActiveAdmins = await _context.Employees
                    .CountAsync(e =>
                        e.RestaurantId == restaurantId &&
                        !e.IsDeleted &&
                        e.IsActive &&
                        e.Id != employee.Id &&
                        (e.Role.Name == "Admin" || e.Role.Name == "Owner"));

                if (remainingActiveAdmins == 0)
                {
                    return BadRequest(new { message = "Cannot delete the last active restaurant administrator." });
                }
            }

            var wasActive = employee.IsActive;
            var now = DateTime.UtcNow;

            employee.IsDeleted = true;
            employee.IsActive = false;
            employee.DeletedAtUtc = now;
            employee.DeletedByEmployeeId = userId != Guid.Empty ? userId : null;
            employee.SecurityStamp = Guid.NewGuid().ToString("N");
            employee.ResetOtpHash = null;
            employee.ResetOtpExpiration = null;
            employee.ResetOtpAttempts = 0;
            employee.ResetTokenHash = null;
            employee.ManagerPinHash = null;
            employee.FailedPinAttempts = 0;
            employee.PinLockedUntilUtc = null;

            var actorEmail = User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.Identity?.Name
                ?? "restaurant-admin";

            _context.EmployeeAuditLogs.Add(new EmployeeAuditLog
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurantId,
                TargetEmployeeId = employee.Id,
                TargetEmail = employee.Email,
                TargetName = $"{employee.FirstName} {employee.LastName}".Trim(),
                TargetRole = employee.Role.Name,
                ActorEmployeeId = userId != Guid.Empty ? userId : null,
                ActorEmail = actorEmail,
                Action = "Deleted",
                Reason = "Archived via Staff Management",
                DetailsJson = JsonSerializer.Serialize(new
                {
                    previousIsActive = wasActive,
                    deletedAtUtc = now
                }),
                TimestampUtc = now
            });

            await _context.SaveChangesAsync();
            if (tx != null)
            {
                await tx.CommitAsync();
            }

            if (_revoker != null)
            {
                await _revoker.RevokeEmployeeConnectionsAsync(employee.Id, "Your staff account was deleted by an administrator.");
            }
            if (_orderRevoker != null)
            {
                await _orderRevoker.SyncEmployeePermissionsAsync(employee, "Your staff account was deleted by an administrator.");
            }

            return Ok(new DeleteEmployeeResponseDto
            {
                Message = "Employee deleted.",
                EmployeeId = employee.Id,
                AlreadyDeleted = false,
                DeletedAtUtc = now
            });
        }

        [HttpPatch("{id}/password")]
        public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordDto request)
        {
            var restaurantId = User.GetRestaurantId();

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == id && e.RestaurantId == restaurantId);

            if (employee == null)
            {
                return NotFound();
            }

            if (employee.IsDeleted)
            {
                return BadRequest(new { message = "Cannot reset password for a deleted staff account." });
            }

            employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            employee.SecurityStamp = Guid.NewGuid().ToString("N");
            employee.ResetOtpHash = null;
            employee.ResetTokenHash = null;
            employee.ResetOtpExpiration = null;
            await _context.SaveChangesAsync();

            if (_revoker != null)
            {
                await _revoker.RevokeEmployeeConnectionsAsync(employee.Id, "Session invalidated due to password reset.");
            }
            if (_orderRevoker != null)
            {
                await _orderRevoker.SyncEmployeePermissionsAsync(employee, "Session invalidated due to password reset.");
            }

            return Ok(new { message = "Password reset successfully." });
        }

        [HttpPost("{id}/pin")]
        public async Task<IActionResult> SetManagerPin(Guid id, [FromBody] SetManagerPinRequestDto request, [FromServices] Services.ManagerAuthService managerAuth)
        {
            var restaurantId = User.GetRestaurantId();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var target = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id && e.RestaurantId == restaurantId);

            if (target == null)
            {
                return NotFound(new { message = "Employee not found or not eligible for manager PIN." });
            }

            if (target.IsDeleted)
            {
                return BadRequest(new { message = "Manager PIN cannot be updated for a deleted staff account." });
            }

            var success = await managerAuth.SetPinAsync(restaurantId, id, request.Pin);
            if (!success)
            {
                return NotFound(new { message = "Employee not found or not eligible for manager PIN." });
            }

            return Ok(new { message = "Manager PIN set successfully." });
        }

        [Authorize(Roles = "Admin,Owner,Waiter,Kitchen,Cashier")]
        [HttpPost("verify-pin")]
        public async Task<IActionResult> VerifyManagerPin([FromBody] VerifyManagerPinRequestDto request, [FromServices] Services.ManagerAuthService managerAuth)
        {
            var restaurantId = User.GetRestaurantId();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var (success, employee, errorMessage) = await managerAuth.VerifyAndRecordManagerPinAsync(restaurantId, request.Pin, null);
            if (!success || employee == null)
            {
                return Unauthorized(new { message = errorMessage ?? "Invalid or locked manager PIN." });
            }

            return Ok(new
            {
                id = employee.Id,
                name = employee.FullName,
                role = employee.Role?.Name ?? "Admin"
            });
        }
    }
}
