using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Tablo.Api.Extensions;

namespace Tablo.Api.Controllers
{
    [ApiController]
    [Authorize(Roles = "Admin,Owner,Waiter,Kitchen,Cashier")]
    [Route("api/menu-items/{menuItemId:guid}")]
    public class MenuItemsController : ControllerBase
    {
        private readonly IMenuService _menuService;
        private readonly IMenuRepository _menuRepository;
        private readonly IImageStorageService _imageStorageService;

        public MenuItemsController(IMenuService menuService, IMenuRepository menuRepository, IImageStorageService imageStorageService)
        {
            _menuService = menuService;
            _menuRepository = menuRepository;
            _imageStorageService = imageStorageService;
        }

        [HttpPut]
        [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<MenuItemDto>> UpdateMenuItem(Guid menuItemId, [FromForm] UpdateMenuItemRequestDto request, CancellationToken cancellationToken)
        {
            var itemToUpdate = await _menuRepository.GetMenuItemAsync(menuItemId, cancellationToken);
            if (itemToUpdate == null) return NotFound(new { Message = "Menu item not found." });
            if (itemToUpdate.RestaurantId != User.GetRestaurantId()) return Forbid();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.ImageFile != null)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                var ext = System.IO.Path.GetExtension(request.ImageFile.FileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(ext) || !System.Linq.Enumerable.Contains(allowedExtensions, ext))
                {
                    return BadRequest(new { Message = "Invalid image format." });
                }

                request.ImageUrl = await _imageStorageService.UploadImageAsync(request.ImageFile, cancellationToken);
            }

            try
            {
                var menuItem = await _menuService.UpdateMenuItemAsync(menuItemId, request, cancellationToken);
                return Ok(menuItem);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPatch("availability")]
        [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<MenuItemDto>> UpdateMenuItemAvailability(Guid menuItemId, [FromBody] UpdateMenuItemAvailabilityRequestDto request, CancellationToken cancellationToken)
        {
            var itemToUpdate = await _menuRepository.GetMenuItemAsync(menuItemId, cancellationToken);
            if (itemToUpdate == null) return NotFound(new { Message = "Menu item not found." });
            if (itemToUpdate.RestaurantId != User.GetRestaurantId()) return Forbid();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var menuItem = await _menuService.UpdateMenuItemAvailabilityAsync(menuItemId, request, cancellationToken);
                return Ok(menuItem);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteMenuItem(Guid menuItemId, CancellationToken cancellationToken)
        {
            var itemToDelete = await _menuRepository.GetMenuItemAsync(menuItemId, cancellationToken);
            if (itemToDelete == null) return NotFound(new { Message = "Menu item not found." });
            if (itemToDelete.RestaurantId != User.GetRestaurantId()) return Forbid();

            await _menuService.DeleteMenuItemAsync(menuItemId, cancellationToken);
            return NoContent();
        }
    }
}

