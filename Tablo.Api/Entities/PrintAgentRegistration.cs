using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities;

public class PrintAgentRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }

    [Required]
    [MaxLength(100)]
    public string AgentName { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string ApiKeyHash { get; set; } = string.Empty; // SHA-256 hash of agent token

    [MaxLength(100)]
    public string KeyPrefix { get; set; } = string.Empty; // First 8 chars for identification in UI

    public bool IsActive { get; set; } = true;

    public DateTime? LastHeartbeatUtc { get; set; }

    [MaxLength(50)]
    public string? Version { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAtUtc { get; set; }
}
