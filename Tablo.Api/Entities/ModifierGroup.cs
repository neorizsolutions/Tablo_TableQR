using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities;

public class ModifierGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int MinSelections { get; set; } = 0; // 0 = optional
    public int MaxSelections { get; set; } = 1; // 1 = single choice, >1 = multiple choices

    public bool IsRequired => MinSelections > 0;

    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ModifierOption> Options { get; set; } = new List<ModifierOption>();
    public ICollection<MenuItemModifierGroup> MenuItemLinks { get; set; } = new List<MenuItemModifierGroup>();
}
