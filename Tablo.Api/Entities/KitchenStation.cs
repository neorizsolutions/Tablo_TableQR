using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities;

public class KitchenStation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty; // e.g. "MAIN", "FOOD", "BEV"

    public bool IsDefault { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public Guid? PrinterId { get; set; }
    public ThermalPrinter? Printer { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<KotTicket> KotTickets { get; set; } = new List<KotTicket>();
}
