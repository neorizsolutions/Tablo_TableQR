namespace Tablo.Api.DTOs;

public record CentralProductionBatchDto(
    string Id,
    string RestaurantId,
    string BatchCode,
    string RecipeName,
    decimal TargetQuantity,
    string Unit,
    decimal UnitProductionCost,
    decimal TotalCost,
    string Status, // Draft, InProduction, Completed
    DateTime CreatedAtUtc
);

public record OutletStockTransferRequestDto(
    string SourceKitchenId,
    string DestinationOutletId,
    string IngredientId,
    string ItemName,
    decimal QuantityDispatched,
    string Unit,
    decimal UnitCost
);

public record OutletStockTransferReceiptDto(
    string TransferId,
    decimal QuantityReceived,
    string? DiscrepancyReason
);

public record OutletStockTransferResultDto(
    string TransferId,
    string TransferCode,
    string SourceKitchenId,
    string DestinationOutletId,
    string ItemName,
    decimal QuantityDispatched,
    decimal QuantityReceived,
    decimal ShortageQuantity,
    decimal DispatchedCost,
    decimal ReceivedCost,
    decimal VarianceLossCost,
    string Status, // Dispatched, PartiallyReceived, FullyReceived
    string? DiscrepancyReason,
    DateTime DispatchedAtUtc,
    DateTime? ReceivedAtUtc
);

public record MenuPublishItemOverrideDto(
    string MenuItemId,
    string ItemName,
    decimal CentralPrice,
    decimal? OutletPriceOverride,
    bool? IsAvailableOverride
);

public record MenuPublishPreviewDto(
    string CentralRestaurantId,
    List<string> TargetOutletIds,
    int ItemsToAddCount,
    int ItemsToUpdateCount,
    List<MenuPublishItemOverrideDto> OverridesPreview
);

public record ExecuteMenuPublishRequestDto(
    string CentralRestaurantId,
    List<string> TargetOutletIds,
    List<MenuPublishItemOverrideDto> ItemOverrides,
    bool ForceAvailabilitySync
);

public record MenuPublishResultDto(
    string PublishJobId,
    DateTime PublishedAtUtc,
    int OutletsUpdatedCount,
    int ItemsUpdatedCount,
    bool Success
);
