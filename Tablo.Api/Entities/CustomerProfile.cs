using System;

namespace Tablo.Api.Entities
{
    public class CustomerProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RestaurantId { get; set; }
        public string Phone { get; set; } = string.Empty; // Primary lookup index (e.g. +91 9876543210 or 9876543210)
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Notes { get; set; }
        public string? Tags { get; set; } // Comma-separated: VIP, Regular, Vegan
        public int LoyaltyPointsBalance { get; set; } = 0;
        public decimal TotalSpend { get; set; } = 0m;
        public int TotalVisits { get; set; } = 0;
        public DateTime FirstVisitUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastVisitUtc { get; set; } = DateTime.UtcNow;
        public bool ConsentGiven { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }

        public Restaurant? Restaurant { get; set; }
    }
}
