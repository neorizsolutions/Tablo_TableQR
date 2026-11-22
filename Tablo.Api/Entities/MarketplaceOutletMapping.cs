using System;

namespace Tablo.Api.Entities
{
    public class MarketplaceOutletMapping
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RestaurantId { get; set; }
        public string Provider { get; set; } = "Swiggy"; // Swiggy, Zomato
        public string ExternalStoreId { get; set; } = string.Empty;
        public string StoreStatus { get; set; } = "Online"; // Online, Paused
        public bool AutoAccept { get; set; } = true;
        public string? WebhookSecret { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? LastSyncUtc { get; set; }

        public Restaurant? Restaurant { get; set; }
    }
}
