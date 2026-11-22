using System;
using System.Collections.Generic;

namespace Tablo.Api.DTOs
{
    public class CustomerProfileDto
    {
        public Guid Id { get; set; }
        public Guid RestaurantId { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Notes { get; set; }
        public List<string> Tags { get; set; } = new();
        public int LoyaltyPointsBalance { get; set; }
        public decimal TotalSpend { get; set; }
        public int TotalVisits { get; set; }
        public DateTime FirstVisitUtc { get; set; }
        public DateTime LastVisitUtc { get; set; }
        public bool ConsentGiven { get; set; }
    }

    public class CreateCustomerRequestDto
    {
        public string Phone { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Notes { get; set; }
        public List<string>? Tags { get; set; }
        public bool ConsentGiven { get; set; } = true;
    }

    public class UpdateCustomerRequestDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }
        public List<string>? Tags { get; set; }
        public bool? ConsentGiven { get; set; }
    }

    public class LoyaltyLedgerEntryDto
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Guid? BillId { get; set; }
        public Guid? OrderId { get; set; }
        public string TransactionType { get; set; } = "Earn";
        public int Points { get; set; }
        public int BalanceAfter { get; set; }
        public decimal EligibleSalesAmount { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    public class LoyaltyRedemptionRequestDto
    {
        public Guid CustomerId { get; set; }
        public Guid BillId { get; set; }
        public int PointsToRedeem { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
    }

    public class CustomerFeedbackDto
    {
        public Guid Id { get; set; }
        public Guid RestaurantId { get; set; }
        public Guid? BillId { get; set; }
        public Guid? OrderId { get; set; }
        public Guid? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? Channel { get; set; }
        public int? TableNumber { get; set; }
        public int OverallRating { get; set; }
        public int FoodRating { get; set; }
        public int ServiceRating { get; set; }
        public int AmbianceRating { get; set; }
        public string? Comments { get; set; }
        public string Status { get; set; } = "New";
        public string? StaffNotes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class SubmitFeedbackRequestDto
    {
        public Guid? BillId { get; set; }
        public Guid? OrderId { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerName { get; set; }
        public string? Channel { get; set; }
        public int? TableNumber { get; set; }
        public int OverallRating { get; set; } = 5;
        public int FoodRating { get; set; } = 5;
        public int ServiceRating { get; set; } = 5;
        public int AmbianceRating { get; set; } = 5;
        public string? Comments { get; set; }
    }
}
