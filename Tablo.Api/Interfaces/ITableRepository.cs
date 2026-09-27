using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tablo.Api.Entities;

namespace Tablo.Api.Interfaces
{
    public interface ITableRepository
    {
        Task<IEnumerable<RestaurantTable>> GetTablesAsync(Guid restaurantId);
        Task<RestaurantTable?> GetTableByIdAsync(Guid restaurantId, Guid tableId);
        Task<RestaurantTable?> GetTableByQRTokenAsync(string qrToken);
        Task<RestaurantTable?> GetTableByNumberAsync(Guid restaurantId, int tableNumber);
        Task<RestaurantTable> CreateTableAsync(RestaurantTable table);
        Task UpdateTableAsync(RestaurantTable table);
    }
}
