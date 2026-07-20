using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RestaurantsController : ControllerBase
    {
        private readonly IMenuService _menuService;

        public RestaurantsController(IMenuService menuService)
        {
            _menuService = menuService;
        }

        /// <summary>
        /// Retrieves the menu (categories and available menu items) for a given restaurant.
        /// </summary>
        /// <param name="restaurantId">The unique identifier of the restaurant.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The restaurant menu.</returns>
        [HttpGet("{restaurantId:guid}/menu")]
        [ProducesResponseType(typeof(MenuResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MenuResponseDto>> GetMenu(Guid restaurantId, CancellationToken cancellationToken)
        {
            var menu = await _menuService.GetMenuAsync(restaurantId, cancellationToken);

            if (menu == null)
            {
                return NotFound(new { Message = $"Restaurant with ID {restaurantId} was not found." });
            }

            return Ok(menu);
        }
    }
}
