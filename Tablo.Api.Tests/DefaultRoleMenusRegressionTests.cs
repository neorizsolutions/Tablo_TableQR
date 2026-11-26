using Tablo.Api.Entities;
using Tablo.Api.Services;

namespace Tablo.Api.Tests;

public class DefaultRoleMenusRegressionTests
{
    private static readonly string[] ExpectedWaiterDefaults =
    [
        "overview",
        "orders-servicedesk",
        "staff-order-taking",
        "table-billing"
    ];

    private static readonly string[] ExpectedKitchenDefaults =
    [
        "overview",
        "kitchen-queue"
    ];

    private static readonly string[] ExpectedCashierDefaults =
    [
        "cashier-billing"
    ];

    [Theory]
    [InlineData("Waiter")]
    [InlineData("Kitchen")]
    [InlineData("Cashier")]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public void NullOverrideRetainsExactRoleDefaultSelections(string roleName)
    {
        var employee = new Employee
        {
            Role = new Role { Name = roleName },
            AllowedMenuIds = null
        };

        var actual = NavigationAccess.Menus.Keys
            .Where(id => NavigationAccess.Allows(employee, [id]))
            .ToArray();

        var expected = roleName switch
        {
            "Waiter" => ExpectedWaiterDefaults,
            "Kitchen" => ExpectedKitchenDefaults,
            "Cashier" => ExpectedCashierDefaults,
            "Admin" or "Owner" => NavigationAccess.Menus.Keys.ToArray(),
            _ => throw new InvalidOperationException(roleName)
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EmptyCustomOverrideDeniesOperationalMenusWhileProtectingAdminRecovery()
    {
        foreach (var roleName in new[] { "Waiter", "Kitchen", "Cashier" })
        {
            var emp = new Employee { Role = new Role { Name = roleName }, AllowedMenuIds = [] };
            Assert.Empty(NavigationAccess.Menus.Keys.Where(id => NavigationAccess.Allows(emp, [id])));
        }

        foreach (var roleName in new[] { "Admin", "Owner" })
        {
            var admin = new Employee { Role = new Role { Name = roleName }, AllowedMenuIds = [] };
            var actual = NavigationAccess.Menus.Keys.Where(id => NavigationAccess.Allows(admin, [id])).ToArray();
            Assert.Equal(["staff-management", "staff-menu-access"], actual);
        }
    }
}
