using System;
using System.Collections.Generic;

namespace Tablo.Api.DTOs
{
    public class OrderDto
    {
        public Guid Id { get; set; }
        public int OrderNumber { get; set; }
        public int TableNumber { get; set; }
        public Guid TableId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
    }
}
