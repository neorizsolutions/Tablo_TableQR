using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities
{
    public class Order
    {
        public Guid Id { get; set; }
        
        public Guid RestaurantId { get; set; }
        public Restaurant Restaurant { get; set; } = null!;
        
        public Guid? TableId { get; set; }
        public RestaurantTable? Table { get; set; }
        
        public Guid? DiningGroupId { get; set; }
        public string? GroupLabel { get; set; }
        public int OrderNumber { get; set; }

        [MaxLength(30)]
        public string OrderChannel { get; set; } = "DineIn"; // DineIn, ParcelTakeaway, CarParking

        [MaxLength(30)]
        public string? OrderSource { get; set; } // Counter, Phone, WhatsApp, WebQr

        public bool IsScheduled { get; set; } = false;
        public DateTime? ScheduledPickupTimeUtc { get; set; }

        [MaxLength(50)]
        public string? ScheduledPickupTimeLocal { get; set; } // e.g. "2026-11-20 14:30" in Asia/Kolkata

        public int? PreparationLeadMinutes { get; set; } = 15;
        public DateTime? ScheduledReleaseTimeUtc { get; set; }

        // Car-side / Parking Fields
        [MaxLength(30)]
        public string? VehiclePlate { get; set; }

        [MaxLength(100)]
        public string? VehicleModel { get; set; }

        [MaxLength(30)]
        public string? ParkingBay { get; set; }

        public DateTime? DeliveredAtUtc { get; set; }
        public Guid? DeliveredByEmployeeId { get; set; }

        public int? DailyTokenNumber { get; set; }

        public Guid? OutletId { get; set; }
        public Outlet? Outlet { get; set; }

        [MaxLength(50)]
        public string? DeliveryProvider { get; set; } // Swiggy, Zomato, Other Delivery

        [MaxLength(100)]
        public string? ExternalOrderReference { get; set; }

        [MaxLength(100)]
        public string? NormalizedExternalReference { get; set; }

        public bool MarketplaceCollected { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MarketplaceEnteredTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MarketplaceEnteredTax { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MarketplaceEnteredDiscount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MarketplaceEnteredPackaging { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MarketplaceFeeSnapshot { get; set; }

        [MaxLength(10)]
        public string? BusinessDate { get; set; } // YYYY-MM-DD in Asia/Kolkata

        [MaxLength(100)]
        public string? CustomerName { get; set; }

        [MaxLength(20)]
        public string? CustomerPhone { get; set; }

        [MaxLength(64)]
        public string? SubmissionOperationId { get; set; }

        [MaxLength(64)]
        public string? SubmissionFingerprint { get; set; }

        [MaxLength(64)]
        public string? TrackingToken { get; set; }

        [MaxLength(50)]
        public string KotPrintStatus { get; set; } = "None"; // None, Queued, Skipped, Failed

        [MaxLength(500)]
        public string? KotPrintError { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PackagingCharge { get; set; } = 0m;

        public Guid? TableBillId { get; set; }
        public TableBill? TableBill { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Placed";
        
        [MaxLength(500)]
        public string? ExceptionReason { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal Tax { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation Properties
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
