using System;

namespace Tablo.Api.Entities
{
    public class LoyaltyLedgerEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RestaurantId { get; set; }
        public Guid CustomerId { get; set; }
        public Guid? BillId { get; set; }
        public Guid? OrderId { get; set; }
        public string TransactionType { get; set; } = "Earn"; // Earn, Redeem, Reversal, Adjustment
        public int Points { get; set; } // Positive for Earn/Adjustment, negative for Redeem, opposite for Reversal
        public int BalanceAfter { get; set; }
        public decimal EligibleSalesAmount { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty; // e.g. EARN-{billId}, REDEEM-{billId}-{uuid}
        public string Description { get; set; } = string.Empty;
        public Guid? ActorEmployeeId { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public CustomerProfile? Customer { get; set; }
        public Restaurant? Restaurant { get; set; }
    }
}
