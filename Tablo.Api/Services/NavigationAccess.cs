using Tablo.Api.Entities;
namespace Tablo.Api.Services;

public static class NavigationAccess
{
    public record Menu(string[] Roles, string[] Capabilities);
    public static readonly IReadOnlyDictionary<string, Menu> Menus = new Dictionary<string, Menu>(StringComparer.Ordinal)
    {
        ["overview"] = new(new[] { "Admin", "Owner", "Waiter", "Kitchen" }, new string[] {  }),
        ["orders-servicedesk"] = new(new[] { "Admin", "Owner", "Waiter" }, new string[] { "standard_billing" }),
        ["staff-order-taking"] = new(new[] { "Admin", "Owner", "Waiter" }, new string[] { "staff_ordering", "standard_billing" }),
        ["kitchen-queue"] = new(new[] { "Admin", "Owner", "Kitchen" }, new string[] { "kitchen_display" }),
        ["table-billing"] = new(new[] { "Admin", "Owner", "Waiter" }, new string[] { "standard_billing" }),
        ["parcel-counter"] = new(new[] { "Admin", "Owner" }, new string[] { "standard_billing" }),
        ["menu-management"] = new(new[] { "Admin", "Owner" }, new string[] { "menu_management" }),
        ["tables-qr"] = new(new[] { "Admin", "Owner" }, new string[] { "tables_qr" }),
        ["staff-management"] = new(new[] { "Admin", "Owner" }, new string[] {  }),
        ["staff-menu-access"] = new(new[] { "Admin", "Owner" }, new string[] {  }),
        ["crm-loyalty"] = new(new[] { "Admin", "Owner" }, new string[] { "standard_billing" }),
        ["storefront-settings"] = new(new[] { "Admin", "Owner" }, new string[] { "standard_billing" }),
        ["marketplace-automation"] = new(new[] { "Admin", "Owner" }, new string[] { "standard_billing" }),
        ["inventory-stock"] = new(new[] { "Admin", "Owner" }, new string[] { "inventory_core" }),
        ["inventory-recipes"] = new(new[] { "Admin", "Owner" }, new string[] { "recipes_costing" }),
        ["purchasing-orders"] = new(new[] { "Admin", "Owner" }, new string[] { "purchasing" }),
        ["inventory-suppliers"] = new(new[] { "Admin", "Owner" }, new string[] { "purchasing" }),
        ["inventory-preparations"] = new(new[] { "Admin", "Owner" }, new string[] { "preparations" }),
        ["inventory-movements"] = new(new[] { "Admin", "Owner" }, new string[] { "inventory_core" }),
        ["lots-expiry"] = new(new[] { "Admin", "Owner" }, new string[] { "lots_expiry" }),
        ["outlet-transfers"] = new(new[] { "Admin", "Owner" }, new string[] { "outlet_transfers" }),
        ["inventory-stocktakes"] = new(new[] { "Admin", "Owner" }, new string[] { "stocktakes" }),
        ["central-kitchen"] = new(new[] { "Admin", "Owner" }, new string[] { "preparations", "outlet_transfers" }),
        ["financial-reports"] = new(new[] { "Admin", "Owner" }, new string[] { "basic_reports" }),
        ["stock-valuation-report"] = new(new[] { "Admin", "Owner" }, new string[] { "stock_profit_reports" }),
        ["gross-profit-report"] = new(new[] { "Admin", "Owner" }, new string[] { "stock_profit_reports" }),
    };
    public static string[]? EffectiveOverride(Employee employee) => employee.AllowedMenuIds == null ? null :
        employee.Role.Name is "Admin" or "Owner" ? employee.AllowedMenuIds.Concat(["staff-management", "staff-menu-access"]).Distinct().ToArray() : employee.AllowedMenuIds;
    public static bool RoleAllows(string role, string id) => Menus.TryGetValue(id, out var menu) && menu.Roles.Contains(role);
    public static bool Allows(Employee employee, IEnumerable<string> alternatives) => alternatives.Any(id =>
        RoleAllows(employee.Role.Name, id) && (employee.AllowedMenuIds == null || employee.AllowedMenuIds.Contains(id) || (id is "staff-management" or "staff-menu-access" && employee.Role.Name is "Admin" or "Owner")));
}
