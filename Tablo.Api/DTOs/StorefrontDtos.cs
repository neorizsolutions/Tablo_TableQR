using System;
using System.Collections.Generic;

namespace Tablo.Api.DTOs;

public record StorefrontOutletSummaryDto(
    string OutletId,
    string RestaurantId,
    string Name,
    string Address,
    string Phone,
    bool IsOpenNow,
    string OperatingHours,
    int LeadTimeMinutes,
    decimal MinOrderAmount,
    decimal DeliveryBaseFee,
    decimal FreeDeliveryAboveAmount,
    bool IsPublished,
    bool IsTemporarilyClosed,
    string? ClosureReason
);

public record StorefrontOrderItemDto(
    string MenuItemId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    List<StorefrontModifierSelectionDto>? Modifiers,
    string? CookingNotes
);

public record StorefrontModifierSelectionDto(
    string ModifierOptionId,
    string OptionName,
    decimal PriceDelta
);

public record StorefrontCheckoutRequestDto(
    string OutletId,
    string OrderChannel, // DirectDelivery, TakeawayPickup, DineInDirect
    string FulfillmentTiming, // Immediate, Scheduled
    DateTime? ScheduledForUtc,
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail,
    string? DeliveryAddress,
    string? DeliveryNotes,
    List<StorefrontOrderItemDto> Items,
    string PaymentMethod, // UpiOnline, PayOnDelivery, PayAtCounter
    string IdempotencyKey
);

public record StorefrontCheckoutResponseDto(
    string OrderId,
    string OrderNumber,
    string Token,
    string Status, // Placed, Preparing, Ready, Dispatched, Delivered
    decimal Subtotal,
    decimal Tax,
    decimal DeliveryFee,
    decimal PackagingFee,
    decimal TotalPayable,
    string PaymentStatus, // Pending, Paid
    string? UpiUri,
    string? PayeeVpa,
    string? Reference,
    DateTime EstimatedFulfillmentUtc,
    bool IsReplay,
    string TrackingToken
);

public record StorefrontTrackingItemDto(
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);

public record StorefrontOrderTrackingDto(
    string OrderNumber,
    string Token,
    string Status,
    string Channel,
    string OutletName,
    DateTime PlacedAtUtc,
    DateTime EstimatedFulfillmentUtc,
    List<StorefrontTrackingItemDto> Items,
    decimal Subtotal,
    decimal Tax,
    decimal DeliveryFee,
    decimal PackagingFee,
    decimal TotalPayable,
    string PaymentStatus,
    string? UpiUri,
    string? PayeeVpa
);

public record UpdateOutletStorefrontSettingsDto(
    bool? IsPublished,
    bool? IsOpen,
    string? Timezone,
    string? OpeningTime,
    string? ClosingTime,
    bool? IsOvernight,
    int? LeadTimeMinutes,
    decimal? MinOrderAmount,
    decimal? DeliveryBaseFee,
    decimal? FreeDeliveryAboveAmount,
    string? Phone,
    bool? IsTemporarilyClosed,
    string? ClosureReason
);
