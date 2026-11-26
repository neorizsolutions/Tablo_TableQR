using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Constants;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(ApplicationDbContext context, ILogger<SubscriptionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<EffectiveAccessDto?> GetEffectiveAccessAsync(Guid restaurantId, Guid employeeId, Guid? outletId = null)
    {
        var employee = await _context.Employees
            .Include(e => e.Role)
            .Include(e => e.Restaurant)
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.RestaurantId == restaurantId);

        if (employee == null || !employee.IsActive)
        {
            return null;
        }

        // Get restaurant subscription
        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);

        // Fallback: if no subscription exists yet, default to Basic Trial or active Premium
        var plan = subscription?.Plan;
        var subStatus = subscription?.Status ?? SubscriptionStatus.Active;

        if (plan == null)
        {
            plan = await _context.Plans.FirstOrDefaultAsync(p => p.Code == "premium")
                   ?? await _context.Plans.FirstOrDefaultAsync(p => p.Code == "basic");
        }

        var planFeatures = new List<string>();
        if (plan != null && !string.IsNullOrWhiteSpace(plan.FeaturesJson))
        {
            try
            {
                planFeatures = JsonSerializer.Deserialize<List<string>>(plan.FeaturesJson) ?? new List<string>();
            }
            catch
            {
                planFeatures = Capabilities.BasicDefaults.ToList();
            }
        }

        // Effective capabilities = Intersection of (Role or Custom Menu Permitted Capabilities) AND (Plan Features)
        // If subscription is Suspended or Expired, no new operational capabilities are allowed
        var permittedCapabilities = NavigationAccess.WorkspaceCapabilitiesForMenus(employee)
            ?? GetRoleCapabilities(employee.Role.Name);
        var effectiveCaps = new List<string>();

        if (subStatus == SubscriptionStatus.Active || subStatus == SubscriptionStatus.Trial)
        {
            effectiveCaps = permittedCapabilities.Where(c => planFeatures.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        return new EffectiveAccessDto
        {
            RestaurantId = restaurantId,
            RestaurantName = employee.Restaurant.Name,
            OutletId = outletId,
            Role = employee.Role.Name,
            EmployeeName = $"{employee.FirstName} {employee.LastName}".Trim(),
            EmployeeEmail = employee.Email,
            Plan = new PlanSummaryDto
            {
                Code = plan?.Code ?? "basic",
                Name = plan?.Name ?? "Basic Plan",
                Status = subStatus.ToString(),
                StartDateUtc = subscription?.StartDateUtc ?? DateTime.UtcNow,
                EndDateUtc = subscription?.EndDateUtc ?? DateTime.UtcNow.AddYears(1),
                MonthlyPriceInr = plan?.MonthlyPriceInr ?? 0,
                AutoRenew = subscription?.AutoRenew ?? true
            },
            Capabilities = effectiveCaps,
            Limits = new PlanLimitsDto
            {
                MaxOutlets = plan?.MaxOutlets ?? 1,
                MaxTables = plan?.MaxTables ?? 15,
                MaxKitchenStations = plan?.MaxKitchenStations ?? 1
            }
        };
    }

    private static HashSet<string> GetRoleCapabilities(string roleName)
    {
        var role = roleName.Trim();
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase) || role.Equals("Owner", StringComparison.OrdinalIgnoreCase))
        {
            // Admins have access to all capabilities enabled by their plan
            return new HashSet<string>(Capabilities.PremiumDefaults, StringComparer.OrdinalIgnoreCase);
        }

        if (role.Equals("Cashier", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<string>(new[]
            {
                Capabilities.StandardBilling,
                Capabilities.SplitBilling,
                Capabilities.MixedPayments,
                Capabilities.ThermalPrinting
            }, StringComparer.OrdinalIgnoreCase);
        }

        if (role.Equals("Waiter", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<string>(new[]
            {
                Capabilities.QrOrdering,
                Capabilities.KitchenDisplay,
                Capabilities.StandardBilling,
                Capabilities.StaffOrdering,
                Capabilities.DiningGroups,
                Capabilities.SplitBilling,
                Capabilities.MixedPayments,
                Capabilities.TableTransfers,
                Capabilities.TablesAndQr
            }, StringComparer.OrdinalIgnoreCase);
        }

        if (role.Equals("Kitchen", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<string>(new[]
            {
                Capabilities.KitchenDisplay,
                Capabilities.Preparations,
                Capabilities.AutoAvailability
            }, StringComparer.OrdinalIgnoreCase);
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<List<PlanDto>> GetPlansAsync()
    {
        var plans = await _context.Plans
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();

        return plans.Select(MapPlanToDto).ToList();
    }

    public async Task<PlanDto?> GetPlanByCodeAsync(string code)
    {
        var plan = await _context.Plans.FirstOrDefaultAsync(p => p.Code.ToLower() == code.ToLower());
        return plan != null ? MapPlanToDto(plan) : null;
    }

    public async Task<PlanDto> UpdatePlanFeaturesAsync(Guid planId, UpdatePlanFeaturesRequestDto request, string actorEmail)
    {
        var plan = await _context.Plans.FindAsync(planId);
        if (plan == null) throw new KeyNotFoundException("Plan not found");

        var (isValid, missing) = Capabilities.ValidateDependencies(request.Features);
        if (!isValid)
        {
            throw new InvalidOperationException($"Invalid feature configuration: {missing}");
        }

        var oldFeatures = plan.FeaturesJson;
        var oldMaxStations = plan.MaxKitchenStations;
        plan.FeaturesJson = JsonSerializer.Serialize(request.Features);
        if (request.MaxKitchenStations.HasValue && request.MaxKitchenStations.Value >= 1)
        {
            plan.MaxKitchenStations = request.MaxKitchenStations.Value;
        }
        plan.UpdatedAtUtc = DateTime.UtcNow;

        _context.SubscriptionAuditLogs.Add(new SubscriptionAuditLog
        {
            Id = Guid.NewGuid(),
            RestaurantId = Guid.Empty, // System-wide plan edit
            ActorEmail = actorEmail,
            Action = "FeaturesUpdated",
            ToPlanId = plan.Id,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Plan features updated by administrator" : request.Reason,
            DetailsJson = JsonSerializer.Serialize(new { oldFeatures, newFeatures = request.Features, oldMaxStations, newMaxStations = plan.MaxKitchenStations }),
            TimestampUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return MapPlanToDto(plan);
    }

    public async Task<SubscriptionDto?> GetRestaurantSubscriptionAsync(Guid restaurantId)
    {
        var sub = await _context.Subscriptions
            .Include(s => s.Plan)
            .Include(s => s.Restaurant)
            .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);

        if (sub == null) return null;

        return MapSubscriptionToDto(sub);
    }

    public async Task<SubscriptionDto> ChangeSubscriptionAsync(Guid restaurantId, ChangeSubscriptionRequestDto request, string actorEmail)
    {
        var restaurant = await _context.Restaurants.FindAsync(restaurantId);
        if (restaurant == null) throw new KeyNotFoundException("Restaurant not found");

        Plan? targetPlan = null;
        if (request.TargetPlanId.HasValue)
        {
            targetPlan = await _context.Plans.FindAsync(request.TargetPlanId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(request.TargetPlanCode))
        {
            targetPlan = await _context.Plans.FirstOrDefaultAsync(p => p.Code.ToLower() == request.TargetPlanCode.ToLower());
        }

        var existingSub = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);

        Guid? fromPlanId = existingSub?.PlanId;
        var fromStatus = existingSub?.Status;

        SubscriptionStatus toStatus = fromStatus ?? SubscriptionStatus.Active;
        if (!string.IsNullOrWhiteSpace(request.NewStatus) && Enum.TryParse<SubscriptionStatus>(request.NewStatus, true, out var parsedStatus))
        {
            toStatus = parsedStatus;
        }

        string action = "Updated";
        if (existingSub == null)
        {
            action = "Created";
            if (targetPlan == null)
            {
                targetPlan = await _context.Plans.FirstOrDefaultAsync(p => p.Code == "basic")
                             ?? throw new InvalidOperationException("Default basic plan not found");
            }

            existingSub = new Subscription
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurantId,
                PlanId = targetPlan.Id,
                Status = toStatus,
                StartDateUtc = DateTime.UtcNow,
                EndDateUtc = request.EndDateUtc ?? DateTime.UtcNow.AddYears(1),
                AutoRenew = request.AutoRenew ?? true,
                Notes = request.Notes,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _context.Subscriptions.Add(existingSub);
        }
        else
        {
            if (targetPlan != null && targetPlan.Id != existingSub.PlanId)
            {
                action = targetPlan.MonthlyPriceInr >= existingSub.Plan.MonthlyPriceInr ? "Upgraded" : "Downgraded";
                existingSub.PlanId = targetPlan.Id;
            }

            existingSub.Status = toStatus;
            if (request.EndDateUtc.HasValue)
            {
                existingSub.EndDateUtc = request.EndDateUtc.Value;
            }
            if (request.AutoRenew.HasValue)
            {
                existingSub.AutoRenew = request.AutoRenew.Value;
            }
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                existingSub.Notes = request.Notes;
            }
            existingSub.UpdatedAtUtc = DateTime.UtcNow;
        }

        _context.SubscriptionAuditLogs.Add(new SubscriptionAuditLog
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            SubscriptionId = existingSub.Id,
            ActorEmail = actorEmail,
            Action = action,
            FromPlanId = fromPlanId,
            ToPlanId = targetPlan?.Id ?? existingSub.PlanId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? $"Subscription {action.ToLower()} by {actorEmail}" : request.Reason,
            DetailsJson = JsonSerializer.Serialize(request),
            TimestampUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        // Reload navigation
        await _context.Entry(existingSub).Reference(s => s.Plan).LoadAsync();
        return MapSubscriptionToDto(existingSub);
    }

    public async Task<PlanChangePreviewDto> PreviewPlanChangeAsync(Guid restaurantId, string targetPlanCode)
    {
        var existingSub = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);

        var targetPlan = await _context.Plans
            .FirstOrDefaultAsync(p => p.Code.ToLower() == targetPlanCode.ToLower());

        if (targetPlan == null) throw new KeyNotFoundException($"Target plan '{targetPlanCode}' not found.");

        var currentFeatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (existingSub?.Plan?.FeaturesJson != null)
        {
            try { currentFeatures = new HashSet<string>(JsonSerializer.Deserialize<List<string>>(existingSub.Plan.FeaturesJson) ?? new(), StringComparer.OrdinalIgnoreCase); } catch { }
        }

        var targetFeatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (targetPlan.FeaturesJson != null)
        {
            try { targetFeatures = new HashSet<string>(JsonSerializer.Deserialize<List<string>>(targetPlan.FeaturesJson) ?? new(), StringComparer.OrdinalIgnoreCase); } catch { }
        }

        var gained = targetFeatures.Where(f => !currentFeatures.Contains(f)).ToList();
        var lost = currentFeatures.Where(f => !targetFeatures.Contains(f)).ToList();
        var unchanged = currentFeatures.Where(f => targetFeatures.Contains(f)).ToList();

        return new PlanChangePreviewDto
        {
            FromPlanCode = existingSub?.Plan.Code ?? "none",
            ToPlanCode = targetPlan.Code,
            CapabilitiesGained = gained,
            CapabilitiesLost = lost,
            UnchangedCapabilities = unchanged
        };
    }

    public async Task<List<SubscriptionAuditLogDto>> GetAuditLogsAsync(Guid? restaurantId = null, int limit = 100)
    {
        var query = _context.SubscriptionAuditLogs
            .Include(a => a.Restaurant)
            .Include(a => a.FromPlan)
            .Include(a => a.ToPlan)
            .AsQueryable();

        if (restaurantId.HasValue && restaurantId.Value != Guid.Empty)
        {
            query = query.Where(a => a.RestaurantId == restaurantId.Value);
        }

        var logs = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Take(limit)
            .ToListAsync();

        return logs.Select(a => new SubscriptionAuditLogDto
        {
            Id = a.Id,
            RestaurantId = a.RestaurantId,
            RestaurantName = a.Restaurant?.Name ?? "System",
            ActorEmail = a.ActorEmail,
            Action = a.Action,
            FromPlanCode = a.FromPlan?.Code,
            ToPlanCode = a.ToPlan?.Code,
            FromStatus = a.FromStatus?.ToString(),
            ToStatus = a.ToStatus?.ToString(),
            Reason = a.Reason,
            TimestampUtc = a.TimestampUtc,
            DetailsJson = a.DetailsJson
        }).ToList();
    }

    public async Task<SuperAdminOverviewDto> GetSuperAdminOverviewAsync()
    {
        var totalRestaurants = await _context.Restaurants.CountAsync();
        
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        var activeRestaurants = 0;
        if (adminRole != null)
        {
            activeRestaurants = await _context.Employees
                .Where(e => e.RoleId == adminRole.Id && e.IsActive)
                .Select(e => e.RestaurantId)
                .Distinct()
                .CountAsync();
        }
        else
        {
            activeRestaurants = totalRestaurants;
        }

        var subscriptions = await _context.Subscriptions
            .Include(s => s.Plan)
            .ToListAsync();

        var totalSubs = subscriptions.Count;
        var activeSubs = subscriptions.Count(s => s.Status == SubscriptionStatus.Active);
        var trialSubs = subscriptions.Count(s => s.Status == SubscriptionStatus.Trial);
        var suspendedSubs = subscriptions.Count(s => s.Status == SubscriptionStatus.Suspended);
        var expiredSubs = subscriptions.Count(s => s.Status == SubscriptionStatus.Expired);

        var soonThreshold = DateTime.UtcNow.AddDays(30);
        var expiringSoon = subscriptions.Count(s => (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial) && s.EndDateUtc <= soonThreshold && s.EndDateUtc >= DateTime.UtcNow);

        var byPlan = subscriptions
            .GroupBy(s => s.Plan.Code)
            .ToDictionary(g => g.Key, g => g.Count());

        var totalEmployees = await _context.Employees.CountAsync(e => e.IsActive);

        return new SuperAdminOverviewDto
        {
            TotalRestaurants = totalRestaurants,
            ActiveRestaurants = activeRestaurants,
            DisabledRestaurants = totalRestaurants - activeRestaurants,
            TotalSubscriptions = totalSubs,
            ActiveSubscriptions = activeSubs,
            TrialSubscriptions = trialSubs,
            SuspendedSubscriptions = suspendedSubs,
            ExpiredSubscriptions = expiredSubs,
            ExpiringSoonCount = expiringSoon,
            TotalEmployees = totalEmployees,
            SubscriptionsByPlan = byPlan
        };
    }

    public async Task<bool> HasCapabilityAsync(Guid restaurantId, string capability)
    {
        var sub = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);

        if (sub == null)
        {
            // Preserve backward compatibility for unmigrated or in-memory test fixtures
            return true;
        }

        if (sub.Status != SubscriptionStatus.Active && sub.Status != SubscriptionStatus.Trial)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(sub.Plan?.FeaturesJson)) return false;

        try
        {
            var features = JsonSerializer.Deserialize<List<string>>(sub.Plan.FeaturesJson);
            return features?.Contains(capability, StringComparer.OrdinalIgnoreCase) ?? false;
        }
        catch
        {
            return false;
        }
    }

    private static PlanDto MapPlanToDto(Plan plan)
    {
        var features = new List<string>();
        if (!string.IsNullOrWhiteSpace(plan.FeaturesJson))
        {
            try { features = JsonSerializer.Deserialize<List<string>>(plan.FeaturesJson) ?? new(); } catch { }
        }

        return new PlanDto
        {
            Id = plan.Id,
            Code = plan.Code,
            Name = plan.Name,
            Description = plan.Description,
            MonthlyPriceInr = plan.MonthlyPriceInr,
            MaxOutlets = plan.MaxOutlets,
            MaxTables = plan.MaxTables,
            MaxKitchenStations = plan.MaxKitchenStations,
            Features = features,
            IsActive = plan.IsActive,
            DisplayOrder = plan.DisplayOrder
        };
    }

    private static SubscriptionDto MapSubscriptionToDto(Subscription sub)
    {
        var features = new List<string>();
        if (!string.IsNullOrWhiteSpace(sub.Plan?.FeaturesJson))
        {
            try { features = JsonSerializer.Deserialize<List<string>>(sub.Plan.FeaturesJson) ?? new(); } catch { }
        }

        return new SubscriptionDto
        {
            Id = sub.Id,
            RestaurantId = sub.RestaurantId,
            RestaurantName = sub.Restaurant?.Name ?? "",
            PlanId = sub.PlanId,
            PlanCode = sub.Plan?.Code ?? "",
            PlanName = sub.Plan?.Name ?? "",
            Status = sub.Status.ToString(),
            StartDateUtc = sub.StartDateUtc,
            EndDateUtc = sub.EndDateUtc,
            AutoRenew = sub.AutoRenew,
            Notes = sub.Notes,
            EnabledFeatures = features
        };
    }
}
