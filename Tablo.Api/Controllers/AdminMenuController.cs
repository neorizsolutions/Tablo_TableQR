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
    [Authorize(Roles = "Admin")]
    [Route("api/restaurants/{restaurantId:guid}")]
    public class AdminMenuController : ControllerBase
    {
        private readonly IMenuService _menuService;
        private readonly IImageStorageService _imageStorageService;

        public AdminMenuController(IMenuService menuService, IImageStorageService imageStorageService)
        {
            _menuService = menuService;
            _imageStorageService = imageStorageService;
        }

        [HttpGet("menu/admin")]
        [ProducesResponseType(typeof(MenuResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MenuResponseDto>> GetAdminMenu(Guid restaurantId, CancellationToken cancellationToken)
        {
            if (User.GetRestaurantId() != restaurantId) return Forbid();

            var menu = await _menuService.GetAdminMenuAsync(restaurantId, cancellationToken);
            if (menu == null)
            {
                return NotFound(new { Message = $"Restaurant with ID {restaurantId} was not found." });
            }
            return Ok(menu);
        }

        [HttpPost("categories")]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CategoryDto>> CreateCategory(Guid restaurantId, [FromBody] CreateCategoryRequestDto request, CancellationToken cancellationToken)
        {
            if (User.GetRestaurantId() != restaurantId) return Forbid();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var category = await _menuService.CreateCategoryAsync(restaurantId, request, cancellationToken);
                return CreatedAtAction(nameof(GetAdminMenu), new { restaurantId = restaurantId }, category);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost("menu-items")]
        [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<MenuItemDto>> CreateMenuItem(Guid restaurantId, [FromForm] CreateMenuItemRequestDto request, CancellationToken cancellationToken)
        {
            if (User.GetRestaurantId() != restaurantId) return Forbid();

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
                var menuItem = await _menuService.CreateMenuItemAsync(restaurantId, request, cancellationToken);
                return CreatedAtAction(nameof(GetAdminMenu), new { restaurantId = restaurantId }, menuItem);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
    }
}

