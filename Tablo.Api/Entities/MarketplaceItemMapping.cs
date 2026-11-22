using System;

namespace Tablo.Api.Entities
{
    public class MarketplaceItemMapping
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RestaurantId { get; set; }
        public string Provider { get; set; } = "Swiggy";
        public string ExternalItemId { get; set; } = string.Empty;
        public Guid MenuItemId { get; set; }
        public bool IsAvailable { get; set; } = true;
        public decimal? CustomPrice { get; set; }
        public decimal PackagingCharge { get; set; } = 0m;
        public DateTime? LastSyncUtc { get; set; }

        public Restaurant? Restaurant { get; set; }
        public MenuItem? MenuItem { get; set; }
    }
}
