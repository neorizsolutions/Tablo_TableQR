using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServiceRequestsController : ControllerBase
    {
        private readonly IServiceRequestService _serviceRequestService;
        private readonly ApplicationDbContext _context;

        public ServiceRequestsController(IServiceRequestService serviceRequestService, ApplicationDbContext context)
        {
            _serviceRequestService = serviceRequestService;
            _context = context;
        }

        [HttpGet("qr/{qrToken}")]
        public async Task<ActionResult<IEnumerable<ServiceRequestDto>>> GetTableActiveRequests(string qrToken,[FromQuery] Guid? diningGroupId=null)
        {
            try
            {
                var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.QRToken == qrToken && t.IsActive);
                if (table == null) return NotFound(new { message = "Invalid QR token" });

                var activeStatuses = new[] { "Pending", "Acknowledged" };
                var requests = await _context.ServiceRequests
                    .Include(sr => sr.Table)
                    .Where(sr => sr.TableId == table.Id && sr.DiningGroupId==diningGroupId && activeStatuses.Contains(sr.Status))
                    .OrderBy(sr => sr.CreatedAt)
                    .Select(sr => new ServiceRequestDto
                    {
                        Id = sr.Id, DiningGroupId=sr.DiningGroupId,
                        RestaurantId = sr.RestaurantId,
                        TableId = sr.TableId,
                        TableName = $"Table {sr.Table.TableNumber}",
                        Type = sr.Type,
                        Status = sr.Status,
                        CreatedAt = sr.CreatedAt,
                        UpdatedAt = sr.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(requests);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("qr/{qrToken}")]
        public async Task<ActionResult<ServiceRequestDto>> CreateRequest(string qrToken, [FromBody] CreateServiceRequestDto requestDto)
        {
            try
            {
                var result = await _serviceRequestService.CreateRequestAsync(qrToken, requestDto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize(Roles = "Admin,Waiter")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceRequestDto>>> GetActiveRequests()
        {
            var restaurantIdClaim = User.FindFirst("RestaurantId")?.Value;
            if (string.IsNullOrEmpty(restaurantIdClaim) || !Guid.TryParse(restaurantIdClaim, out var restaurantId))
            {
                return Unauthorized(new { message = "Invalid token claims." });
            }

            try
            {
                var requests = await _serviceRequestService.GetActiveRequestsAsync(restaurantId);
                return Ok(requests);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize(Roles = "Admin,Waiter")]
        [HttpPatch("{id}/acknowledge")]
        public async Task<ActionResult<ServiceRequestDto>> AcknowledgeRequest(Guid id)
        {
            var restaurantIdClaim = User.FindFirst("RestaurantId")?.Value;
            if (string.IsNullOrEmpty(restaurantIdClaim) || !Guid.TryParse(restaurantIdClaim, out var restaurantId))
            {
                return Unauthorized(new { message = "Invalid token claims." });
            }

            try
            {
                var result = await _serviceRequestService.AcknowledgeRequestAsync(restaurantId, id);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize(Roles = "Admin,Waiter")]
        [HttpPatch("{id}/complete")]
        public async Task<ActionResult<ServiceRequestDto>> CompleteRequest(Guid id)
        {
            var restaurantIdClaim = User.FindFirst("RestaurantId")?.Value;
            if (string.IsNullOrEmpty(restaurantIdClaim) || !Guid.TryParse(restaurantIdClaim, out var restaurantId))
            {
                return Unauthorized(new { message = "Invalid token claims." });
            }

            try
            {
                var result = await _serviceRequestService.CompleteRequestAsync(restaurantId, id);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
