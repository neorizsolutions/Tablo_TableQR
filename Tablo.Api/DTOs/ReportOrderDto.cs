using System;
using System.Collections.Generic;

namespace Tablo.Api.DTOs
{
    public class ReportOrderDto
    {
        public Guid OrderId { get; set; }
        public int OrderNumber { get; set; }
        public string DisplayOrderNumber => $"ORD-{OrderNumber:D4}";
        public Guid TableId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public IEnumerable<ReportOrderItemDto> Items { get; set; } = new List<ReportOrderItemDto>();
    }
}
