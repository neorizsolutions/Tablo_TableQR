using System;

namespace Tablo.Api.DTOs
{
    public class OrderItemDto
    {
        public Guid Id { get; set; }
        public Guid MenuItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ItemNameSnapshot { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string? SpecialInstructions { get; set; }
        public string ReleaseState { get; set; } = "Fired";
        public string PackingIntent { get; set; } = "DineIn";
        public decimal PackagingFeeApplied { get; set; }
        public bool IsPackagingWaived { get; set; }
        public string? PackagingWaiverReason { get; set; }
        public string? CourseName { get; set; }
        public DateTime? FiredAtUtc { get; set; }
        public string? ModifiersJson { get; set; }
        public bool IsRefill { get; set; }
        public Guid? OriginalOrderItemId { get; set; }
        public int RefillCount { get; set; }
        public int CancelledQuantity { get; set; }
        public bool IsCancelled { get; set; }
    }
}
