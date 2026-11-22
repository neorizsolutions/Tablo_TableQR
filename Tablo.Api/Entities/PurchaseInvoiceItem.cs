using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class PurchaseInvoiceItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PurchaseInvoiceId { get; set; }
    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;

    public Guid? PurchaseOrderItemId { get; set; }
    public PurchaseOrderItem? PurchaseOrderItem { get; set; }

    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string PackUnit { get; set; } = "Pack";

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReceivedPackQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ConversionFactor { get; set; } = 1m;

    [Column(TypeName = "decimal(18,4)")]
    public decimal BaseQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal PackPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineDiscount { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTax { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalLineCost { get; set; }

    /// <summary>
    /// Effective fully landed unit cost in INR per ingredient base unit.
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal UnitCostPerBaseUnit { get; set; }

    public Guid? StockLotId { get; set; }
    public StockLot? StockLot { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReturnedBaseQuantity { get; set; } = 0m;
}
