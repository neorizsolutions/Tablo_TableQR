using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities
{
    public class Restaurant
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        [MaxLength(500)]
        public string? Address { get; set; }
        
        [Required]
        [MaxLength(255)]
        [EmailAddress]
        public string ContactEmail { get; set; } = string.Empty;
        
        [MaxLength(50)]
        public string? ContactPhone { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation Properties
        public ICollection<RestaurantTable> Tables { get; set; } = new List<RestaurantTable>();
        public ICollection<Category> Categories { get; set; } = new List<Category>();
        public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}
