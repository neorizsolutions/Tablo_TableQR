using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities
{
    public class Order
    {
        public Guid Id { get; set; }
        
        public Guid RestaurantId { get; set; }
        public Restaurant Restaurant { get; set; } = null!;
        
        public Guid TableId { get; set; }
        public RestaurantTable Table { get; set; } = null!;
        
        public int OrderNumber { get; set; }

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
