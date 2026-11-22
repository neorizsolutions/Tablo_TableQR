namespace Tablo.Api.Constants;

public static class Capabilities
{
    // Basic Features
    public const string QrOrdering = "qr_ordering";
    public const string KitchenDisplay = "kitchen_display";
    public const string StandardBilling = "standard_billing";
    public const string BasicReports = "basic_reports";
    public const string MenuManagement = "menu_management";
    public const string TablesAndQr = "tables_qr";

    // Plus Features
    public const string StaffOrdering = "staff_ordering";
    public const string DiningGroups = "dining_groups";
    public const string SplitBilling = "split_billing";
    public const string MixedPayments = "mixed_payments";
    public const string TableTransfers = "table_transfers";
    public const string AdvancedPaymentReports = "advanced_payment_reports";
    public const string ThermalPrinting = "thermal_printing";
    public const string MultipleKitchenStations = "multiple_stations";
    public const string ParcelCounter = "parcel_counter";
    public const string HoldFireCourses = "hold_fire_courses";
    public const string ItemModifiers = "item_modifiers";
    public const string FreeRefills = "free_refills";
    public const string ManagerWasteCancellation = "waste_cancellation";
    public const string ScheduledPickup = "scheduled_pickup";
    public const string HybridDineInParcel = "hybrid_dinein_parcel";
    public const string CarSideOrdering = "carside_ordering";
    public const string DynamicUpiQr = "dynamic_upi_qr";

    // Premium Features
    public const string InventoryCore = "inventory_core";
    public const string RecipesCosting = "recipes_costing";
    public const string Stocktakes = "stocktakes";
    public const string Purchasing = "purchasing";
    public const string LotsAndExpiry = "lots_expiry";
    public const string Preparations = "preparations";
    public const string OutletTransfers = "outlet_transfers";
    public const string StockProfitReports = "stock_profit_reports";
    public const string AutoAvailability = "auto_availability";

    public static readonly string[] BasicDefaults = new[]
    {
        QrOrdering,
        KitchenDisplay,
        StandardBilling,
        BasicReports,
        MenuManagement,
        TablesAndQr
    };

    public static readonly string[] PlusDefaults = BasicDefaults.Concat(new[]
    {
        StaffOrdering,
        DiningGroups,
        SplitBilling,
        MixedPayments,
        TableTransfers,
        AdvancedPaymentReports,
        ThermalPrinting,
        ParcelCounter
    }).ToArray();

    public static readonly string[] PremiumDefaults = PlusDefaults.Concat(new[]
    {
        MultipleKitchenStations,
        InventoryCore,
        RecipesCosting,
        Stocktakes,
        Purchasing,
        LotsAndExpiry,
        Preparations,
        OutletTransfers,
        StockProfitReports,
        AutoAvailability
    }).ToArray();

    /// <summary>
    /// Dependencies: Map of feature -> required prerequisite features
    /// </summary>
    public static readonly Dictionary<string, string[]> Dependencies = new()
    {
        { StaffOrdering, new[] { StandardBilling } },
        { DiningGroups, new[] { StandardBilling } },
        { SplitBilling, new[] { DiningGroups, StandardBilling } },
        { MixedPayments, new[] { StandardBilling } },
        { TableTransfers, new[] { DiningGroups } },
        { AdvancedPaymentReports, new[] { StandardBilling } },
        { ThermalPrinting, new[] { KitchenDisplay } },
        { MultipleKitchenStations, new[] { ThermalPrinting } },
        { ParcelCounter, new[] { StandardBilling } },
        { HoldFireCourses, new[] { KitchenDisplay } },
        { ItemModifiers, new[] { MenuManagement } },
        { FreeRefills, new[] { StandardBilling } },
        { ManagerWasteCancellation, new[] { StandardBilling } },
        { ScheduledPickup, new[] { ParcelCounter } },
        { HybridDineInParcel, new[] { ParcelCounter } },
        { CarSideOrdering, new[] { StandardBilling } },
        { DynamicUpiQr, new[] { StandardBilling } },
        { RecipesCosting, new[] { InventoryCore } },
        { Stocktakes, new[] { InventoryCore } },
        { Purchasing, new[] { InventoryCore } },
        { LotsAndExpiry, new[] { InventoryCore } },
        { Preparations, new[] { InventoryCore, RecipesCosting } },
        { OutletTransfers, new[] { InventoryCore } },
        { StockProfitReports, new[] { InventoryCore, RecipesCosting } },
        { AutoAvailability, new[] { InventoryCore } }
    };

    public static (bool isValid, string? missingDependency) ValidateDependencies(IEnumerable<string> enabledFeatures)
    {
        var set = new HashSet<string>(enabledFeatures, StringComparer.OrdinalIgnoreCase);
        foreach (var feat in set)
        {
            if (Dependencies.TryGetValue(feat, out var reqs))
            {
                foreach (var req in reqs)
                {
                    if (!set.Contains(req))
                    {
                        return (false, $"Feature '{feat}' requires '{req}' which is not enabled.");
                    }
                }
            }
        }
        return (true, null);
    }
}
