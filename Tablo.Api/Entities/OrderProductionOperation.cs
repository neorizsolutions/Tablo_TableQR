using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities;

public class OrderProductionOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string OperationType { get; set; } = string.Empty; // "Fire", "Refill", "Cancellation"

    [Required]
    [MaxLength(64)]
    public string OperationId { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string PayloadHash { get; set; } = string.Empty;

    [Required]
    public string OriginalResponseJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
