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

        public Guid? KitchenStationId { get; set; }
        public KitchenStation? KitchenStation { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PackagingFee { get; set; } = 0m;

        public bool IsRefillEligible { get; set; } = false;
        public int? RefillMaxCount { get; set; }
        public Guid? RefillKitchenStationId { get; set; }
        public KitchenStation? RefillKitchenStation { get; set; }

        public ICollection<MenuItemModifierGroup> ModifierGroups { get; set; } = new List<MenuItemModifierGroup>();

        // Navigation Properties
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
