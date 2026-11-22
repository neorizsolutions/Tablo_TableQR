using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class PurchaseReturnItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PurchaseReturnId { get; set; }
    public PurchaseReturn PurchaseReturn { get; set; } = null!;

    public Guid PurchaseInvoiceItemId { get; set; }
    public PurchaseInvoiceItem PurchaseInvoiceItem { get; set; } = null!;

    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    public Guid? StockLotId { get; set; }
    public StockLot? StockLot { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReturnQuantity { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal UnitCostSnapshot { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCredit { get; set; }

    [MaxLength(200)]
    public string? Reason { get; set; }
}
