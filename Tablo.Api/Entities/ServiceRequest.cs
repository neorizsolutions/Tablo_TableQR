using System;
using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities
{
    public class ServiceRequest
    {
        public Guid Id { get; set; }
        
        public Guid RestaurantId { get; set; }
        public Restaurant Restaurant { get; set; } = null!;
        
        public Guid? DiningGroupId { get; set; }
        public DiningGroup? DiningGroup { get; set; }
        public Guid TableId { get; set; }
        public RestaurantTable Table { get; set; } = null!;
        
        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty; // "CallWaiter", "RequestBill"
        
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // "Pending", "Acknowledged", "Completed", "Cancelled"
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
