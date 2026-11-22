using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities;

public enum SubscriptionStatus
{
    Trial = 0,
    Active = 1,
    Expired = 2,
    Suspended = 3
}

public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    public DateTime StartDateUtc { get; set; } = DateTime.UtcNow;

    public DateTime EndDateUtc { get; set; } = DateTime.UtcNow.AddYears(1);

    public bool AutoRenew { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<SubscriptionAuditLog> AuditLogs { get; set; } = new List<SubscriptionAuditLog>();
}
