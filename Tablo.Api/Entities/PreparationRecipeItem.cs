using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class PreparationRecipeItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PreparationRecipeId { get; set; }
    public PreparationRecipe PreparationRecipe { get; set; } = null!;

    public Guid InputIngredientId { get; set; }
    public Ingredient InputIngredient { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal YieldPercent { get; set; } = 100m;
}
