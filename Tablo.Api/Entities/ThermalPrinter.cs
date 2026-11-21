using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities;

public class ThermalPrinter
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
    [MaxLength(50)]
    public string ConnectionType { get; set; } = "NetworkTcp"; // "NetworkTcp" or "LocalAgentQueue"

    [MaxLength(100)]
    public string? IpAddress { get; set; }

    public int Port { get; set; } = 9100;

    [MaxLength(100)]
    public string? LocalQueueName { get; set; } // Identifier used by local printing agent

    public int PaperWidthMm { get; set; } = 80; // 58 or 80 mm

    public int CharactersPerLine { get; set; } = 48; // 32 for 58mm, 42/48 for 80mm

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Unknown"; // Online, Offline, PaperOut, Error, Unknown

    public DateTime? LastSeenAtUtc { get; set; }

    [MaxLength(500)]
    public string? LastStatusMessage { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<KitchenStation> Stations { get; set; } = new List<KitchenStation>();
    public ICollection<PrintJob> PrintJobs { get; set; } = new List<PrintJob>();
}
