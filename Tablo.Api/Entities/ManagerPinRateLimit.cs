using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities;

public class ManagerPinRateLimit
{
    [Key]
    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public int FailedAttempts { get; set; } = 0;
    public DateTime? LockedUntilUtc { get; set; }
    public DateTime LastAttemptAtUtc { get; set; } = DateTime.UtcNow;
}
