using System;
using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities
{
    public class RestaurantSettings
    {
        [Key]
        public Guid RestaurantId { get; set; }
        
        public bool IsAcceptingOrders { get; set; } = true;
        
        /// <summary>
        /// Inventory costing method: "WeightedAverage" (default), "FIFO", or "LIFO".
        /// </summary>
        [MaxLength(30)]
        public string CostingMethod { get; set; } = "WeightedAverage";

        /// <summary>
        /// When true, automatically marks dishes as 86'd (unavailable) when ingredient stock is exhausted.
        /// </summary>
        public bool AutoManageAvailability { get; set; } = false;
        
        // Navigation Property
        public Restaurant Restaurant { get; set; } = null!;
    }
}
