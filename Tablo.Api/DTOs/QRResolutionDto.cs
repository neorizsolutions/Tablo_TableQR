using System;
using System.Collections.Generic;

namespace Tablo.Api.DTOs
{
    public class QRResolutionDto
    {
        public Guid RestaurantId { get; set; }
        public string RestaurantName { get; set; } = string.Empty;
        public string RestaurantTagline { get; set; } = string.Empty;
        
        public Guid TableId { get; set; }
        public string TableNameOrNumber { get; set; } = string.Empty;
        
        public bool IsAcceptingOrders { get; set; } = true;
    }
}
