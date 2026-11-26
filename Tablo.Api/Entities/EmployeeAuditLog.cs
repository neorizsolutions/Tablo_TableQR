using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities;

/// <summary>
/// Immutable tenant-scoped audit trail for employee lifecycle changes such as soft deletion.
/// </summary>
public class EmployeeAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid TargetEmployeeId { get; set; }
    public Employee TargetEmployee { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string TargetEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string TargetName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string TargetRole { get; set; } = string.Empty;

    public Guid? ActorEmployeeId { get; set; }
    public Employee? ActorEmployee { get; set; }

    [Required]
    [MaxLength(255)]
    public string ActorEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Reason { get; set; }

    public string? DetailsJson { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
