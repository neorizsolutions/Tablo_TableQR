using Tablo.Api.Entities;

namespace Tablo.Api.DTOs;

public class EffectiveAccessDto
{
    public Guid RestaurantId { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public Guid? OutletId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeEmail { get; set; } = string.Empty;

    public PlanSummaryDto Plan { get; set; } = new();
    public List<string> Capabilities { get; set; } = new();
    public PlanLimitsDto Limits { get; set; } = new();
}

public class PlanSummaryDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public decimal MonthlyPriceInr { get; set; }
    public bool AutoRenew { get; set; }
}

public class PlanLimitsDto
{
    public int MaxOutlets { get; set; }
    public int MaxTables { get; set; }
    public int MaxKitchenStations { get; set; }
}

public class PlanDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MonthlyPriceInr { get; set; }
    public int MaxOutlets { get; set; }
    public int MaxTables { get; set; }
    public int MaxKitchenStations { get; set; }
    public List<string> Features { get; set; } = new();
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public class SubscriptionDto
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public bool AutoRenew { get; set; }
    public string? Notes { get; set; }
    public List<string> EnabledFeatures { get; set; } = new();
}

public class ChangeSubscriptionRequestDto
{
    public string? TargetPlanCode { get; set; }
    public Guid? TargetPlanId { get; set; }
    public string? NewStatus { get; set; } // Active, Trial, Suspended, Expired
    public DateTime? EndDateUtc { get; set; }
    public bool? AutoRenew { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class UpdatePlanFeaturesRequestDto
{
    public List<string> Features { get; set; } = new();
    public int? MaxKitchenStations { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class SubscriptionAuditLogDto
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? FromPlanCode { get; set; }
    public string? ToPlanCode { get; set; }
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string? DetailsJson { get; set; }
}

public class SuperAdminOverviewDto
{
    public int TotalRestaurants { get; set; }
    public int ActiveRestaurants { get; set; }
    public int DisabledRestaurants { get; set; }
    public int TotalSubscriptions { get; set; }
    public int ActiveSubscriptions { get; set; }
    public int TrialSubscriptions { get; set; }
    public int SuspendedSubscriptions { get; set; }
    public int ExpiredSubscriptions { get; set; }
    public int ExpiringSoonCount { get; set; }
    public int TotalEmployees { get; set; }
    public Dictionary<string, int> SubscriptionsByPlan { get; set; } = new();
}

public class PlanChangePreviewDto
{
    public string FromPlanCode { get; set; } = string.Empty;
    public string ToPlanCode { get; set; } = string.Empty;
    public List<string> CapabilitiesGained { get; set; } = new();
    public List<string> CapabilitiesLost { get; set; } = new();
    public List<string> UnchangedCapabilities { get; set; } = new();
}
