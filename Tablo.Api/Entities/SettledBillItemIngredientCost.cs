using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class SettledBillItemIngredientCost
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SettledBillItemCostId { get; set; }
    public SettledBillItemCost SettledBillItemCost { get; set; } = null!;

    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string IngredientNameSnapshot { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string BaseUnitSnapshot { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal RawQuantityPerPortion { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalRawQuantity { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal UnitCostSnapshot { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalCost { get; set; }
}
