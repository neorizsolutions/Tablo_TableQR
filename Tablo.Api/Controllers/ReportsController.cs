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
                // In a real application, this should be logged.
                return StatusCode(500, new { message = "An error occurred while generating the report." });
            }
        }
    }
}
