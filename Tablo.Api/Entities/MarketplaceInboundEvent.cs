using System;

namespace Tablo.Api.Entities
{
    public class MarketplaceInboundEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RestaurantId { get; set; }
        public string Provider { get; set; } = "Swiggy";
        public string EventId { get; set; } = string.Empty; // Unique external event ID
        public string? ExternalOrderId { get; set; }
        public string EventType { get; set; } = "ORDER_PLACED"; // ORDER_PLACED, ORDER_CANCELLED, STORE_STATUS
        public string PayloadJson { get; set; } = string.Empty;
        public string Status { get; set; } = "Processed"; // Processed, Duplicate, Failed
        public string? ErrorMessage { get; set; }
        public Guid? LinkedOrderId { get; set; }
        public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAtUtc { get; set; }

        public Restaurant? Restaurant { get; set; }
        public Order? LinkedOrder { get; set; }
    }
}
