using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    public Guid? ActorEmployeeId { get; set; }
    public Employee? ActorEmployee { get; set; }

    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }

    public Guid? StockLotId { get; set; }
    public StockLot? StockLot { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Quantity change in the ingredient's base unit. Positive for additions, negative for deductions.
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal QuantityChange { get; set; }

    [Required]
    [MaxLength(20)]
    public string UnitSnapshot { get; set; } = string.Empty;

    /// <summary>
    /// Cost per base unit snapshot at the time of movement (INR).
    /// For purchases/receipts, this is the purchase cost per base unit.
    /// For deductions/adjustments, this is the ingredient's average cost at movement time.
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal? UnitCostSnapshot { get; set; }

    /// <summary>
    /// Extended total cost of the movement: |QuantityChange| * UnitCostSnapshot (INR).
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? TotalCost { get; set; }

    /// <summary>
    /// Reason for stock movement: "OpeningStock", "Restock", "Waste", "ManualCorrection", "OrderDeduction", "StocktakeAdjustment"
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ReferenceType { get; set; }

    [MaxLength(100)]
    public string? ReferenceId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Guid? OperationId { get; set; }
}
