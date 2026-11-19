namespace Tablo.Api.DTOs;

public record IngredientDto(
    Guid Id,
    string Name,
    string BaseUnit,
    decimal? LowStockThreshold,
    decimal CurrentAverageCost,
    bool IsActive,
    decimal OnHandQuantity,
    bool IsLowStock,
    DateTime? LastMovementAtUtc
);

public record CreateIngredientDto(
    string Name,
    string BaseUnit,
    decimal? LowStockThreshold = null,
    decimal? InitialStock = null,
    string? InitialStockUnit = null,
    Guid? OperationId = null,
    decimal? InitialCost = null
);

public record UpdateIngredientDto(
    string Name,
    decimal? LowStockThreshold = null,
    decimal? CurrentAverageCost = null,
    bool? IsActive = null
);

public record RecordStockMovementDto(
    Guid OperationId,
    decimal Quantity,
    string? Unit = null,
    string Reason = "Restock",
    string? Notes = null,
    decimal? TotalCost = null,
    decimal? UnitCost = null
);

public record StockMovementDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    DateTime TimestampUtc,
    decimal QuantityChange,
    string UnitSnapshot,
    string Reason,
    decimal? UnitCostSnapshot,
    decimal? TotalCost,
    string? ReferenceType,
    string? ReferenceId,
    string? Notes,
    string? ActorName,
    Guid? OperationId
);

public record RecipeItemDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    string IngredientBaseUnit,
    decimal UsableQuantity,
    decimal YieldPercent,
    decimal RawQuantityPerServing,
    decimal UnitCost = 0m,
    decimal CostPerServing = 0m
);

public record MenuItemRecipeDto(
    Guid MenuItemId,
    string MenuItemName,
    string CategoryName,
    decimal Price,
    bool IsConfigured,
    decimal TotalCostPerServing,
    decimal GrossProfitPerServing,
    decimal GrossMarginPercent,
    bool HasIncompleteCost,
    List<RecipeItemDto> Items
);

public record SetRecipeItemDto(
    Guid IngredientId,
    decimal UsableQuantity,
    decimal YieldPercent
);

public record SetRecipeDto(
    List<SetRecipeItemDto> Items
);

public record StocktakeItemDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    decimal RecordedStockSnapshot,
    decimal PhysicalCount,
    decimal Variance,
    decimal? StockAtPosting,
    decimal? FinalAdjustment
);

public record StocktakeDto(
    Guid Id,
    string BusinessDateIst,
    DateTime CountedAtUtc,
    DateTime? PostedAtUtc,
    string Status,
    Guid OperationId,
    string? Notes,
    string? ActorName,
    bool HasReconciliationConflict,
    List<StocktakeItemDto> Items
);

public record SaveStocktakeDraftDto(
    List<StocktakeCountEntryDto> Counts,
    string? Notes
);

public record StocktakeCountEntryDto(
    Guid IngredientId,
    decimal PhysicalCount
);

public record PostStocktakeRequestDto(
    Guid OperationId,
    bool ConfirmReconciliation = true,
    List<StocktakeCountEntryDto>? Counts = null,
    string? Notes = null
);

// --- REPORTING DTOS ---

public class StockReportFilter
{
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Guid? IngredientId { get; set; }
    public string? Reason { get; set; }
    public bool? LowStockOnly { get; set; }
}

public record StockReportMovementDto(
    Guid Id,
    DateTime TimestampUtc,
    string TimestampIst,
    string Reason,
    decimal QuantityChange,
    string UnitSnapshot,
    decimal? UnitCostSnapshot,
    decimal? TotalCost,
    string? ReferenceType,
    string? ReferenceId,
    string? ActorName,
    string? Notes
);

public record StockReportRowDto(
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    decimal? LowStockThreshold,
    bool IsLowStock,
    decimal OpeningQuantity,
    decimal ReceiptsQuantity,
    decimal SalesQuantity,
    decimal WasteQuantity,
    decimal AdjustmentsQuantity,
    decimal ClosingQuantity,
    decimal CostPerBaseUnit,
    decimal ClosingStockValue,
    List<StockReportMovementDto> Movements
);

