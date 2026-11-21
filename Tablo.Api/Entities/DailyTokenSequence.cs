using System;
using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities;

public class DailyTokenSequence
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    [Required]
    [MaxLength(10)]
    public string BusinessDate { get; set; } = string.Empty; // YYYY-MM-DD in Asia/Kolkata

    public int LastTokenNumber { get; set; } = 0;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
