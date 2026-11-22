using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionsController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet("effective-access")]
    public async Task<IActionResult> GetEffectiveAccess([FromQuery] Guid? outletId = null)
    {
        var restaurantIdStr = User.FindFirst("RestaurantId")?.Value;
        var subStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(restaurantIdStr) || !Guid.TryParse(restaurantIdStr, out var restaurantId) ||
            string.IsNullOrEmpty(subStr) || !Guid.TryParse(subStr, out var employeeId))
        {
            // SuperAdmin or non-restaurant user
            if (User.IsInRole("SuperAdmin"))
            {
                return Ok(new EffectiveAccessDto
                {
                    RestaurantId = Guid.Empty,
                    RestaurantName = "Platform Administration",
                    Role = "SuperAdmin",
                    EmployeeName = "Super Admin",
                    EmployeeEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "superadmin@tablo.local",
                    Plan = new PlanSummaryDto
                    {
                        Code = "platform",
                        Name = "Super Admin",
                        Status = "Active"
                    },
                    Capabilities = new List<string> { "all" }
                });
            }

            return Unauthorized(new { message = "User context is missing." });
        }

        var access = await _subscriptionService.GetEffectiveAccessAsync(restaurantId, employeeId, outletId);
        if (access == null)
        {
            return Forbid();
        }

        return Ok(access);
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentSubscription()
    {
        var restaurantIdStr = User.FindFirst("RestaurantId")?.Value;
        if (string.IsNullOrEmpty(restaurantIdStr) || !Guid.TryParse(restaurantIdStr, out var restaurantId))
        {
            return BadRequest(new { message = "Restaurant context is missing." });
        }

        var sub = await _subscriptionService.GetRestaurantSubscriptionAsync(restaurantId);
        if (sub == null)
        {
            return NotFound(new { message = "Subscription not found for this restaurant." });
        }

        return Ok(sub);
    }

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans()
    {
        var plans = await _subscriptionService.GetPlansAsync();
        return Ok(plans);
    }

    [HttpGet("preview-change")]
    public async Task<IActionResult> PreviewPlanChange([FromQuery] string targetPlanCode)
    {
        var restaurantIdStr = User.FindFirst("RestaurantId")?.Value;
        if (string.IsNullOrEmpty(restaurantIdStr) || !Guid.TryParse(restaurantIdStr, out var restaurantId))
        {
            return BadRequest(new { message = "Restaurant context is missing." });
        }

        if (string.IsNullOrWhiteSpace(targetPlanCode))
        {
            return BadRequest(new { message = "Target plan code is required." });
        }

        try
        {
            var preview = await _subscriptionService.PreviewPlanChangeAsync(restaurantId, targetPlanCode);
            return Ok(preview);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
