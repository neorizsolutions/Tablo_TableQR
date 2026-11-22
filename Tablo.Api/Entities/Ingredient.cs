using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class Ingredient
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Base unit for stockkeeping: "g", "ml", or "pieces".
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string BaseUnit { get; set; } = "g";

    [Column(TypeName = "decimal(18,4)")]
    public decimal? LowStockThreshold { get; set; }

    /// <summary>
    /// Current moving weighted-average acquisition cost per base unit in INR.
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal CurrentAverageCost { get; set; } = 0m;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Indicates whether this ingredient is an intermediate prepared recipe item (e.g. Biryani Masala, Base Gravy).
    /// </summary>
    public bool IsPrepared { get; set; } = false;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<RecipeItem> RecipeItems { get; set; } = new List<RecipeItem>();
    public ICollection<StocktakeItem> StocktakeItems { get; set; } = new List<StocktakeItem>();
    public ICollection<SupplierIngredient> SupplierIngredients { get; set; } = new List<SupplierIngredient>();
    public ICollection<StockLot> StockLots { get; set; } = new List<StockLot>();
    public PreparationRecipe? PreparationRecipe { get; set; }
}
