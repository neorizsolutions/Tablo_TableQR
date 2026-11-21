using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class ModifierOption
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ModifierGroupId { get; set; }
    public ModifierGroup ModifierGroup { get; set; } = null!;

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal PriceDelta { get; set; } = 0.00m;

    public int DisplayOrder { get; set; } = 0;
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// "None", "Add", "Substitute", "Remove"
    /// </summary>
    [MaxLength(30)]
    public string IngredientAdjustmentType { get; set; } = "None";

    public Guid? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal IngredientQuantity { get; set; } = 0m;

    public Guid? OriginalIngredientId { get; set; }
    public Ingredient? OriginalIngredient { get; set; }
}
