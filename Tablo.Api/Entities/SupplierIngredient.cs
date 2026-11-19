using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class SupplierIngredient
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string PackUnit { get; set; } = "Pack";

    [Column(TypeName = "decimal(18,4)")]
    public decimal PackQuantity { get; set; } = 1m;

    [Required]
    [MaxLength(20)]
    public string PackUnitType { get; set; } = "g";

    /// <summary>
    /// Multiplier to convert 1 PackUnit into Ingredient.BaseUnit.
    /// E.g. 25 kg sack to base unit 'g' -> ConversionFactor = 25000.
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal ConversionFactor { get; set; } = 1m;

    [Column(TypeName = "decimal(18,4)")]
    public decimal LastPurchasePricePerPack { get; set; } = 0m;

    /// <summary>
    /// LastPurchasePricePerPack / ConversionFactor (INR per base unit).
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal NormalizedPricePerBaseUnit { get; set; } = 0m;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
