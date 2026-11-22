using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs;

public class CreateParcelOrderRequestDto
{
    [Required]
    public Guid OperationId { get; set; }

    [MaxLength(100)]
    public string? CustomerName { get; set; }

    [MaxLength(20)]
    public string? CustomerPhone { get; set; }

    [MaxLength(30)]
    public string OrderChannel { get; set; } = "ParcelTakeaway"; // ParcelTakeaway, CarParking

    [MaxLength(30)]
    public string OrderSource { get; set; } = "Counter"; // Counter, Phone, WhatsApp

    public bool IsScheduled { get; set; } = false;

    [MaxLength(50)]
    public string? ScheduledPickupTimeLocal { get; set; } // YYYY-MM-DD HH:mm in Asia/Kolkata

    public int? PreparationLeadMinutes { get; set; } = 15;

    // Car-Side Fields
    [MaxLength(30)]
    public string? VehiclePlate { get; set; }

    [MaxLength(100)]
    public string? VehicleModel { get; set; }

    [MaxLength(30)]
    public string? ParkingBay { get; set; }

    // Manual Marketplace Delivery Fields
    public Guid? OutletId { get; set; }

    [MaxLength(50)]
    public string? DeliveryProvider { get; set; } // "Swiggy", "Zomato", or custom e.g. "Dunzo"

    [MaxLength(100)]
    public string? ExternalOrderReference { get; set; } // Required for marketplace delivery

    public bool MarketplaceCollected { get; set; } = false;

    public decimal? MarketplaceEnteredTotal { get; set; }
    public decimal? MarketplaceEnteredTax { get; set; }
    public decimal? MarketplaceEnteredDiscount { get; set; }
    public decimal? MarketplaceEnteredPackaging { get; set; }
    public decimal? MarketplaceFeeSnapshot { get; set; }

    public bool WaivePackaging { get; set; } = false;

    [MaxLength(200)]
    public string? PackagingWaiverReason { get; set; }

    [Required, MinLength(1), MaxLength(200)]
    public List<OrderItemRequestDto> Items { get; set; } = new();

    public string? Notes { get; set; }
}

