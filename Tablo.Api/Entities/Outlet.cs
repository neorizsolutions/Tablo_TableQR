using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class Outlet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    public bool IsCentralKitchen { get; set; } = false;

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Address { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Storefront and Operating Hours Configuration
    public bool IsPublished { get; set; } = true;

    public bool IsOpen { get; set; } = true;

    [MaxLength(50)]
    public string Timezone { get; set; } = "Asia/Kolkata";

    [MaxLength(10)]
    public string OpeningTime { get; set; } = "11:00";

    [MaxLength(10)]
    public string ClosingTime { get; set; } = "23:30";

    public bool IsOvernight { get; set; } = false;

    public int LeadTimeMinutes { get; set; } = 25;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MinOrderAmount { get; set; } = 200m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DeliveryBaseFee { get; set; } = 40m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal FreeDeliveryAboveAmount { get; set; } = 799m;

    [MaxLength(50)]
    public string? Phone { get; set; } = "+91 98765 43210";

    public bool IsTemporarilyClosed { get; set; } = false;

    [MaxLength(255)]
    public string? ClosureReason { get; set; }
}
