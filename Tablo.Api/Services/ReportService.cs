using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Interfaces;
using ClosedXML.Excel;
using System.IO;
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
                // Ensure we start at the very beginning of the requested day and set Kind to UTC for Npgsql
                var start = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
                query = query.Where(o => o.CreatedAt >= start);
            }
            if (endDate.HasValue)
            {
                // Ensure we include everything UP TO the end of the requested day and set Kind to UTC
                var end = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Utc);
                query = query.Where(o => o.CreatedAt < end);
            }

            // Filter: Status
            if (!string.IsNullOrWhiteSpace(status))
            {
                var lowerStatus = status.ToLower();
                query = query.Where(o => o.Status.ToLower() == lowerStatus);
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
                    TableId = o.TableId ?? Guid.Empty,
                    TableName = o.Table != null ? $"Table {o.Table.TableNumber}" : (o.DailyTokenNumber.HasValue ? $"Parcel #{o.DailyTokenNumber.Value:D3}" : "Parcel Takeaway"),
                    CreatedAt = o.CreatedAt,
                    UpdatedAt = o.UpdatedAt,
                    Status = o.Status,
                    ExceptionReason = o.ExceptionReason,
                    Subtotal = o.Subtotal,
                    Tax = o.Tax,
                    Discount = o.Discount,
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

        public async Task<byte[]> GetOrderHistoryExportAsync(
            Guid restaurantId,
            DateTime? startDate,
            DateTime? endDate,
            string? status,
            Guid? tableId,
            string? search)
        {
            var query = _context.Orders
                .Include(o => o.Table)
                .Include(o => o.OrderItems)
                .AsNoTracking()
                .Where(o => o.RestaurantId == restaurantId);

            if (startDate.HasValue)
            {
                var start = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
                query = query.Where(o => o.CreatedAt >= start);
            }
            if (endDate.HasValue)
            {
                var end = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Utc);
                query = query.Where(o => o.CreatedAt < end);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var lowerStatus = status.ToLower();
                query = query.Where(o => o.Status.ToLower() == lowerStatus);
            }

            if (tableId.HasValue)
            {
                query = query.Where(o => o.TableId == tableId.Value);
            }

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
                    query = query.Where(o => false);
                }
            }

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Order Report");

            // Headers
            worksheet.Cell(1, 1).Value = "Order Number";
            worksheet.Cell(1, 2).Value = "Order Date";
            worksheet.Cell(1, 3).Value = "Table";
            worksheet.Cell(1, 4).Value = "Status";
            worksheet.Cell(1, 5).Value = "Items";
            worksheet.Cell(1, 6).Value = "Quantity";
            worksheet.Cell(1, 7).Value = "Subtotal (INR)";
            worksheet.Cell(1, 8).Value = "Tax (INR)";
            worksheet.Cell(1, 9).Value = "Discount (INR)";
            worksheet.Cell(1, 10).Value = "Total (INR)";

            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;
            worksheet.SheetView.FreezeRows(1);

            int row = 2;
            foreach (var order in orders)
            {
                worksheet.Cell(row, 1).Value = $"ORD-{order.OrderNumber:D4}";
                worksheet.Cell(row, 2).Value = order.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                worksheet.Cell(row, 3).Value = $"Table {order.Table?.TableNumber}";
                worksheet.Cell(row, 4).Value = order.Status;
                
                var itemNames = string.Join(", ", order.OrderItems.Select(oi => oi.ItemNameSnapshot));
                worksheet.Cell(row, 5).Value = itemNames;
                
                var totalQty = order.OrderItems.Sum(oi => oi.Quantity);
                worksheet.Cell(row, 6).Value = totalQty;
                
                var subtotalCell = worksheet.Cell(row, 7);
                subtotalCell.Value = order.Subtotal;
                subtotalCell.Style.NumberFormat.Format = "[$₹-400] #,##0.00";

                var taxCell = worksheet.Cell(row, 8);
                taxCell.Value = order.Tax;
                taxCell.Style.NumberFormat.Format = "[$₹-400] #,##0.00";

                var discountCell = worksheet.Cell(row, 9);
                discountCell.Value = order.Discount;
                discountCell.Style.NumberFormat.Format = "[$₹-400] #,##0.00";

                var totalCell = worksheet.Cell(row, 10);
                totalCell.Value = order.TotalAmount;
                totalCell.Style.NumberFormat.Format = "[$₹-400] #,##0.00"; // Native Excel INR formatting

                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
        private IQueryable<Order> BuildBaseQuery(Guid restaurantId, DateTime? startDate, DateTime? endDate, string? status, Guid? tableId)
        {
            var query = _context.Orders
                .AsNoTracking()
                .Where(o => o.RestaurantId == restaurantId);

            if (startDate.HasValue)
            {
                var start = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
                query = query.Where(o => o.CreatedAt >= start);
            }
            if (endDate.HasValue)
            {
                var end = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Utc);
                query = query.Where(o => o.CreatedAt < end);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var lowerStatus = status.ToLower();
                query = query.Where(o => o.Status.ToLower() == lowerStatus);
            }
            else
            {
                // Default: Exclude Cancelled and Rejected from Analytics if no specific status is requested
                query = query.Where(o => o.Status.ToLower() != "cancelled" && o.Status.ToLower() != "rejected");
            }

            if (tableId.HasValue)
            {
                query = query.Where(o => o.TableId == tableId.Value);
            }

            return query;
        }

        public async Task<AnalyticsSummaryDto> GetAnalyticsSummaryAsync(Guid restaurantId, DateTime? startDate, DateTime? endDate, string? status, Guid? tableId)
        {
            var query = BuildBaseQuery(restaurantId, startDate, endDate, status, tableId);

            var summary = await query
                .Select(o => new {
                    o.TotalAmount,
                    ItemsCount = o.OrderItems.Sum(oi => oi.Quantity)
                })
                .ToListAsync();

            var totalOrders = summary.Count;
            var totalRevenue = summary.Sum(x => x.TotalAmount);

            return new AnalyticsSummaryDto
            {
                TotalOrders = totalOrders,
                TotalRevenue = totalRevenue,
                TotalItemsSold = summary.Sum(x => x.ItemsCount),
                AverageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0
            };
        }

        public async Task<System.Collections.Generic.IEnumerable<SalesOverTimeDto>> GetSalesOverTimeAsync(Guid restaurantId, DateTime? startDate, DateTime? endDate, string? status, Guid? tableId)
        {
            var query = BuildBaseQuery(restaurantId, startDate, endDate, status, tableId);

            var sales = await query
                .Select(o => new { o.CreatedAt.Date, o.TotalAmount })
                .GroupBy(o => o.Date)
                .Select(g => new SalesOverTimeDto
                {
                    Date = g.Key,
                    Revenue = g.Sum(x => x.TotalAmount),
                    OrderCount = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return sales;
        }

        public async Task<System.Collections.Generic.IEnumerable<TopItemDto>> GetTopItemsAsync(Guid restaurantId, DateTime? startDate, DateTime? endDate, string? status, Guid? tableId)
        {
            var query = BuildBaseQuery(restaurantId, startDate, endDate, status, tableId);

            var topItems = await query
                .SelectMany(o => o.OrderItems)
                .GroupBy(oi => oi.ItemNameSnapshot)
                .Select(g => new TopItemDto
                {
                    ItemName = g.Key,
                    QuantitySold = g.Sum(oi => oi.Quantity),
                    Revenue = g.Sum(oi => oi.Quantity * oi.UnitPrice)
                })
                .OrderByDescending(x => x.QuantitySold)
                .Take(10)
                .ToListAsync();

            return topItems;
        }
    }
}
