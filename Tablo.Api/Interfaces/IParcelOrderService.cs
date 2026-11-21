using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tablo.Api.DTOs;

namespace Tablo.Api.Interfaces;

public interface IParcelOrderService
{
    Task<ParcelOrderResponseDto> CreateParcelOrderAsync(Guid restaurantId, Guid actorId, string actorEmail, CreateParcelOrderRequestDto request);
    Task<ParcelOrderResponseDto> CancelParcelOrderAsync(Guid restaurantId, Guid orderId, Guid actorId, string reason);
    Task<ParcelOrderResponseDto> ReleaseScheduledOrderAsync(Guid restaurantId, Guid orderId, Guid actorId, string actorEmail);
    Task<ParcelOrderResponseDto> RescheduleParcelOrderAsync(Guid restaurantId, Guid orderId, Guid actorId, RescheduleParcelOrderRequestDto request);
    Task<ParcelOrderResponseDto> DeliverCarOrderAsync(Guid restaurantId, Guid orderId, Guid actorId, DeliverCarOrderRequestDto request);
    Task<BillDto> AddLeftoverPackingChargeAsync(Guid restaurantId, Guid billId, Guid actorId, AddLeftoverPackingRequestDto request);
    Task<UpiQrResponseDto> GenerateBillUpiQrAsync(Guid restaurantId, Guid billId, decimal? requestedAmount);
    Task<BillDto> SettleParcelBillAsync(Guid restaurantId, Guid orderId, Guid actorId, SettleParcelBillRequestDto request);
    Task<List<ParcelOrderResponseDto>> SearchParcelOrdersAsync(Guid restaurantId, ParcelSearchFilterDto filter);
    Task<ParcelOrderResponseDto?> GetParcelOrderByIdAsync(Guid restaurantId, Guid orderId);
    Task<PrintJobDto> PrintBillReceiptAsync(Guid restaurantId, Guid orderId, Guid actorId, string actorEmail, string? reprintReason = null);
    Task<List<PrintJobDto>> RetryParcelKotAsync(Guid restaurantId, Guid orderId, Guid actorId, string actorEmail);
    Task<ParcelSettingsDto> GetParcelSettingsAsync(Guid restaurantId);
    Task<ParcelSettingsDto> UpdateParcelSettingsAsync(Guid restaurantId, UpdateParcelSettingsRequestDto dto);
    Task<MarketplacePayoutDto> RecordMarketplacePayoutAsync(Guid restaurantId, Guid actorId, RecordMarketplacePayoutDto request);
    Task<List<MarketplacePayoutDto>> GetMarketplacePayoutsAsync(Guid restaurantId, string? deliveryProvider = null);
}
