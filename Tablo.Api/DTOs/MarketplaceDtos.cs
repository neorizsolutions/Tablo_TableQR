using System;
using System.Collections.Generic;

namespace Tablo.Api.DTOs
{
    public class MarketplaceOutletMappingDto
    {
        public Guid Id { get; set; }
        public Guid RestaurantId { get; set; }
        public string Provider { get; set; } = "Swiggy";
        public string ExternalStoreId { get; set; } = string.Empty;
        public string StoreStatus { get; set; } = "Online";
        public bool AutoAccept { get; set; }
        public DateTime? LastSyncUtc { get; set; }
    }

    public class MarketplaceItemMappingDto
    {
        public Guid Id { get; set; }
        public Guid RestaurantId { get; set; }
        public string Provider { get; set; } = "Swiggy";
        public string ExternalItemId { get; set; } = string.Empty;
        public Guid MenuItemId { get; set; }
        public string MenuItemName { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public decimal? CustomPrice { get; set; }
        public decimal PackagingCharge { get; set; }
    }

    public class MarketplaceInboundWebhookDto
    {
        public string EventId { get; set; } = string.Empty;
        public string Provider { get; set; } = "Swiggy";
        public string EventType { get; set; } = "ORDER_PLACED";
        public string ExternalOrderId { get; set; } = string.Empty;
        public string ExternalStoreId { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? DeliveryAddress { get; set; }
        public List<MarketplaceOrderItemDto> Items { get; set; } = new();
    }

    public class MarketplaceOrderItemDto
    {
        public string ExternalItemId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string? CookingNotes { get; set; }
    }

    public class MarketplaceWebhookResultDto
    {
        public string Status { get; set; } = "Processed"; // Processed, Duplicate, Failed
        public string Message { get; set; } = string.Empty;
        public Guid? OrderId { get; set; }
        public int? OrderNumber { get; set; }
        public int? TokenNumber { get; set; }
        public List<Guid> GeneratedKotTicketIds { get; set; } = new();
    }

    public class MarketplaceOutboundUpdateDto
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string Provider { get; set; } = "Swiggy";
        public string ExternalOrderId { get; set; } = string.Empty;
        public string TargetStatus { get; set; } = "CONFIRMED";
        public int Attempts { get; set; }
        public string Status { get; set; } = "Pending";
        public string? LastError { get; set; }
        public DateTime? LastAttemptUtc { get; set; }
    }
}
