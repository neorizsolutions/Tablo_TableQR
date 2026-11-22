using Microsoft.AspNetCore.Mvc;
using Tablo.Api.DTOs;
using Tablo.Api.Services;

namespace Tablo.Api.Controllers;

[ApiController]
[Route("api/restaurants/{restaurantId}/modifiers")]
public class ModifiersController : ControllerBase
{
    private readonly ModifierService _modifierService;

    public ModifiersController(ModifierService modifierService)
    {
        _modifierService = modifierService;
    }

    [HttpGet]
    [HttpGet("groups")]
    public async Task<ActionResult<List<ModifierGroupDto>>> GetGroups(Guid restaurantId)
    {
        var groups = await _modifierService.GetGroupsAsync(restaurantId);
        return Ok(groups);
    }

    [HttpPost]
    [HttpPost("groups")]
    public async Task<ActionResult<ModifierGroupDto>> CreateGroup(Guid restaurantId, [FromBody] CreateModifierGroupDto dto)
    {
        var created = await _modifierService.CreateGroupAsync(restaurantId, dto);
        return CreatedAtAction(nameof(GetGroups), new { restaurantId }, created);
    }

    [HttpPut("groups/{groupId}")]
    [HttpPut("/api/modifiers/{groupId}")]
    public async Task<ActionResult<ModifierGroupDto>> UpdateGroup(Guid restaurantId, Guid groupId, [FromBody] UpdateModifierGroupDto dto)
    {
        var updated = await _modifierService.UpdateGroupAsync(restaurantId, groupId, dto);
        return Ok(updated);
    }

    [HttpDelete("groups/{groupId}")]
    [HttpDelete("/api/modifiers/{groupId}")]
    public async Task<IActionResult> DeleteGroup(Guid restaurantId, Guid groupId)
    {
        await _modifierService.DeleteGroupAsync(restaurantId, groupId);
        return NoContent();
    }

    [HttpPost("groups/{groupId}/options")]
    [HttpPost("/api/modifiers/{groupId}/options")]
    public async Task<ActionResult<ModifierOptionDto>> AddOption(Guid restaurantId, Guid groupId, [FromBody] CreateModifierOptionDto dto)
    {
        var created = await _modifierService.AddOptionAsync(restaurantId, groupId, dto);
        return Ok(created);
    }

    [HttpPut("options/{optionId}")]
    [HttpPut("/api/modifiers/options/{optionId}")]
    public async Task<ActionResult<ModifierOptionDto>> UpdateOption(Guid restaurantId, Guid optionId, [FromBody] UpdateModifierOptionDto dto)
    {
        var updated = await _modifierService.UpdateOptionAsync(restaurantId, optionId, dto);
        return Ok(updated);
    }

    [HttpDelete("options/{optionId}")]
    [HttpDelete("/api/modifiers/options/{optionId}")]
    public async Task<IActionResult> DeleteOption(Guid restaurantId, Guid optionId)
    {
        await _modifierService.DeleteOptionAsync(restaurantId, optionId);
        return NoContent();
    }

    [HttpPost("/api/restaurants/{restaurantId}/menu-items/{menuItemId}/modifiers")]
    [HttpPost("/api/menu-items/{menuItemId}/modifier-groups")]
    public async Task<IActionResult> AssignGroupsToMenuItem(Guid restaurantId, Guid menuItemId, [FromBody] AssignModifierGroupsRequestDto dto)
    {
        await _modifierService.AssignGroupsToMenuItemAsync(restaurantId, menuItemId, dto.ModifierGroupIds);
        return Ok(new { message = "Modifier groups assigned to menu item successfully." });
    }

    [HttpGet("/api/restaurants/{restaurantId}/menu-items/{menuItemId}/modifiers")]
    [HttpGet("/api/menu-items/{menuItemId}/modifier-groups")]
    public async Task<ActionResult<List<ModifierGroupDto>>> GetMenuItemGroups(Guid restaurantId, Guid menuItemId)
    {
        var groups = await _modifierService.GetMenuItemGroupsAsync(restaurantId, menuItemId);
        return Ok(groups);
    }
}
