using System;
using System.Threading.Tasks;
using Tablo.Api.DTOs;

namespace Tablo.Api.Interfaces
{
    public interface ISettingsService
    {
        Task<RestaurantSettingsDto> GetSettingsAsync(Guid restaurantId);
        Task<RestaurantSettingsDto> UpdateAcceptingOrdersAsync(Guid restaurantId, bool isAcceptingOrders);
        Task<RestaurantSettingsDto> UpdateKitchenPrintingSettingsAsync(Guid restaurantId, UpdateKitchenPrintingSettingsDto request);
        Task<RestaurantSettingsDto> UpdateUpiQrSettingsAsync(Guid restaurantId, UpdateUpiQrSettingsDto request);
        Task<RestaurantSettingsDto> UpdateScheduledPickupSettingsAsync(Guid restaurantId, UpdateScheduledPickupSettingsDto request);
        Task<RestaurantSettingsDto> UpdateCarSideSettingsAsync(Guid restaurantId, UpdateCarSideSettingsDto request);
        Task<RestaurantSettingsDto> UpdateHybridPackagingSettingsAsync(Guid restaurantId, UpdateHybridPackagingSettingsDto request);
    }
}
