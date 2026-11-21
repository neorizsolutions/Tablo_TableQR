using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class MarketplacePayout
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }

    [Required]
    [MaxLength(50)]
    public string DeliveryProvider { get; set; } = string.Empty; // "Swiggy", "Zomato", "Other"

    [Required]
    [MaxLength(100)]
    public string PayoutReference { get; set; } = string.Empty; // Unique per restaurant & provider

    public DateTime PayoutDateUtc { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrossAmount { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal CommissionDeducted { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TcsGstDeducted { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal OtherAdjustments { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetAmount { get; set; } = 0m;

    [MaxLength(4000)]
    public string? LinkedOrderIdsJson { get; set; } // JSON array of order IDs or references covered

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? RecordedByEmployeeId { get; set; }
    public Employee? RecordedByEmployee { get; set; }
}
