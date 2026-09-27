using System;
using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities
{
    public class RestaurantSettings
    {
        [Key]
        public Guid RestaurantId { get; set; }
        
        public bool IsAcceptingOrders { get; set; } = true;
        
        // Navigation Property
        public Restaurant Restaurant { get; set; } = null!;
    }
}
