using System;

namespace Tablo.Api.Entities
{
    public class CustomerFeedback
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RestaurantId { get; set; }
        public Guid? BillId { get; set; }
        public Guid? OrderId { get; set; }
        public Guid? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? Channel { get; set; } // DineIn, Parcel, Delivery
        public int? TableNumber { get; set; }
        public int OverallRating { get; set; } = 5; // 1 to 5
        public int FoodRating { get; set; } = 5; // 1 to 5
        public int ServiceRating { get; set; } = 5; // 1 to 5
        public int AmbianceRating { get; set; } = 5; // 1 to 5
        public string? Comments { get; set; }
        public string Status { get; set; } = "New"; // New, Reviewed, Resolved
        public string? StaffNotes { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public Restaurant? Restaurant { get; set; }
        public CustomerProfile? Customer { get; set; }
    }
}
