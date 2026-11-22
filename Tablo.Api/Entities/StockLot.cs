using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class StockLot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }

    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string LotCode { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal InitialQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal AvailableQuantity { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal UnitCost { get; set; }

    public DateTime ReceivedOrProducedDateUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// "Active", "Quarantined", "WrittenOff", "Exhausted"
    /// </summary>
    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "Active";

    [MaxLength(50)]
    public string? SourceType { get; set; }

    [MaxLength(100)]
    public string? SourceReferenceId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
