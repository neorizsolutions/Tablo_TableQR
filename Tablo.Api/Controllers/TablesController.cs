using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Tablo.Api.Extensions;

namespace Tablo.Api.Controllers
{
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class TablesController : ControllerBase
    {
        private readonly ITableService _tableService;

        public TablesController(ITableService tableService)
        {
            _tableService = tableService;
        }

        [HttpGet("/api/restaurants/{restaurantId}/tables")]
        public async Task<ActionResult<IEnumerable<TableDto>>> GetTables(Guid restaurantId)
        {
            if (User.GetRestaurantId() != restaurantId) return Forbid();

            var tables = await _tableService.GetTablesAsync(restaurantId);
            return Ok(tables);
        }

        [HttpPost("/api/restaurants/{restaurantId}/tables")]
        public async Task<ActionResult<TableDto>> CreateTable(Guid restaurantId, [FromBody] CreateTableRequestDto request)
        {
            if (User.GetRestaurantId() != restaurantId) return Forbid();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var table = await _tableService.CreateTableAsync(restaurantId, request);
                return CreatedAtAction(nameof(GetTables), new { restaurantId = table.RestaurantId }, table);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("/api/restaurants/{restaurantId}/tables/{tableId}")]
        public async Task<ActionResult<TableDto>> UpdateTable(Guid restaurantId, Guid tableId, [FromBody] UpdateTableRequestDto request)
        {
            if (User.GetRestaurantId() != restaurantId) return Forbid();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var table = await _tableService.UpdateTableAsync(restaurantId, tableId, request);
                return Ok(table);
            }
            catch (ArgumentException ex)
            {
                if (ex.Message.Contains("already exists"))
                {
                    return BadRequest(new { message = ex.Message });
                }
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("/api/restaurants/{restaurantId}/tables/{tableId}/regenerate-qr")]
        public async Task<ActionResult<TableDto>> RegenerateQR(Guid restaurantId, Guid tableId)
        {
            if (User.GetRestaurantId() != restaurantId) return Forbid();

            try
            {
                var table = await _tableService.RegenerateQRTokenAsync(restaurantId, tableId);
                return Ok(table);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpGet("/api/restaurants/{restaurantId}/tables/{tableId}/qr")]
        public async Task<IActionResult> DownloadQR(Guid restaurantId, Guid tableId, [FromQuery] string origin)
        {
            if (User.GetRestaurantId() != restaurantId) return Forbid();

            var tables = await _tableService.GetTablesAsync(restaurantId);
            var table = System.Linq.Enumerable.FirstOrDefault(tables, t => t.Id == tableId);
            
            if (table == null) return NotFound(new { message = "Table not found" });

            if (string.IsNullOrEmpty(origin))
            {
                origin = "http://localhost:8080";
            }

            var qrUrl = $"{origin.TrimEnd('/')}/t/{table.QRToken}";

            using var qrGenerator = new QRCoder.QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(qrUrl, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
            
            // Pixels per module (20 makes a nice high-res image)
            var qrCodeBytes = qrCode.GetGraphic(20);

            var rawName = $"table-{table.TableNumber}";
            var safeName = System.Text.RegularExpressions.Regex.Replace(rawName, "[^a-zA-Z0-9]", "-").ToLower();
            var filename = $"tablo-table-{safeName}-qr.png";

            return File(qrCodeBytes, "image/png", filename);
        }
    }
}
