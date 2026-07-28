using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResponseDto<ReportOrderDto>> GetOrderHistoryAsync(
            Guid restaurantId,
            DateTime? startDate,
            DateTime? endDate,
            string? status,
            Guid? tableId,
            string? search,
            int page,
            int pageSize)
        {
            // Validate pagination inputs
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100; // Cap to prevent massive payloads

            // Mandatory Tenant Isolation
            var query = _context.Orders
                .AsNoTracking()
                .Where(o => o.RestaurantId == restaurantId);

            // Filter: Date Range
            if (startDate.HasValue)
            {
                // Ensure we start at the very beginning of the requested day
                var start = startDate.Value.Date;
                query = query.Where(o => o.CreatedAt >= start);
            }
            if (endDate.HasValue)
            {
                // Ensure we include everything UP TO the end of the requested day
                var end = endDate.Value.Date.AddDays(1);
                query = query.Where(o => o.CreatedAt < end);
            }

            // Filter: Status
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(o => o.Status == status);
            }

            // Filter: Table
            if (tableId.HasValue)
            {
                query = query.Where(o => o.TableId == tableId.Value);
            }

            // Filter: Search (Order Number)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var cleanSearch = search.Trim().ToUpper();
                if (cleanSearch.StartsWith("ORD-"))
                {
                    cleanSearch = cleanSearch.Substring(4);
                }

                if (int.TryParse(cleanSearch, out int searchOrderNumber))
                {
                    query = query.Where(o => o.OrderNumber == searchOrderNumber);
                }
                else
                {
                    // If they typed something entirely non-numeric that isn't ORD-, 
                    // it will never match an order number. We enforce a condition that returns false.
                    query = query.Where(o => false);
                }
            }

            // Total Count for Pagination
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Execute paginated projection
            var items = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new ReportOrderDto
                {
                    OrderId = o.Id,
                    OrderNumber = o.OrderNumber,
                    TableId = o.TableId,
                    TableName = o.Table.NameOrNumber, // EF Core correctly navigates because we use .Select() projection
                    CreatedAt = o.CreatedAt,
                    UpdatedAt = o.UpdatedAt,
                    Status = o.Status,
                    TotalAmount = o.TotalAmount,
                    Items = o.OrderItems.Select(oi => new ReportOrderItemDto
                    {
                        ItemNameSnapshot = oi.ItemNameSnapshot,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice
                    }).ToList()
                })
                .ToListAsync();

            return new PagedResponseDto<ReportOrderDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };
        }
    }
}
