using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tablo.Api.DTOs;
using Tablo.Api.Extensions;
using Tablo.Api.Services;

namespace Tablo.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Owner,Waiter,Kitchen,Cashier")]
[Route("api/restaurants/{restaurantId}/modifiers")]
public class ModifiersController : ControllerBase
{
    private readonly ModifierService _modifierService;

    public ModifiersController(ModifierService modifierService)
    {
        _modifierService = modifierService;
    }

    private bool TryResolveRestaurantId(Guid restaurantId, out Guid effectiveRestaurantId)
    {
        var userRestaurantId = User.GetRestaurantId();
        if (userRestaurantId == Guid.Empty)
        {
            effectiveRestaurantId = Guid.Empty;
            return false;
        }

        if (restaurantId != Guid.Empty && restaurantId != userRestaurantId)
        {
            effectiveRestaurantId = Guid.Empty;
            return false;
        }

        effectiveRestaurantId = userRestaurantId;
        return true;
    }

    [HttpGet]
    [HttpGet("groups")]
    public async Task<ActionResult<List<ModifierGroupDto>>> GetGroups(Guid restaurantId)
    {
        if (!TryResolveRestaurantId(restaurantId, out var effectiveRestaurantId)) return Forbid();
        var groups = await _modifierService.GetGroupsAsync(effectiveRestaurantId);
        return Ok(groups);
    }

    [HttpPost]
    [HttpPost("groups")]
    public async Task<ActionResult<ModifierGroupDto>> CreateGroup(Guid restaurantId, [FromBody] CreateModifierGroupDto dto)
    {
        if (!TryResolveRestaurantId(restaurantId, out var effectiveRestaurantId)) return Forbid();
        var created = await _modifierService.CreateGroupAsync(effectiveRestaurantId, dto);
        return CreatedAtAction(nameof(GetGroups), new { restaurantId = effectiveRestaurantId }, created);
    }

    [HttpPut("groups/{groupId}")]
    [HttpPut("/api/modifiers/{groupId}")]
    public async Task<ActionResult<ModifierGroupDto>> UpdateGroup(Guid restaurantId, Guid groupId, [FromBody] UpdateModifierGroupDto dto)
    {
        if (!TryResolveRestaurantId(restaurantId, out var effectiveRestaurantId)) return Forbid();
        var updated = await _modifierService.UpdateGroupAsync(effectiveRestaurantId, groupId, dto);
        return Ok(updated);
    }

    [HttpDelete("groups/{groupId}")]
    [HttpDelete("/api/modifiers/{groupId}")]
    public async Task<IActionResult> DeleteGroup(Guid restaurantId, Guid groupId)
    {
        if (!TryResolveRestaurantId(restaurantId, out var effectiveRestaurantId)) return Forbid();
        await _modifierService.DeleteGroupAsync(effectiveRestaurantId, groupId);
        return NoContent();
    }

    [HttpPost("groups/{groupId}/options")]
    [HttpPost("/api/modifiers/{groupId}/options")]
    public async Task<ActionResult<ModifierOptionDto>> AddOption(Guid restaurantId, Guid groupId, [FromBody] CreateModifierOptionDto dto)
    {
        if (!TryResolveRestaurantId(restaurantId, out var effectiveRestaurantId)) return Forbid();
        var created = await _modifierService.AddOptionAsync(effectiveRestaurantId, groupId, dto);
        return Ok(created);
    }

    [HttpPut("options/{optionId}")]
    [HttpPut("/api/modifiers/options/{optionId}")]
    public async Task<ActionResult<ModifierOptionDto>> UpdateOption(Guid restaurantId, Guid optionId, [FromBody] UpdateModifierOptionDto dto)
    {
        if (!TryResolveRestaurantId(restaurantId, out var effectiveRestaurantId)) return Forbid();
        var updated = await _modifierService.UpdateOptionAsync(effectiveRestaurantId, optionId, dto);
        return Ok(updated);
    }

    [HttpDelete("options/{optionId}")]
    [HttpDelete("/api/modifiers/options/{optionId}")]
    public async Task<IActionResult> DeleteOption(Guid restaurantId, Guid optionId)
    {
        if (!TryResolveRestaurantId(restaurantId, out var effectiveRestaurantId)) return Forbid();
        await _modifierService.DeleteOptionAsync(effectiveRestaurantId, optionId);
        return NoContent();
    }

    [HttpPost("/api/restaurants/{restaurantId}/menu-items/{menuItemId}/modifiers")]
    [HttpPost("/api/menu-items/{menuItemId}/modifier-groups")]
    public async Task<IActionResult> AssignGroupsToMenuItem(Guid restaurantId, Guid menuItemId, [FromBody] AssignModifierGroupsRequestDto dto)
    {
        if (!TryResolveRestaurantId(restaurantId, out var effectiveRestaurantId)) return Forbid();
        await _modifierService.AssignGroupsToMenuItemAsync(effectiveRestaurantId, menuItemId, dto.ModifierGroupIds);
        return Ok(new { message = "Modifier groups assigned to menu item successfully." });
    }

    [HttpGet("/api/restaurants/{restaurantId}/menu-items/{menuItemId}/modifiers")]
    [HttpGet("/api/menu-items/{menuItemId}/modifier-groups")]
    public async Task<ActionResult<List<ModifierGroupDto>>> GetMenuItemGroups(Guid restaurantId, Guid menuItemId)
    {
        if (!TryResolveRestaurantId(restaurantId, out var effectiveRestaurantId)) return Forbid();
        var groups = await _modifierService.GetMenuItemGroupsAsync(effectiveRestaurantId, menuItemId);
        return Ok(groups);
    }
}