public class ParcelOrderResponseDto
{
    public Guid OrderId { get; set; }
    public Guid BillId { get; set; }
    public int OrderNumber { get; set; }
    public int DailyTokenNumber { get; set; }
    public string BusinessDate { get; set; } = string.Empty;
    public string TokenFormatted => $"#{DailyTokenNumber:D3}";
    public string OrderChannel { get; set; } = "ParcelTakeaway";
    public string Status { get; set; } = "Accepted";
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public decimal Subtotal { get; set; }
    public decimal PackagingCharge { get; set; }
    public string? PackagingChargeMode { get; set; }
    public decimal PackagingRate { get; set; }
    public bool IsPackagingWaived { get; set; }
    public string? PackagingWaiverReason { get; set; }
    public decimal Tax { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedAtIstFormatted { get; set; } = string.Empty;
    public string KotPrintStatus { get; set; } = "None";
    public string? KotPrintError { get; set; }

    // Scheduled Pickup fields
    public string? OrderSource { get; set; }
    public bool IsScheduled { get; set; }
    public DateTime? ScheduledPickupTimeUtc { get; set; }
    public string? ScheduledPickupTimeLocal { get; set; }
    public DateTime? ScheduledReleaseTimeUtc { get; set; }
    public int? PreparationLeadMinutes { get; set; }

    // Car-Side fields
    public string? VehiclePlate { get; set; }
    public string? VehicleModel { get; set; }
    public string? ParkingBay { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public Guid? DeliveredByEmployeeId { get; set; }

    // Marketplace Delivery fields
    public Guid? OutletId { get; set; }
    public string? DeliveryProvider { get; set; }
    public string? ExternalOrderReference { get; set; }
    public bool MarketplaceCollected { get; set; }
    public decimal? MarketplaceEnteredTotal { get; set; }
    public decimal? MarketplaceEnteredTax { get; set; }
    public decimal? MarketplaceEnteredDiscount { get; set; }
    public decimal? MarketplaceEnteredPackaging { get; set; }
    public decimal? MarketplaceDiscrepancy => MarketplaceEnteredTotal.HasValue ? Math.Round(MarketplaceEnteredTotal.Value - TotalAmount, 2) : null;

    public List<OrderItemDto> Items { get; set; } = new();
    public BillDto Bill { get; set; } = null!;
}

public class SettleParcelBillRequestDto
{
    [Required]
    public Guid OperationId { get; set; }

    [Required, StringLength(64, MinimumLength = 64)]
    public string ExpectedRevision { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal ExpectedTotal { get; set; }

    [Required, RegularExpression("^(Cash|UPI|Card|MarketplaceCollected|Marketplace)$")]
    public string PaymentMethod { get; set; } = string.Empty;
}

public class CancelParcelOrderRequestDto
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}

public class ParcelSearchFilterDto
{
    public string? BusinessDate { get; set; } // YYYY-MM-DD (defaults to today IST)
    public int? DailyTokenNumber { get; set; }
    public string? CustomerSearch { get; set; }
    public string? Status { get; set; }
    public string? Channel { get; set; } // "ParcelTakeaway", "CarParking", "Delivery", "Swiggy", "Zomato"
    public string? ExternalOrderReference { get; set; }
    public string? DeliveryProvider { get; set; }
    public int Limit { get; set; } = 50;
}

public class ParcelSettingsDto
{
    public string ChargeMode { get; set; } = "None"; // None, PerItem, FlatPerOrder, Percentage
    public decimal FlatRate { get; set; } = 0m;
    public decimal Percentage { get; set; } = 0m;
    public bool IsTaxable { get; set; } = false;
    public decimal? TaxRate { get; set; } = 0m;
}

public class UpdateParcelSettingsRequestDto
{
    [Required, RegularExpression("^(None|PerItem|FlatPerOrder|Percentage)$")]
    public string ChargeMode { get; set; } = "None";

    [Range(typeof(decimal), "0", "999999.99")]
    public decimal FlatRate { get; set; } = 0m;

    [Range(typeof(decimal), "0", "100.00")]
    public decimal Percentage { get; set; } = 0m;

    public bool IsTaxable { get; set; } = false;

    [Range(typeof(decimal), "0", "100.00")]
    public decimal? TaxRate { get; set; } = 0m;
}

public class RescheduleParcelOrderRequestDto
{
    [Required]
    public Guid OperationId { get; set; }

    [Required, MaxLength(50)]
    public string NewPickupTimeLocal { get; set; } = string.Empty; // YYYY-MM-DD HH:mm in Asia/Kolkata

    public int? PreparationLeadMinutes { get; set; }
}

public class ReleaseScheduledOrderRequestDto
{
    [Required]
    public Guid OperationId { get; set; }
}

public class DeliverCarOrderRequestDto
{
    [Required]
    public Guid OperationId { get; set; }
}

public class AddLeftoverPackingRequestDto
{
    [Required]
    public Guid OperationId { get; set; }

    [Range(typeof(decimal), "0.01", "9999.99")]
    public decimal ContainerCharge { get; set; }

    public int ContainerCount { get; set; } = 1;

    [MaxLength(200)]
    public string? Notes { get; set; }
}

public class UpiQrResponseDto
{
    public Guid BillId { get; set; }
    public decimal RemainingBalance { get; set; }
    public decimal Amount { get; set; }
    public string UpiUri { get; set; } = string.Empty;
    public string PayeeVpa { get; set; } = string.Empty;
    public string PayeeName { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string? Note { get; set; }
    public bool IsSettled { get; set; }
}

public class UpiCallbackDto
{
    public string? TransactionId { get; set; }
    public string? MerchantId { get; set; }
    public string? BillReference { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; } = "INR";
    public string? Status { get; set; } // SUCCESS, FAILURE
    public string? Signature { get; set; }
}

public class RecordMarketplacePayoutDto
{
    [Required]
    public Guid OperationId { get; set; }

    [Required, MaxLength(50)]
    public string DeliveryProvider { get; set; } = "Swiggy"; // "Swiggy", "Zomato", "Other"

    [Required, MaxLength(100)]
    public string PayoutReference { get; set; } = string.Empty; // e.g. "SWIG-PAY-20261120-001"

    public DateTime? PayoutDateUtc { get; set; }

    [Range(0, 999999999)]
    public decimal GrossAmount { get; set; } // Expected aggregate order gross

    [Range(0, 999999999)]
    public decimal CommissionDeducted { get; set; }

    [Range(0, 999999999)]
    public decimal TcsGstDeducted { get; set; }

    [Range(-999999999, 999999999)]
    public decimal OtherAdjustments { get; set; }

    [Range(0, 999999999)]
    public decimal NetAmount { get; set; } // Actual net received in bank

    public List<Guid>? LinkedOrderIds { get; set; } = new();

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class MarketplacePayoutDto
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public string DeliveryProvider { get; set; } = string.Empty;
    public string PayoutReference { get; set; } = string.Empty;
    public DateTime PayoutDateUtc { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal CommissionDeducted { get; set; }
    public decimal TcsGstDeducted { get; set; }
    public decimal OtherAdjustments { get; set; }
    public decimal NetAmount { get; set; }
    public decimal ExpectedNetAmount => Math.Round(GrossAmount - CommissionDeducted - TcsGstDeducted - OtherAdjustments, 2);
    public decimal ReconciliationDiscrepancy => Math.Round(NetAmount - ExpectedNetAmount, 2);
    public List<string> LinkedOrderIds { get; set; } = new();
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? RecordedByEmployeeId { get; set; }
}

