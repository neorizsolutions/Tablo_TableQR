using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities;

public class PrintJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid? KotTicketId { get; set; }
    public KotTicket? KotTicket { get; set; }

    public Guid PrinterId { get; set; }
    public ThermalPrinter Printer { get; set; } = null!;

    public Guid? StationId { get; set; }
    public KitchenStation? Station { get; set; }

    [Required]
    [MaxLength(50)]
    public string JobType { get; set; } = "KOT"; // KOT, TestPrint, Reprint, Notice

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Claimed, Sent, Printed, Failed, UncertainDelivery, Cancelled

    [Required]
    public string PayloadEscPosBase64 { get; set; } = string.Empty;

    [Required]
    public string ReadableText { get; set; } = string.Empty; // Human-readable monospace representation for simulator/preview

    public int PaperWidthMm { get; set; } = 80;

    public int Attempts { get; set; } = 0;

    public int MaxAttempts { get; set; } = 3;

    public DateTime? LastAttemptUtc { get; set; }

    [MaxLength(1000)]
    public string? LastError { get; set; }

    public Guid? ClaimedByAgentId { get; set; }

    public DateTime? ClaimedAtUtc { get; set; }

    [MaxLength(64)]
    public string? ClaimToken { get; set; }

    public DateTime? LeaseExpiresAtUtc { get; set; }

    [MaxLength(30)]
    public string DeliveryState { get; set; } = "Certain"; // Certain, Uncertain

    [MaxLength(500)]
    public string? UncertainReason { get; set; }

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    public DateTime? CompletedAtUtc { get; set; }

    [MaxLength(100)]
    public string CreatedBy { get; set; } = "System";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// JSON array of attempt history entries:
    /// [{ "timestamp": "...", "status": "Failed", "error": "Connection timed out", "durationMs": 5000 }]
    /// </summary>
    public string AttemptHistoryJson { get; set; } = "[]";
}
