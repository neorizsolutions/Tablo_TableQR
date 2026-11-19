using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities;

public class Stocktake
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid? ActorEmployeeId { get; set; }
    public Employee? ActorEmployee { get; set; }

    [Required]
    [MaxLength(20)]
    public string BusinessDateIst { get; set; } = string.Empty;

    public DateTime CountedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? PostedAtUtc { get; set; }

    /// <summary>
    /// Status: "Draft", "Completed"
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Draft";

    public Guid OperationId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public ICollection<StocktakeItem> Items { get; set; } = new List<StocktakeItem>();
}
