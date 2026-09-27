using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly ApplicationDbContext _context;

        public SettingsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<RestaurantSettingsDto> GetSettingsAsync(Guid restaurantId)
        {
            var settings = await _context.RestaurantSettings
                .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);

            if (settings == null)
            {
                return new RestaurantSettingsDto { IsAcceptingOrders = true };
            }

            return new RestaurantSettingsDto { IsAcceptingOrders = settings.IsAcceptingOrders };
        }

        public async Task<RestaurantSettingsDto> UpdateAcceptingOrdersAsync(Guid restaurantId, bool isAcceptingOrders)
        {
            var settings = await _context.RestaurantSettings
                .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);

            if (settings == null)
            {
                settings = new RestaurantSettings
                {
                    RestaurantId = restaurantId,
                    IsAcceptingOrders = isAcceptingOrders
                };
                _context.RestaurantSettings.Add(settings);
            }
            else
            {
                settings.IsAcceptingOrders = isAcceptingOrders;
                _context.RestaurantSettings.Update(settings);
            }

            await _context.SaveChangesAsync();

            return new RestaurantSettingsDto { IsAcceptingOrders = settings.IsAcceptingOrders };
        }
    }
}
