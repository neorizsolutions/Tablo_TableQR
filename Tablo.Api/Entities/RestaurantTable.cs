using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities
{
    public class RestaurantTable
    {
        public Guid Id { get; set; }
        
        public Guid RestaurantId { get; set; }
        public Restaurant Restaurant { get; set; } = null!;
        
        [Required]
        [MaxLength(100)]
        public string NameOrNumber { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(255)]
        public string QRToken { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        // Navigation Properties
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
