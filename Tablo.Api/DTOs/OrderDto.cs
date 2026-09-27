using System;
using System.Collections.Generic;

namespace Tablo.Api.DTOs
{
    public class OrderDto
    {
        public Guid Id { get; set; }
        public Guid RestaurantId { get; set; }
        public int OrderNumber { get; set; }
        public int TableNumber { get; set; }
        public Guid TableId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? ExceptionReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
    }
}
