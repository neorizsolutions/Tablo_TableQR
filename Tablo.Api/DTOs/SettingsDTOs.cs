using System;

namespace Tablo.Api.DTOs
{
    public class RestaurantSettingsDto
    {
        public bool IsAcceptingOrders { get; set; }
    }

    public class UpdateAcceptingOrdersDto
    {
        public bool IsAcceptingOrders { get; set; }
    }
}
