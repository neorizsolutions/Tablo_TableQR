using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities;

public class KotBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int BatchSequence { get; set; } = 1; // 1 for initial order, 2 for first addition, etc.

    [Required]
    [MaxLength(50)]
    public string KotNumber { get; set; } = string.Empty; // e.g. "KOT-0042-1"

    [Required]
    [MaxLength(50)]
    public string TriggeredBy { get; set; } = "StaffAccepted"; // StaffAccepted, StaffAddition, CustomerAccepted, OperatorReprint

    [MaxLength(100)]
    public string? ActorEmail { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<KotTicket> Tickets { get; set; } = new List<KotTicket>();
}
