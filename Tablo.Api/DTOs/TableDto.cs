using System;

namespace Tablo.Api.DTOs
{
    public class TableDto
    {
        public Guid Id { get; set; }
        public Guid RestaurantId { get; set; }
        public int TableNumber { get; set; }
        public string QRToken { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
