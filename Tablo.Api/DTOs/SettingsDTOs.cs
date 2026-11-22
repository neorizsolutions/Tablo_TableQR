using System;

namespace Tablo.Api.DTOs
{
    public class RestaurantSettingsDto
    {
        public bool IsAcceptingOrders { get; set; }
        public string CostingMethod { get; set; } = "WeightedAverage";
        public bool AutoManageAvailability { get; set; }
        public bool KotPrintingEnabled { get; set; }
        public string KitchenSetupMode { get; set; } = "SingleKitchen";
        public Guid? DefaultKitchenStationId { get; set; }
        public string? DefaultKitchenStationName { get; set; }

        // Scheduled Pickup
        public bool ScheduledPickupEnabled { get; set; }
        public int DefaultPreparationLeadMinutes { get; set; } = 15;

        // Car-Side
        public bool CarSideOrderingEnabled { get; set; }

        // Hybrid Dine-In / Parcel
        public bool HybridDineInParcelEnabled { get; set; }
        public decimal LeftoverContainerCharge { get; set; }

        // Dynamic UPI QR
        public bool UpiQrEnabled { get; set; }
        public string? UpiVpa { get; set; }
        public string? UpiPayeeName { get; set; }
        public string? UpiMerchantCode { get; set; }
    }

    public class UpdateAcceptingOrdersDto
    {
        public bool IsAcceptingOrders { get; set; }
    }

    public class UpdateKitchenPrintingSettingsDto
    {
        public bool KotPrintingEnabled { get; set; }
        public string KitchenSetupMode { get; set; } = "SingleKitchen";
        public Guid? DefaultKitchenStationId { get; set; }
    }

    public class UpdateUpiQrSettingsDto
    {
        public bool UpiQrEnabled { get; set; }
        public string? UpiVpa { get; set; }
        public string? UpiPayeeName { get; set; }
        public string? UpiMerchantCode { get; set; }
    }

    public class UpdateScheduledPickupSettingsDto
    {
        public bool ScheduledPickupEnabled { get; set; }
        public int DefaultPreparationLeadMinutes { get; set; } = 15;
    }

    public class UpdateCarSideSettingsDto
    {
        public bool CarSideOrderingEnabled { get; set; }
    }

    public class UpdateHybridPackagingSettingsDto
    {
        public bool HybridDineInParcelEnabled { get; set; }
        public decimal LeftoverContainerCharge { get; set; }
    }
}
