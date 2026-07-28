using System;
using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities
{
    public class RestaurantOrderSequence
    {
        public Guid RestaurantId { get; set; }
        
        public int NextOrderNumber { get; set; }

        public Restaurant Restaurant { get; set; } = null!;
    }
}
