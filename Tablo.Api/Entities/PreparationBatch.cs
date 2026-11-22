using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class PreparationBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }

    public Guid PreparationRecipeId { get; set; }
    public PreparationRecipe PreparationRecipe { get; set; } = null!;

    public Guid OutputIngredientId { get; set; }
    public Ingredient OutputIngredient { get; set; } = null!;

    public DateTime PreparedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? PreparedByEmployeeId { get; set; }
    public Employee? PreparedByEmployee { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Multiplier { get; set; } = 1m;

    [Column(TypeName = "decimal(18,4)")]
    public decimal ExpectedYieldQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ActualYieldQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal WasteQuantity { get; set; } = 0m;

    [MaxLength(200)]
    public string? WasteReason { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalInputCost { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal CostPerBaseUnit { get; set; }

    public Guid? OutputStockLotId { get; set; }
    public StockLot? OutputStockLot { get; set; }

    public Guid? OperationId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
