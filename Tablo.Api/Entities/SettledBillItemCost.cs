using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class SettledBillItemCost
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid TableBillId { get; set; }
    public TableBill TableBill { get; set; } = null!;

    public Guid MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string MenuItemNameSnapshot { get; set; } = string.Empty;

    [MaxLength(100)]
    public string CategoryNameSnapshot { get; set; } = string.Empty;

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPriceSnapshot { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrossRevenue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedDiscount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetRevenue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedTax { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalIngredientCost { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal GrossProfit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrossMarginPercent { get; set; }

    public bool IsCostComplete { get; set; } = true;

    [MaxLength(500)]
    public string? IncompleteReason { get; set; }

    public DateTime SettledAtUtc { get; set; }

    public ICollection<SettledBillItemIngredientCost> IngredientCosts { get; set; } = new List<SettledBillItemIngredientCost>();
}
