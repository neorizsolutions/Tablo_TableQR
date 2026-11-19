using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities;

public class SubscriptionAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }

    [Required]
    [MaxLength(100)]
    public string ActorEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty; // Created, Upgraded, Downgraded, Renewed, Suspended, Reactivated, FeaturesUpdated

    public Guid? FromPlanId { get; set; }
    public Plan? FromPlan { get; set; }

    public Guid? ToPlanId { get; set; }
    public Plan? ToPlan { get; set; }

    public SubscriptionStatus? FromStatus { get; set; }

    public SubscriptionStatus? ToStatus { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public string? DetailsJson { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
