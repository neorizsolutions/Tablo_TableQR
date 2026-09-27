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

        Task<byte[]> GetOrderHistoryExportAsync(
            Guid restaurantId,
            DateTime? startDate,
            DateTime? endDate,
            string? status,
            Guid? tableId,
            string? search);

        Task<AnalyticsSummaryDto> GetAnalyticsSummaryAsync(
            Guid restaurantId,
            DateTime? startDate,
            DateTime? endDate,
            string? status,
            Guid? tableId);

        Task<System.Collections.Generic.IEnumerable<SalesOverTimeDto>> GetSalesOverTimeAsync(
            Guid restaurantId,
            DateTime? startDate,
            DateTime? endDate,
            string? status,
            Guid? tableId);

        Task<System.Collections.Generic.IEnumerable<TopItemDto>> GetTopItemsAsync(
            Guid restaurantId,
            DateTime? startDate,
            DateTime? endDate,
            string? status,
            Guid? tableId);
    }
}
