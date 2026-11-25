using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tablo.Api.Attributes;
using Tablo.Api.Controllers;
using Tablo.Api.Data;
using Tablo.Api.Entities;
using Tablo.Api.Services;
namespace Tablo.Api.Tests;

public class CashierAccessTests
{
    [Theory]
    [InlineData("ServiceDesk", "Submit", "POST")]
    [InlineData("ServiceDesk", "Transfer", "POST")]
    [InlineData("ServiceDesk", "CreateGroup", "POST")]
    [InlineData("Orders", "UpdateOrderStatus", "PATCH")]
    [InlineData("Settings", "UpdateAcceptingOrders", "PATCH")]
    [InlineData("Employees", "GetEmployees", "GET")]
    public void CashierRoleCannotEscalateThroughModuleMapping(string controller, string action, string method)
    {
        var cashier = new Employee { Role = new Role { Name = "Cashier" } };
        Assert.False(NavigationAccess.Allows(cashier, NavigationAccessFilter.RequiredMenus(controller, action, method)));
        Assert.Equal(new[] { "cashier-billing" }, NavigationAccess.Menus.Keys.Where(id => NavigationAccess.RoleAllows("Cashier", id)));
    }

    [Fact]
    public async Task CashierDefaultsBillOnlyAndRoleChangesResetOldOverrides()
    {
        using var host = new ServiceDeskTestHost();
        await host.SeedAsync();
        var cashierId = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var count = await db.Restaurants.CountAsync();
            // Exercise the existing-database early return: new operational role is
            // created exactly once and seeding does not create demo restaurants.
            db.Roles.Remove(await db.Roles.SingleAsync(r => r.Name == "Cashier"));
            await db.SaveChangesAsync();
            DbSeeder.Seed(db, false);
            DbSeeder.Seed(db, false);
            Assert.Equal(1, await db.Roles.CountAsync(r => r.Name == "Cashier"));
            Assert.Equal(count, await db.Restaurants.CountAsync());
            db.Employees.Add(new Employee { Id = cashierId, RestaurantId = host.Restaurant,
                RoleId = (await db.Roles.SingleAsync(r => r.Name == "Cashier")).Id,
                Email = "cashier@example.test", FirstName = "Cash", LastName = "Desk" });
            await db.SaveChangesAsync();
        }
        using var cashier = host.CreateClient();
        cashier.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token("Cashier", host.Restaurant, cashierId));
        var root = $"/api/restaurants/{host.Restaurant}/service-desk/";
        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync(root + "tables")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cashier.GetAsync(root + $"tables/{Guid.NewGuid()}/bill")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync($"/api/restaurants/{host.Restaurant}/orders")).StatusCode);
        // Missing resources exercise authorization and actor validation without
        // creating or changing bills, payments, or stock.
        var missingBill = Guid.NewGuid();
        var missingTable = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await cashier.PostAsJsonAsync(root + $"tables/{missingTable}/bill/open", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cashier.PostAsJsonAsync(root + $"bills/{missingBill}/payments", new { operationId = Guid.NewGuid(), method = "Cash", amount = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cashier.PostAsJsonAsync(root + $"bills/{missingBill}/settle", new { operationId = Guid.NewGuid(), expectedRevision = new string('0', 64), expectedTotal = 0, paymentMethod = "Cash" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cashier.GetAsync(root + $"bills/{missingBill}/receipt")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cashier.PostAsJsonAsync(root + $"bills/{missingBill}/split-preview", new { operationId = Guid.NewGuid(), expectedRevision = new string('0', 64), amounts = new[] { 1, 1 } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cashier.PostAsJsonAsync(root + $"bills/{missingBill}/split", new { operationId = Guid.NewGuid(), expectedRevision = new string('0', 64), amounts = new[] { 1, 1 } })).StatusCode);

        var effective = await cashier.GetFromJsonAsync<Tablo.Api.DTOs.EffectiveAccessDto>("/api/subscriptions/effective-access");
        Assert.Contains("standard_billing", effective!.Capabilities);
        Assert.DoesNotContain("staff_ordering", effective.Capabilities);
        Assert.DoesNotContain("kitchen_display", effective.Capabilities);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync($"/api/restaurants/{Guid.NewGuid()}/service-desk/tables")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync($"/api/restaurants/{host.Restaurant}/menu/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync("/api/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PostAsJsonAsync(root + "orders", new { operationId = Guid.NewGuid(), tableId = Guid.NewGuid(), items = new[] { new { menuItemId = Guid.NewGuid(), quantity = 1 } } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PutAsJsonAsync($"/api/employees/{cashierId}/menu-access", new MenuAccessDto(null))).StatusCode);
        using var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token("Admin", host.Restaurant, host.AdminActor));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/employees/{cashierId}/menu-access", new MenuAccessDto([]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync(root + "tables")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/employees/{cashierId}", new { firstName = "Cash", lastName = "Desk", email = "cashier@example.test", roleName = "Waiter" })).StatusCode);
        using var finalScope = host.Services.CreateScope();
        Assert.Null((await finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Employees.SingleAsync(e => e.Id == cashierId)).AllowedMenuIds);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync(root + "tables")).StatusCode); // stale role token
    }
}
