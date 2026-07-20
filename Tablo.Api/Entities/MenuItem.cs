using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities
{
    public class MenuItem
    {
        public Guid Id { get; set; }
        
        public Guid RestaurantId { get; set; }
        public Restaurant Restaurant { get; set; } = null!;
        
        public Guid CategoryId { get; set; }
        public Category Category { get; set; } = null!;
        
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
        
        public bool IsAvailable { get; set; }
        
        public bool IsDeleted { get; set; }
        
        [MaxLength(2000)]
        public string? ImageUrl { get; set; }

        // Navigation Properties
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
