using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class StocktakeItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StocktakeId { get; set; }
    public Stocktake Stocktake { get; set; } = null!;

    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal RecordedStockSnapshot { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal PhysicalCount { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Variance { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? StockAtPosting { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? FinalAdjustment { get; set; }
}
