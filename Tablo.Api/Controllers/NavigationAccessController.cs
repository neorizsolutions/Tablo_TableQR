using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Attributes;
using Tablo.Api.Data;
using Tablo.Api.Extensions;
using Tablo.Api.Hubs;
using Tablo.Api.Interfaces;
using Tablo.Api.Services;

namespace Tablo.Api.Controllers;

public record MenuAccessDto(string[]? AllowedMenuIds);

[ApiController]
public class NavigationAccessController(
    ApplicationDbContext db,
    ISubscriptionService subscriptions,
    ISupportConnectionRevoker? revoker = null,
    IOrderConnectionRevoker? orderRevoker = null) : ControllerBase
{
    [Authorize]
    [HttpGet("api/navigation-access")]
    public async Task<ActionResult<MenuAccessDto>> Get()
    {
        if (User.IsInRole("SuperAdmin")) return Ok(new MenuAccessDto(null));
        var employee = await db.Employees.AsNoTracking().Include(e => e.Role)
            .FirstOrDefaultAsync(e => e.Id == User.GetUserId() && e.RestaurantId == User.GetRestaurantId() && e.IsActive);
        return employee == null ? Forbid() : Ok(new MenuAccessDto(NavigationAccess.EffectiveOverride(employee)));
    }

    [Authorize(Roles = "Admin,Owner")]
    [HttpGet("api/employees/{id:guid}/menu-access")]
    public async Task<ActionResult<MenuAccessDto>> GetEmployee(Guid id)
    {
        var employee = await db.Employees.AsNoTracking().Include(e => e.Role)
            .FirstOrDefaultAsync(e => e.Id == id && e.RestaurantId == User.GetRestaurantId());
        return employee == null ? NotFound() : Ok(new MenuAccessDto(NavigationAccess.EffectiveOverride(employee)));
    }

    [Authorize(Roles = "Admin,Owner")]
    [HttpPut("api/employees/{id:guid}/menu-access")]
    public async Task<IActionResult> Put(Guid id, MenuAccessDto request)
    {
        var employee = await db.Employees.Include(e => e.Role)
            .FirstOrDefaultAsync(e => e.Id == id && e.RestaurantId == User.GetRestaurantId());
        if (employee == null) return NotFound();

        var ids = request.AllowedMenuIds?.Distinct(StringComparer.Ordinal).ToArray();
        if (ids != null)
        {
            if (ids.Any(menuId => !NavigationAccess.CanAssign(employee.Role.Name, menuId)))
                return BadRequest(new { message = "Unknown menu or restricted administrator menu cannot be assigned to this role." });
            if (NavigationAccess.IsAdminRole(employee.Role.Name) && !ids.Contains("staff-management", StringComparer.Ordinal))
                return BadRequest(new { message = "Administrators must retain Staff & Roles access." });
            foreach (var menuId in ids)
            {
                var caps = NavigationAccess.Menus[menuId].Capabilities;
                if (caps.Length > 0)
                {
                    var permitted = false;
                    foreach (var cap in caps) permitted |= await subscriptions.HasCapabilityAsync(employee.RestaurantId, cap);
                    if (!permitted) return BadRequest(new { message = "Menu is not available in the restaurant subscription.", menuId });
                }
            }
        }

        employee.AllowedMenuIds = ids;
        employee.AllowedMenuIds = NavigationAccess.EffectiveOverride(employee);
        await db.SaveChangesAsync();

        if (revoker != null && !NavigationAccess.Allows(employee, ["support"]))
        {
            await revoker.RevokeEmployeeConnectionsAsync(employee.Id, "Support menu access revoked.");
        }

        if (orderRevoker != null)
        {
            await orderRevoker.SyncEmployeePermissionsAsync(employee, "Employee menu permissions updated.");
        }

        return Ok(new MenuAccessDto(employee.AllowedMenuIds));
    }
}
