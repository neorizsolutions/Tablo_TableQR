using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class RecipeItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    /// <summary>
    /// Usable net quantity required per serving in the ingredient's base unit.
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal UsableQuantity { get; set; }

    /// <summary>
    /// Ingredient yield percentage (e.g. 80.00 for 80%).
    /// Raw stock consumed per serving = UsableQuantity / (YieldPercent / 100).
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal YieldPercent { get; set; } = 100m;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
