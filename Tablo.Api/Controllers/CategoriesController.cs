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
    [Route("api/categories/{categoryId:guid}")]
    public class CategoriesController : ControllerBase
    {
        private readonly IMenuService _menuService;
        private readonly IMenuRepository _menuRepository;

        public CategoriesController(IMenuService menuService, IMenuRepository menuRepository)
        {
            _menuService = menuService;
            _menuRepository = menuRepository;
        }

        [HttpPut]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CategoryDto>> UpdateCategory(Guid categoryId, [FromBody] UpdateCategoryRequestDto request, CancellationToken cancellationToken)
        {
            var categoryToUpdate = await _menuRepository.GetCategoryAsync(categoryId, cancellationToken);
            if (categoryToUpdate == null) return NotFound(new { Message = "Category not found." });
            if (categoryToUpdate.RestaurantId != User.GetRestaurantId()) return Forbid();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var category = await _menuService.UpdateCategoryAsync(categoryId, request, cancellationToken);
                return Ok(category);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteCategory(Guid categoryId, CancellationToken cancellationToken)
        {
            var categoryToDelete = await _menuRepository.GetCategoryAsync(categoryId, cancellationToken);
            if (categoryToDelete == null) return NotFound(new { Message = "Category not found." });
            if (categoryToDelete.RestaurantId != User.GetRestaurantId()) return Forbid();

            try
            {
                await _menuService.DeleteCategoryAsync(categoryId, cancellationToken);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
    }
}
