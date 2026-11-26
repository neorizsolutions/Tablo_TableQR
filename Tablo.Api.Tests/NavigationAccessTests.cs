using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tablo.Api.Controllers;
using Tablo.Api.Data;
using Tablo.Api.Attributes;
using Tablo.Api.Services;
namespace Tablo.Api.Tests;

public class NavigationAccessTests
{
    [Fact]
    public void RegistryAndModuleMappingsCannotGrantOutsideRole()
    {
        Assert.False(NavigationAccess.RoleAllows("Waiter", "staff-management"));
        Assert.False(NavigationAccess.RoleAllows("Kitchen", "table-billing"));
        Assert.False(NavigationAccess.RoleAllows("Admin", "sa-tenants"));
        Assert.True(NavigationAccess.RoleAllows("Admin", "staff-menu-access"));
        Assert.False(NavigationAccess.RoleAllows("Waiter", "staff-menu-access"));
        Assert.True(NavigationAccess.CanAssign("Waiter", "kitchen-queue"));
        Assert.True(NavigationAccess.CanAssign("Kitchen", "cashier-billing"));
        Assert.True(NavigationAccess.CanAssign("Cashier", "inventory-stock"));
        Assert.False(NavigationAccess.CanAssign("Waiter", "staff-management"));
        Assert.False(NavigationAccess.CanAssign("Waiter", "staff-menu-access"));
        Assert.False(NavigationAccess.CanAssign("Waiter", "support"));
        Assert.False(NavigationAccess.CanAssign("Kitchen", "support"));
        Assert.False(NavigationAccess.CanAssign("Cashier", "staff-menu-access"));
        Assert.False(NavigationAccess.CanAssign("Admin", "sa-tenants"));
        var administrator = new Tablo.Api.Entities.Employee
        {
            Role = new Tablo.Api.Entities.Role { Name = "Admin" },
            AllowedMenuIds = ["staff-management"]
        };
        Assert.Contains("staff-menu-access", NavigationAccess.EffectiveOverride(administrator)!);
        Assert.Contains("staff-order-taking", NavigationAccessFilter.RequiredMenus("ServiceDesk", "Submit", "POST"));
        Assert.Equal(new[] { "inventory-stocktakes" }, NavigationAccessFilter.RequiredMenus("Inventory", "SaveDraft", "PUT"));
        Assert.Equal(new[] { "gross-profit-report" }, NavigationAccessFilter.RequiredMenus("Inventory", "GetSalesGrossProfitReport", "GET"));
    }

    [Fact]
    public async Task TenantScopedPersistenceResetAndImmediateRevocation()
    {
        using var host = new ServiceDeskTestHost();
        await host.SeedAsync();
        using var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token("Admin", host.Restaurant, host.AdminActor));
        using var staff = host.CreateClient();
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token("Waiter", host.Restaurant));
        var target = $"/api/employees/{host.Actor}/menu-access";
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(target, new MenuAccessDto([]))).StatusCode);
        var result = await staff.GetFromJsonAsync<MenuAccessDto>("/api/navigation-access");
        Assert.Empty(result!.AllowedMenuIds!);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync($"/api/restaurants/{host.Restaurant}/service-desk/tables")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(target, new MenuAccessDto(["overview"]))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/api/restaurants/{host.Restaurant}/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync($"/api/restaurants/{host.Restaurant}/service-desk/tables")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(target, new MenuAccessDto(["staff-management"]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(target, new MenuAccessDto(["staff-menu-access"]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(target, new MenuAccessDto(["support"]))).StatusCode);
        // Cross-role assignment: Waiter with default null cannot update kitchen/inventory,
        // but once Waiter is granted kitchen-queue / inventory-stock, cross-role routes succeed.
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(target, new MenuAccessDto(["kitchen-queue", "inventory-stock"]))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/api/restaurants/{host.Restaurant}/inventory/ingredients")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync($"/api/restaurants/{host.Restaurant}/service-desk/tables")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/employees")).StatusCode);
        using (var tenantScope = host.Services.CreateScope())
        {
            var tenantDb = tenantScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var role = await tenantDb.Roles.SingleAsync(r => r.Name == "Waiter");
            var foreign = new Tablo.Api.Entities.Employee { Id = Guid.NewGuid(), RestaurantId = Guid.NewGuid(), RoleId = role.Id, Email = "foreign@example.test" };
            tenantDb.Employees.Add(foreign);
            await tenantDb.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/employees/{foreign.Id}/menu-access")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/employees/{foreign.Id}/menu-access", new MenuAccessDto([]))).StatusCode);
            Assert.Null((await tenantDb.Employees.AsNoTracking().SingleAsync(e => e.Id == foreign.Id)).AllowedMenuIds);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/employees/{host.AdminActor}/menu-access", new MenuAccessDto([]))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/employees/{Guid.NewGuid()}/menu-access", new MenuAccessDto([]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PutAsJsonAsync(target, new MenuAccessDto(null))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(target, new MenuAccessDto(null))).StatusCode);
        Assert.Null((await staff.GetFromJsonAsync<MenuAccessDto>("/api/navigation-access"))!.AllowedMenuIds);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/api/restaurants/{host.Restaurant}/service-desk/tables")).StatusCode);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var employee = await db.Employees.SingleAsync(e => e.Id == host.Actor);
        employee.IsActive = false;
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/navigation-access")).StatusCode);
    }
}
