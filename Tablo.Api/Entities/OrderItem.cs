using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities
{
    public class OrderItem
    {
        public Guid Id { get; set; }
        
        public Guid OrderId { get; set; }
        public Order Order { get; set; } = null!;
        
        public Guid MenuItemId { get; set; }
        public string ItemNameSnapshot { get; set; } = string.Empty;
        public MenuItem MenuItem { get; set; } = null!;
        
        public int Quantity { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }
        
        [MaxLength(500)]
        public string? SpecialInstructions { get; set; }

        [MaxLength(30)]
        public string ReleaseState { get; set; } = "Fired"; // Fired, Held

        [MaxLength(30)]
        public string PackingIntent { get; set; } = "DineIn"; // DineIn, Takeaway

        [Column(TypeName = "decimal(18,2)")]
        public decimal PackagingFeeApplied { get; set; } = 0m;

        public bool IsPackagingWaived { get; set; } = false;

        [MaxLength(200)]
        public string? PackagingWaiverReason { get; set; }

        [MaxLength(100)]
        public string? PackagingWaivedBy { get; set; }

        [MaxLength(50)]
        public string? CourseName { get; set; } // Starters, Mains, Desserts, Beverages

        public DateTime? FiredAtUtc { get; set; }
        public Guid? FiredByEmployeeId { get; set; }

        [MaxLength(64)]
        public string? FireOperationId { get; set; }

        public string? ModifiersJson { get; set; }

        public bool IsRefill { get; set; } = false;
        public Guid? OriginalOrderItemId { get; set; }
        public int RefillCount { get; set; } = 0;

        public int CancelledQuantity { get; set; } = 0;
        public bool IsCancelled { get; set; } = false;
    }
}