public record StockReportDto(
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalClosingStockValue,
    int TotalIngredientsCount,
    int LowStockCount,
    List<StockReportRowDto> Rows
);

public class SalesGrossProfitFilter
{
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Guid? MenuItemId { get; set; }
    public Guid? CategoryId { get; set; }
}

public record SalesGrossProfitDrillDownDto(
    Guid BillId,
    int TableNumber,
    string? GroupLabel,
    DateTime SettledAtUtc,
    string SettledAtIst,
    int Quantity,
    decimal GrossRevenue,
    decimal AllocatedDiscount,
    decimal NetRevenue,
    decimal AllocatedTax,
    decimal TotalIngredientCost,
    decimal GrossProfit,
    decimal GrossMarginPercent,
    bool IsCostComplete,
    string? IncompleteReason,
    List<BillItemIngredientCostDto> IngredientBreakdown
);

public record BillItemIngredientCostDto(
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    decimal RawQuantityPerPortion,
    decimal TotalRawQuantity,
    decimal UnitCostSnapshot,
    decimal TotalCost
);

public record SalesGrossProfitRowDto(
    Guid MenuItemId,
    string MenuItemName,
    string CategoryName,
    int QuantitySold,
    decimal GrossSales,
    decimal TotalDiscount,
    decimal NetSalesExcludingTax,
    decimal TotalTax,
    decimal TotalCogs,
    decimal GrossProfit,
    decimal GrossMarginPercent,
    bool IsCostComplete,
    string? IncompleteReason,
    List<SalesGrossProfitDrillDownDto> DrillDown
);

public record IncompleteCostingItemDto(
    Guid MenuItemId,
    string MenuItemName,
    int QuantitySold,
    decimal NetSales,
    string Reason
);

public record SalesGrossProfitDto(
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalGrossSales,
    decimal TotalDiscounts,
    decimal TotalNetSalesExcludingTax,
    decimal TotalTax,
    decimal TotalCogs,
    decimal TotalGrossProfit,
    decimal OverallGrossMarginPercent,
    int TotalBillsSettled,
    int TotalPortionsSold,
    int IncompleteCostingCount,
    decimal IncompleteSalesAmount,
    List<IncompleteCostingItemDto> IncompleteCostingItems,
    List<SalesGrossProfitRowDto> Rows
);


// --- 1. SUPPLIERS ---

public record SupplierDto(
    Guid Id,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxId,
    bool IsActive,
    int IngredientsCount,
    int InvoicesCount,
    DateTime CreatedAtUtc
);

public record CreateSupplierDto(
    string Name,
    string? ContactPerson = null,
    string? Email = null,
    string? Phone = null,
    string? Address = null,
    string? TaxId = null
);

public record UpdateSupplierDto(
    string Name,
    string? ContactPerson = null,
    string? Email = null,
    string? Phone = null,
    string? Address = null,
    string? TaxId = null,
    bool? IsActive = null
);

public record SupplierIngredientDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    string PackUnit,
    decimal PackQuantity,
    string PackUnitType,
    decimal ConversionFactor,
    decimal LastPurchasePricePerPack,
    decimal NormalizedPricePerBaseUnit
);

public record SetSupplierIngredientDto(
    Guid SupplierId,
    Guid IngredientId,
    string PackUnit,
    decimal PackQuantity,
    string PackUnitType,
    decimal ConversionFactor,
    decimal LastPurchasePricePerPack
);

public record IngredientPriceComparisonDto(
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    List<SupplierPriceComparisonItemDto> Suppliers
);

public record SupplierPriceComparisonItemDto(
    Guid SupplierId,
    string SupplierName,
    string PackUnit,
    decimal PackQuantity,
    decimal LastPricePerPack,
    decimal NormalizedPricePerBaseUnit,
    DateTime LastPurchasedAtUtc
);

// --- 2. PURCHASE ORDERS & INVOICES ---

public record PurchaseOrderDto(
    Guid Id,
    string OrderNumber,
    Guid SupplierId,
    string SupplierName,
    string Status,
    DateTime OrderDateUtc,
    DateTime? ExpectedDeliveryDateUtc,
    DateTime? ApprovedAtUtc,
    decimal TotalAmount,
    string? Notes,
    List<PurchaseOrderItemDto> Items
);

