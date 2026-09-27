using System;
using System.Threading.Tasks;
using Tablo.Api.DTOs;

namespace Tablo.Api.Interfaces
{
    public interface ISettingsService
    {
        Task<RestaurantSettingsDto> GetSettingsAsync(Guid restaurantId);
        Task<RestaurantSettingsDto> UpdateAcceptingOrdersAsync(Guid restaurantId, bool isAcceptingOrders);
    }
}
