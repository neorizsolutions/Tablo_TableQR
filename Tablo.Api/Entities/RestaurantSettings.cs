using System;
using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities
{
    public class RestaurantSettings
    {
        [Key]
        public Guid RestaurantId { get; set; }
        
        public bool IsAcceptingOrders { get; set; } = true;
        
        /// <summary>
        /// Inventory costing method: "WeightedAverage" (default), "FIFO", or "LIFO".
        /// </summary>
        [MaxLength(30)]
        public string CostingMethod { get; set; } = "WeightedAverage";

        /// <summary>
        /// When true, automatically marks dishes as 86'd (unavailable) when ingredient stock is exhausted.
        /// </summary>
        public bool AutoManageAvailability { get; set; } = false;

        /// <summary>
        /// When true, accepted orders generate thermal KOT print jobs.
        /// </summary>
        public bool KotPrintingEnabled { get; set; } = false;

        /// <summary>
        /// Kitchen setup mode: "SingleKitchen", "SeparateFoodBeverage", or "CustomMultiStation".
        /// </summary>
        [MaxLength(50)]
        public string KitchenSetupMode { get; set; } = "SingleKitchen";

        public Guid? DefaultKitchenStationId { get; set; }
        public KitchenStation? DefaultKitchenStation { get; set; }

        /// <summary>
        /// Packaging charge mode: "None" (default), "PerItem", "FlatPerOrder", "Percentage".
        /// </summary>
        [MaxLength(30)]
        public string ParcelPackagingChargeMode { get; set; } = "None";

        public decimal ParcelPackagingFlatRate { get; set; } = 0m;

        public decimal ParcelPackagingPercentage { get; set; } = 0m;

        public bool ParcelPackagingTaxable { get; set; } = false;

        public decimal? ParcelPackagingTaxRate { get; set; } = 0m;

        // Scheduled Pickup Settings
        public bool ScheduledPickupEnabled { get; set; } = false;
        public int DefaultPreparationLeadMinutes { get; set; } = 15;

        // Car-Side Ordering Settings
        public bool CarSideOrderingEnabled { get; set; } = false;

        // Hybrid Dine-In / Parcel Settings
        public bool HybridDineInParcelEnabled { get; set; } = false;
        public decimal LeftoverContainerCharge { get; set; } = 0m;

        // Dynamic UPI QR Settings
        public bool UpiQrEnabled { get; set; } = false;

        [MaxLength(100)]
        public string? UpiVpa { get; set; }

        [MaxLength(100)]
        public string? UpiPayeeName { get; set; }

        [MaxLength(30)]
        public string? UpiMerchantCode { get; set; }
        
        // Navigation Property
        public Restaurant Restaurant { get; set; } = null!;
    }
}
