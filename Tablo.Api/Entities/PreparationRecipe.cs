using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class PreparationRecipe
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid OutputIngredientId { get; set; }
    public Ingredient OutputIngredient { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal ExpectedYieldQuantity { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<PreparationRecipeItem> InputItems { get; set; } = new List<PreparationRecipeItem>();
    public ICollection<PreparationBatch> Batches { get; set; } = new List<PreparationBatch>();
}
