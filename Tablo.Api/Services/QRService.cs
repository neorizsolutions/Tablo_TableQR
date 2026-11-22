using System;
using System.Threading.Tasks;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services
{
    public class QRService : IQRService
    {
        private readonly ITableRepository _tableRepository;
        private readonly Tablo.Api.Data.ApplicationDbContext? _db;

        public QRService(ITableRepository tableRepository, Tablo.Api.Data.ApplicationDbContext? db=null)
        {
            _tableRepository = tableRepository;
            _db=db;
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
                DiningGroups=_db==null?new(): (await new DiningGroupService(_db).ListAsync(table.RestaurantId,table.Id)).ToList(),
                RestaurantName = table.Restaurant.Name,
                RestaurantTagline = table.Restaurant.Description ?? string.Empty,
                TableId = table.Id,
                TableNameOrNumber = $"Table {table.TableNumber}",
                IsAcceptingOrders = table.Restaurant.Settings?.IsAcceptingOrders ?? true
            };
        }
    }
}
