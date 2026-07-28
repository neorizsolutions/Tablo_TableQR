using System;
using System.Threading.Tasks;
using Tablo.Api.DTOs;

namespace Tablo.Api.Interfaces
{
    public interface IReportService
    {
        Task<PagedResponseDto<ReportOrderDto>> GetOrderHistoryAsync(
            Guid restaurantId,
            DateTime? startDate,
            DateTime? endDate,
            string? status,
            Guid? tableId,
            string? search,
            int page,
            int pageSize);
    }
}
