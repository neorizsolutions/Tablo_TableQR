using System;

namespace Tablo.Api.Entities
{
    public class MarketplaceOutboundUpdate
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RestaurantId { get; set; }
        public Guid OrderId { get; set; }
        public string Provider { get; set; } = "Swiggy";
        public string ExternalOrderId { get; set; } = string.Empty;
        public string TargetStatus { get; set; } = "CONFIRMED"; // CONFIRMED, PREPARING, READY_FOR_PICKUP, DISPATCHED
        public int Attempts { get; set; } = 0;
        public int MaxAttempts { get; set; } = 5;
        public DateTime? LastAttemptUtc { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Success, Failed
        public string? LastError { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public Restaurant? Restaurant { get; set; }
        public Order? Order { get; set; }
    }
}
