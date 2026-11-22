using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class ItemCancellationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string ItemNameSnapshot { get; set; } = string.Empty;

    public int QuantityCancelled { get; set; }
    public int PreviousQuantity { get; set; }
    public int NewQuantity { get; set; }

    /// <summary>
    /// "NotCooked" (reservation released, zero waste movement),
    /// "KitchenWaste" (ingredient consumption loss recorded once as Waste).
    /// </summary>
    [Required]
    [MaxLength(30)]
    public string ProductionOutcome { get; set; } = "NotCooked";

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public Guid? RequestedByEmployeeId { get; set; }
    public Employee? RequestedByEmployee { get; set; }

    public Guid? ApprovedByEmployeeId { get; set; }
    public Employee? ApprovedByEmployee { get; set; }

    [MaxLength(100)]
    public string? ApproverNameSnapshot { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? WastedCost { get; set; }

    [MaxLength(64)]
    public string? OperationId { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