public record PurchaseOrderItemDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    string PackUnit,
    decimal OrderedPackQuantity,
    decimal ReceivedPackQuantity,
    decimal UnitPricePerPack,
    decimal LineTotal,
    decimal ConversionFactor
);

public record CreatePurchaseOrderDto(
    Guid SupplierId,
    DateTime? ExpectedDeliveryDateUtc,
    string? Notes,
    List<CreatePurchaseOrderItemDto> Items
);

public record CreatePurchaseOrderItemDto(
    Guid IngredientId,
    string PackUnit,
    decimal OrderedPackQuantity,
    decimal UnitPricePerPack,
    decimal ConversionFactor
);

public record ReceiveGoodsDto(
    Guid OperationId,
    Guid SupplierId,
    string InvoiceNumber,
    DateTime InvoiceDate,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal ShippingCharges,
    decimal GrandTotal,
    string? Notes,
    Guid? PurchaseOrderId,
    List<ReceiveGoodsItemDto> Items
);

public record ReceiveGoodsItemDto(
    Guid IngredientId,
    Guid? PurchaseOrderItemId,
    string PackUnit,
    decimal ReceivedPackQuantity,
    decimal ConversionFactor,
    decimal PackPrice,
    decimal LineDiscount,
    decimal LineTax,
    decimal TotalLineCost,
    DateTime? ExpiryDate = null,
    string? LotCode = null
);

public record PurchaseInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    Guid SupplierId,
    string SupplierName,
    Guid? PurchaseOrderId,
    string? PurchaseOrderNumber,
    DateTime InvoiceDate,
    DateTime ReceivedAtUtc,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal ShippingCharges,
    decimal GrandTotal,
    string? Notes,
    List<PurchaseInvoiceItemDto> Items
);

public record PurchaseInvoiceItemDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    string PackUnit,
    decimal ReceivedPackQuantity,
    decimal ConversionFactor,
    decimal BaseQuantity,
    decimal PackPrice,
    decimal LineDiscount,
    decimal LineTax,
    decimal TotalLineCost,
    decimal UnitCostPerBaseUnit,
    Guid? StockLotId,
    string? LotCode,
    decimal ReturnedBaseQuantity
);

// --- 3. PURCHASE RETURNS ---

public record CreatePurchaseReturnDto(
    Guid OperationId,
    Guid PurchaseInvoiceId,
    string CreditNoteReference,
    string Reason,
    string? Notes,
    List<CreatePurchaseReturnItemDto> Items
);

public record CreatePurchaseReturnItemDto(
    Guid PurchaseInvoiceItemId,
    decimal ReturnBaseQuantity,
    string? Reason = null
);

public record PurchaseReturnDto(
    Guid Id,
    string ReturnNumber,
    Guid SupplierId,
    string SupplierName,
    Guid PurchaseInvoiceId,
    string InvoiceNumber,
    string CreditNoteReference,
    string Reason,
    DateTime ReturnDateUtc,
    decimal TotalCreditAmount,
    string? Notes,
    List<PurchaseReturnItemDto> Items
);

public record PurchaseReturnItemDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    decimal ReturnQuantity,
    decimal UnitCostSnapshot,
    decimal TotalCredit,
    string? Reason
);

// --- 4. PREPARATION RECIPES & BATCHES ---

public record PreparationRecipeDto(
    Guid Id,
    Guid OutputIngredientId,
    string OutputIngredientName,
    string OutputBaseUnit,
    decimal ExpectedYieldQuantity,
    string? Notes,
    List<PreparationRecipeInputDto> Inputs
);

public record PreparationRecipeInputDto(
    Guid Id,
    Guid InputIngredientId,
    string InputIngredientName,
    string InputBaseUnit,
    decimal Quantity,
    decimal YieldPercent,
    decimal UnitCost,
    decimal ExtendedCost
);

public record SavePreparationRecipeDto(
    Guid OutputIngredientId,
    decimal ExpectedYieldQuantity,
    string? Notes,
    List<SavePreparationRecipeInputDto> Inputs
);

