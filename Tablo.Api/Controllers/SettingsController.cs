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
    [Authorize(Roles = "Admin,Owner")]
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
    }
}
