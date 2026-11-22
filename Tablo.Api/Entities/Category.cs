using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities
{
    public class Category
    {
        public Guid Id { get; set; }
        
        public Guid RestaurantId { get; set; }
        public Restaurant Restaurant { get; set; } = null!;
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        public int DisplayOrder { get; set; }

        public Guid? KitchenStationId { get; set; }
        public KitchenStation? KitchenStation { get; set; }

        // Navigation Properties
        public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    }
}