public record SavePreparationRecipeInputDto(
    Guid InputIngredientId,
    decimal Quantity,
    decimal YieldPercent
);

public record RecordPreparationBatchDto(
    Guid OperationId,
    Guid PreparationRecipeId,
    decimal Multiplier,
    decimal ActualYieldQuantity,
    decimal WasteQuantity = 0m,
    string? WasteReason = null,
    DateTime? ExpiryDate = null,
    string? Notes = null
);

public record PreparationBatchDto(
    Guid Id,
    Guid PreparationRecipeId,
    Guid OutputIngredientId,
    string OutputIngredientName,
    string OutputBaseUnit,
    DateTime PreparedAtUtc,
    decimal Multiplier,
    decimal ExpectedYieldQuantity,
    decimal ActualYieldQuantity,
    decimal WasteQuantity,
    string? WasteReason,
    decimal TotalInputCost,
    decimal CostPerBaseUnit,
    Guid? OutputStockLotId,
    string? LotCode
);

// --- 5. LOTS & EXPIRY ---

public record StockLotDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    string LotCode,
    decimal InitialQuantity,
    decimal AvailableQuantity,
    decimal UnitCost,
    DateTime ReceivedOrProducedDateUtc,
    DateTime? ExpiryDate,
    string Status,
    string? SourceType,
    string? SourceReferenceId,
    bool IsExpiringSoon,
    bool IsExpired
);

public record QuarantineLotDto(
    Guid OperationId,
    string Reason
);

public record WriteOffLotDto(
    Guid OperationId,
    decimal Quantity,
    string Reason,
    string? Notes = null
);

// --- 6. COSTING & SETTINGS ---

public record InventorySettingsDto(
    string CostingMethod,
    bool AutoManageAvailability
);

public record UpdateInventorySettingsDto(
    string CostingMethod,
    bool AutoManageAvailability
);

// --- 7. OUTLETS & TRANSFERS ---

public record OutletDto(
    Guid Id,
    string Name,
    string Code,
    bool IsCentralKitchen,
    bool IsActive,
    string? Address
);

public record CreateOutletDto(
    string Name,
    string Code,
    bool IsCentralKitchen = false,
    string? Address = null
);

public record StockTransferDto(
    Guid Id,
    string TransferNumber,
    Guid SourceOutletId,
    string SourceOutletName,
    Guid DestinationOutletId,
    string DestinationOutletName,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? DispatchedAtUtc,
    DateTime? ReceivedAtUtc,
    string? Notes,
    List<StockTransferItemDto> Items
);

public record StockTransferItemDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    decimal RequestedQuantity,
    decimal DispatchedQuantity,
    decimal ReceivedQuantity,
    decimal DiscrepancyQuantity,
    string? DiscrepancyReason,
    decimal UnitCostSnapshot
);

public record CreateStockTransferDto(
    Guid SourceOutletId,
    Guid DestinationOutletId,
    string? Notes,
    List<CreateStockTransferItemDto> Items
);

public record CreateStockTransferItemDto(
    Guid IngredientId,
    decimal RequestedQuantity
);

public record DispatchStockTransferDto(
    Guid OperationId,
    List<DispatchTransferItemDto> Items
);

public record DispatchTransferItemDto(
    Guid ItemId,
    decimal DispatchedQuantity
);

public record ReceiveStockTransferDto(
    Guid OperationId,
    List<ReceiveTransferItemDto> Items
);

public record ReceiveTransferItemDto(
    Guid ItemId,
    decimal ReceivedQuantity,
    string? DiscrepancyReason = null
);

// --- 8. MENU AVAILABILITY ---

public record MenuItemAvailabilityDto(
    Guid MenuItemId,
    string MenuItemName,
    bool IsConfigured,
    bool IsAvailable,
    int MaxPortionsAvailable,
    List<IngredientAvailabilityDto> LimitingIngredients
);

public record IngredientAvailabilityDto(
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    decimal RequiredPerPortion,
    decimal AvailableStock,
    int PortionsPossible
);
