using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities;

public class Plan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty; // basic, plus, premium

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public decimal MonthlyPriceInr { get; set; }

    public int MaxOutlets { get; set; } = 1;

    public int MaxTables { get; set; } = 25;

    public int MaxKitchenStations { get; set; } = 1;

    /// <summary>
    /// JSON array of enabled capability codes
    /// </summary>
    [Required]
    public string FeaturesJson { get; set; } = "[]";

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
