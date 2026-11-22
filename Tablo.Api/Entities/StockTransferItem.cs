using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class StockTransferItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StockTransferId { get; set; }
    public StockTransfer StockTransfer { get; set; } = null!;

    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal RequestedQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal DispatchedQuantity { get; set; } = 0m;

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReceivedQuantity { get; set; } = 0m;

    [Column(TypeName = "decimal(18,4)")]
    public decimal DiscrepancyQuantity { get; set; } = 0m;

    [MaxLength(200)]
    public string? DiscrepancyReason { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal UnitCostSnapshot { get; set; } = 0m;
}
