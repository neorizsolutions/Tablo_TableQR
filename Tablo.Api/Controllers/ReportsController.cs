using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tablo.Api.Extensions;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrderHistory(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? status,
            [FromQuery] Guid? tableId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var restaurantId = User.GetRestaurantId();

                if (startDate.HasValue && endDate.HasValue && startDate.Value > endDate.Value)
                {
                    return BadRequest(new { message = "startDate cannot be after endDate." });
                }

                var result = await _reportService.GetOrderHistoryAsync(
                    restaurantId,
                    startDate,
                    endDate,
                    status,
                    tableId,
                    search,
                    page,
                    pageSize);

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return StatusCode(500, new { message = "An error occurred while generating the report.", details = ex.Message });
            }
        }

        [HttpGet("orders/export")]
        public async Task<IActionResult> GetOrderHistoryExport(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? status,
            [FromQuery] Guid? tableId,
            [FromQuery] string? search)
        {
            try
            {
                var restaurantId = User.GetRestaurantId();

                if (startDate.HasValue && endDate.HasValue && startDate.Value > endDate.Value)
                {
                    return BadRequest(new { message = "startDate cannot be after endDate." });
                }

                var excelBytes = await _reportService.GetOrderHistoryExportAsync(
                    restaurantId,
                    startDate,
                    endDate,
                    status,
                    tableId,
                    search);

                var fileName = $"Tablo_Order_Report_{DateTime.UtcNow:yyyy-MM-dd}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return StatusCode(500, new { message = "An error occurred while exporting the report.", details = ex.Message });
            }
        }
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? status,
            [FromQuery] Guid? tableId)
        {
            try
            {
                var restaurantId = User.GetRestaurantId();
                if (startDate.HasValue && endDate.HasValue && startDate.Value > endDate.Value)
                    return BadRequest(new { message = "startDate cannot be after endDate." });

                var result = await _reportService.GetAnalyticsSummaryAsync(restaurantId, startDate, endDate, status, tableId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return StatusCode(500, new { message = "An error occurred while generating the summary.", details = ex.Message });
            }
        }

        [HttpGet("sales-over-time")]
        public async Task<IActionResult> GetSalesOverTime(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? status,
            [FromQuery] Guid? tableId)
        {
            try
            {
                var restaurantId = User.GetRestaurantId();
                if (startDate.HasValue && endDate.HasValue && startDate.Value > endDate.Value)
                    return BadRequest(new { message = "startDate cannot be after endDate." });

                var result = await _reportService.GetSalesOverTimeAsync(restaurantId, startDate, endDate, status, tableId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return StatusCode(500, new { message = "An error occurred while generating the sales chart data.", details = ex.Message });
            }
        }

        [HttpGet("top-items")]
        public async Task<IActionResult> GetTopItems(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? status,
            [FromQuery] Guid? tableId)
        {
            try
            {
                var restaurantId = User.GetRestaurantId();
                if (startDate.HasValue && endDate.HasValue && startDate.Value > endDate.Value)
                    return BadRequest(new { message = "startDate cannot be after endDate." });

                var result = await _reportService.GetTopItemsAsync(restaurantId, startDate, endDate, status, tableId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return StatusCode(500, new { message = "An error occurred while generating the top items report.", details = ex.Message });
            }
        }
    }
}
