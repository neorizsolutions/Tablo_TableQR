using System;

namespace Tablo.Api.DTOs
{
    public class ReportOrderItemDto
    {
        public string ItemNameSnapshot { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => Quantity * UnitPrice;
    }
}
