using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;
using System.Security.Claims;

namespace Tablo.Api.Controllers
{
    [ApiController]
    [Route("api/restaurants/{restaurantId}/settings")]
    [Authorize(Roles = "Admin,Owner,Waiter,Kitchen,Cashier")]
    public class SettingsController : ControllerBase
    {
        private readonly ISettingsService _settingsService;

        public SettingsController(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        private Guid GetUserRestaurantId()
        {
            var claim = User.FindFirst("RestaurantId");
            if (claim != null && Guid.TryParse(claim.Value, out var restaurantId))
            {
                return restaurantId;
            }
            return Guid.Empty;
        }

        [HttpGet]
        [ProducesResponseType(typeof(RestaurantSettingsDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<RestaurantSettingsDto>> GetSettings(Guid restaurantId)
        {
            var userRestaurantId = GetUserRestaurantId();
            if (userRestaurantId != restaurantId)
            {
                return Forbid();
            }

            var settings = await _settingsService.GetSettingsAsync(restaurantId);
            return Ok(settings);
        }

        [HttpPatch("accepting-orders")]
        [ProducesResponseType(typeof(RestaurantSettingsDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<RestaurantSettingsDto>> UpdateAcceptingOrders(Guid restaurantId, [FromBody] UpdateAcceptingOrdersDto request)
        {
            var userRestaurantId = GetUserRestaurantId();
            if (userRestaurantId != restaurantId)
            {
                return Forbid();
            }

            var settings = await _settingsService.UpdateAcceptingOrdersAsync(restaurantId, request.IsAcceptingOrders);
            return Ok(settings);
        }

        [HttpPut("kitchen-printing")]
        [ProducesResponseType(typeof(RestaurantSettingsDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<RestaurantSettingsDto>> UpdateKitchenPrinting(Guid restaurantId, [FromBody] UpdateKitchenPrintingSettingsDto request)
        {
            var userRestaurantId = GetUserRestaurantId();
            if (userRestaurantId != restaurantId && !User.IsInRole("SuperAdmin"))
            {
                return Forbid();
            }

            try
            {
                var settings = await _settingsService.UpdateKitchenPrintingSettingsAsync(restaurantId, request);
                return Ok(settings);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("upi-qr")]
        [ProducesResponseType(typeof(RestaurantSettingsDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<RestaurantSettingsDto>> UpdateUpiQrSettings(Guid restaurantId, [FromBody] UpdateUpiQrSettingsDto request)
        {
            var userRestaurantId = GetUserRestaurantId();
            if (userRestaurantId != restaurantId) return Forbid();

            try
            {
                var settings = await _settingsService.UpdateUpiQrSettingsAsync(restaurantId, request);
                return Ok(settings);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("scheduled-pickup")]
        [ProducesResponseType(typeof(RestaurantSettingsDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<RestaurantSettingsDto>> UpdateScheduledPickupSettings(Guid restaurantId, [FromBody] UpdateScheduledPickupSettingsDto request)
        {
            var userRestaurantId = GetUserRestaurantId();
            if (userRestaurantId != restaurantId) return Forbid();

            try
            {
                var settings = await _settingsService.UpdateScheduledPickupSettingsAsync(restaurantId, request);
                return Ok(settings);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("car-side")]
        [ProducesResponseType(typeof(RestaurantSettingsDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<RestaurantSettingsDto>> UpdateCarSideSettings(Guid restaurantId, [FromBody] UpdateCarSideSettingsDto request)
        {
            var userRestaurantId = GetUserRestaurantId();
            if (userRestaurantId != restaurantId) return Forbid();

            try
            {
                var settings = await _settingsService.UpdateCarSideSettingsAsync(restaurantId, request);
                return Ok(settings);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("hybrid-packaging")]
        [ProducesResponseType(typeof(RestaurantSettingsDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<RestaurantSettingsDto>> UpdateHybridPackagingSettings(Guid restaurantId, [FromBody] UpdateHybridPackagingSettingsDto request)
        {
            var userRestaurantId = GetUserRestaurantId();
            if (userRestaurantId != restaurantId) return Forbid();

            try
            {
                var settings = await _settingsService.UpdateHybridPackagingSettingsAsync(restaurantId, request);
                return Ok(settings);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
