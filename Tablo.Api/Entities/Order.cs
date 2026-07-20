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
        
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Placed";
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation Properties
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
