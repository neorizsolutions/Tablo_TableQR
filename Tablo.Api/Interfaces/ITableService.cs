using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tablo.Api.DTOs;

namespace Tablo.Api.Interfaces
{
    public interface ITableService
    {
        Task<IEnumerable<TableDto>> GetTablesAsync(Guid restaurantId);
        Task<TableDto> CreateTableAsync(Guid restaurantId, CreateTableRequestDto request);
        Task<TableDto> UpdateTableAsync(Guid restaurantId, Guid tableId, UpdateTableRequestDto request);
        Task<TableDto> RegenerateQRTokenAsync(Guid restaurantId, Guid tableId);
    }
}
