using System;
using System.Threading.Tasks;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services
{
    public class QRService : IQRService
    {
        private readonly ITableRepository _tableRepository;

        public QRService(ITableRepository tableRepository)
        {
            _tableRepository = tableRepository;
        }

        public async Task<QRResolutionDto?> ResolveQRTokenAsync(string qrToken)
        {
            var table = await _tableRepository.GetTableByQRTokenAsync(qrToken);
            if (table == null || table.Restaurant == null)
            {
                return null;
            }

            return new QRResolutionDto
            {
                RestaurantId = table.RestaurantId,
                RestaurantName = table.Restaurant.Name,
                RestaurantTagline = table.Restaurant.Description ?? string.Empty,
                TableId = table.Id,
                TableNameOrNumber = $"Table {table.TableNumber}",
                IsAcceptingOrders = table.Restaurant.Settings?.IsAcceptingOrders ?? true
            };
        }
    }
}
