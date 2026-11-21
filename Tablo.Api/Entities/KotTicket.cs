using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities;

public class KotTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid KotBatchId { get; set; }
    public KotBatch KotBatch { get; set; } = null!;

    public Guid KitchenStationId { get; set; }
    public KitchenStation KitchenStation { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string StationNameSnapshot { get; set; } = string.Empty;

    public Guid? PrinterId { get; set; }
    public ThermalPrinter? Printer { get; set; }

    [Required]
    [MaxLength(60)]
    public string TicketNumber { get; set; } = string.Empty; // e.g. "KOT-0042-1-FOOD"

    public int OrderNumber { get; set; }

    public Guid? TableId { get; set; }

    public int TableNumber { get; set; }

    public Guid? DiningGroupId { get; set; }

    [MaxLength(50)]
    public string? GroupLabelSnapshot { get; set; }

    [Required]
    [MaxLength(50)]
    public string TicketType { get; set; } = "New"; // New, Incremental, Reprint, TransferNotice, CancellationNotice, CourseFire, Refill

    [MaxLength(50)]
    public string? CourseName { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(100)]
    public string TimestampIstFormatted { get; set; } = string.Empty; // e.g. "19-Nov-2026, 03:52 PM IST"

    /// <summary>
    /// JSON array of snapshot items:
    /// [{ "OrderItemId": "...", "MenuItemId": "...", "Name": "Paneer Tikka", "Quantity": 2, "Instructions": "Less spicy", "UnitPrice": 220 }]
    /// </summary>
    [Required]
    public string ItemsJson { get; set; } = "[]";

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool IsReprint { get; set; } = false;

    public int ReprintCount { get; set; } = 0;

    public Guid? OriginalTicketId { get; set; }
    public KotTicket? OriginalTicket { get; set; }

    [MaxLength(200)]
    public string? ReprintReason { get; set; }

    [MaxLength(100)]
    public string? ReprinterEmail { get; set; }

    public DateTime? PrintedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<PrintJob> PrintJobs { get; set; } = new List<PrintJob>();
}
