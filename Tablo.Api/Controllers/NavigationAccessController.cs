using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.Extensions;
using Tablo.Api.Hubs;
using Tablo.Api.Interfaces;
using Tablo.Api.Services;
namespace Tablo.Api.Controllers;

public record MenuAccessDto(string[]? AllowedMenuIds);

[ApiController]
[Authorize]
public class NavigationAccessController(ApplicationDbContext db, ISubscriptionService subscriptions, ISupportConnectionRevoker? revoker = null) : ControllerBase
{
    [HttpGet("api/navigation-access")]
    public async Task<IActionResult> Current()
    {
        if (User.IsInRole("SuperAdmin")) return Ok(new MenuAccessDto(null));
        var employee = await db.Employees.AsNoTracking().Include(e => e.Role).FirstOrDefaultAsync(e => e.Id == User.GetUserId() && e.RestaurantId == User.GetRestaurantId() && e.IsActive);
        return employee == null ? Forbid() : Ok(new MenuAccessDto(NavigationAccess.EffectiveOverride(employee)));
    }

    [Authorize(Roles = "Admin,Owner")]
    [HttpGet("api/employees/{id:guid}/menu-access")]
    public async Task<IActionResult> Get(Guid id)
    {
        var employee = await db.Employees.AsNoTracking().Include(e => e.Role).FirstOrDefaultAsync(e => e.Id == id && e.RestaurantId == User.GetRestaurantId());
        return employee == null ? NotFound() : Ok(new MenuAccessDto(NavigationAccess.EffectiveOverride(employee)));
    }

    [Authorize(Roles = "Admin,Owner")]
    [HttpPut("api/employees/{id:guid}/menu-access")]
    public async Task<IActionResult> Put(Guid id, MenuAccessDto request)
    {
        var employee = await db.Employees.Include(e => e.Role).FirstOrDefaultAsync(e => e.Id == id && e.RestaurantId == User.GetRestaurantId());
        if (employee == null) return NotFound();
        var ids = request.AllowedMenuIds?.Distinct(StringComparer.Ordinal).ToArray();
        if (ids != null)
        {
            if (ids.Any(id => !NavigationAccess.RoleAllows(employee.Role.Name, id)))
                return BadRequest(new { message = "Unknown menu or menu outside the employee role." });
            if ((employee.Role.Name is "Admin" or "Owner") && !ids.Contains("staff-management"))
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
        // Older clients may omit the separate access-management menu.
        // Keep administrator recovery available when saving their restrictions.
        employee.AllowedMenuIds = NavigationAccess.EffectiveOverride(employee);
        await db.SaveChangesAsync();
        if (revoker != null && !NavigationAccess.Allows(employee, ["support"]))
        {
            await revoker.RevokeEmployeeConnectionsAsync(employee.Id, "Support menu access revoked.");
        }
        return Ok(new MenuAccessDto(employee.AllowedMenuIds));
    }
}
