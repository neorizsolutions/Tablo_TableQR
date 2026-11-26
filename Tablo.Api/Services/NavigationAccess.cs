using Tablo.Api.Constants;
using Tablo.Api.Entities;

namespace Tablo.Api.Services;

public static class NavigationAccess
{
    public record Menu(string[] Roles, string[] Capabilities, bool AdminOnly = false);

    public static readonly IReadOnlyDictionary<string, Menu> Menus = new Dictionary<string, Menu>(StringComparer.Ordinal)
    {
        ["cashier-billing"] = new(new[] { "Admin", "Owner", "Cashier" }, new[] { Capabilities.StandardBilling }),
        ["overview"] = new(new[] { "Admin", "Owner", "Waiter", "Kitchen" }, Array.Empty<string>()),
        ["orders-servicedesk"] = new(new[] { "Admin", "Owner", "Waiter" }, new[] { Capabilities.StandardBilling }),
        ["staff-order-taking"] = new(new[] { "Admin", "Owner", "Waiter" }, new[] { Capabilities.StaffOrdering, Capabilities.StandardBilling }),
        ["kitchen-queue"] = new(new[] { "Admin", "Owner", "Kitchen" }, new[] { Capabilities.KitchenDisplay }),
        ["table-billing"] = new(new[] { "Admin", "Owner", "Waiter" }, new[] { Capabilities.StandardBilling }),
        ["parcel-counter"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.StandardBilling }),
        ["menu-management"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.MenuManagement }),
        ["tables-qr"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.TablesAndQr }),
        ["staff-management"] = new(new[] { "Admin", "Owner" }, Array.Empty<string>(), AdminOnly: true),
        ["staff-menu-access"] = new(new[] { "Admin", "Owner" }, Array.Empty<string>(), AdminOnly: true),
        ["crm-loyalty"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.StandardBilling }),
        ["storefront-settings"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.StandardBilling }),
        ["marketplace-automation"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.StandardBilling }),
        ["inventory-stock"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.InventoryCore }),
        ["inventory-recipes"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.RecipesCosting }),
        ["purchasing-orders"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.Purchasing }),
        ["inventory-suppliers"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.Purchasing }),
        ["inventory-preparations"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.Preparations }),
        ["inventory-movements"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.InventoryCore }),
        ["lots-expiry"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.LotsAndExpiry }),
        ["outlet-transfers"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.OutletTransfers }),
        ["inventory-stocktakes"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.Stocktakes }),
        ["central-kitchen"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.Preparations, Capabilities.OutletTransfers }),
        ["financial-reports"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.BasicReports }),
        ["stock-valuation-report"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.StockProfitReports }),
        ["gross-profit-report"] = new(new[] { "Admin", "Owner" }, new[] { Capabilities.StockProfitReports }),
        ["support"] = new(new[] { "Admin", "Owner" }, Array.Empty<string>(), AdminOnly: true),
    };

    private static readonly IReadOnlyDictionary<string, string[]> WorkspaceCapabilities =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["cashier-billing"] =
            [
                Capabilities.StandardBilling,
                Capabilities.SplitBilling,
                Capabilities.MixedPayments,
                Capabilities.ThermalPrinting,
                Capabilities.DynamicUpiQr,
                Capabilities.TablesAndQr,
            ],
            ["overview"] = Array.Empty<string>(),
            ["orders-servicedesk"] =
            [
                Capabilities.QrOrdering,
                Capabilities.StandardBilling,
                Capabilities.DiningGroups,
                Capabilities.TableTransfers,
                Capabilities.FreeRefills,
                Capabilities.ManagerWasteCancellation,
                Capabilities.HoldFireCourses,
                Capabilities.TablesAndQr,
            ],
            ["staff-order-taking"] =
            [
                Capabilities.StandardBilling,
                Capabilities.StaffOrdering,
                Capabilities.DiningGroups,
                Capabilities.ItemModifiers,
                Capabilities.TablesAndQr,
            ],
            ["kitchen-queue"] =
            [
                Capabilities.KitchenDisplay,
                Capabilities.ThermalPrinting,
                Capabilities.MultipleKitchenStations,
                Capabilities.HoldFireCourses,
                Capabilities.Preparations,
                Capabilities.AutoAvailability,
            ],
            ["table-billing"] =
            [
                Capabilities.StandardBilling,
                Capabilities.DiningGroups,
                Capabilities.SplitBilling,
                Capabilities.MixedPayments,
                Capabilities.FreeRefills,
                Capabilities.DynamicUpiQr,
                Capabilities.ThermalPrinting,
            ],
            ["parcel-counter"] =
            [
                Capabilities.StandardBilling,
                Capabilities.ParcelCounter,
                Capabilities.ScheduledPickup,
                Capabilities.HybridDineInParcel,
                Capabilities.CarSideOrdering,
                Capabilities.DynamicUpiQr,
                Capabilities.ThermalPrinting,
            ],
            ["menu-management"] =
            [
                Capabilities.MenuManagement,
                Capabilities.ItemModifiers,
                Capabilities.AutoAvailability,
                Capabilities.ThermalPrinting,
                Capabilities.MultipleKitchenStations,
            ],
            ["tables-qr"] =
            [
                Capabilities.TablesAndQr,
                Capabilities.QrOrdering,
            ],
            ["crm-loyalty"] = [Capabilities.StandardBilling],
            ["storefront-settings"] =
            [
                Capabilities.StandardBilling,
                Capabilities.ThermalPrinting,
                Capabilities.MultipleKitchenStations,
                Capabilities.DynamicUpiQr,
                Capabilities.ScheduledPickup,
                Capabilities.CarSideOrdering,
                Capabilities.HybridDineInParcel,
            ],
            ["marketplace-automation"] =
            [
                Capabilities.StandardBilling,
                Capabilities.ParcelCounter,
            ],
            ["inventory-stock"] = [Capabilities.InventoryCore, Capabilities.AutoAvailability],
            ["inventory-recipes"] = [Capabilities.InventoryCore, Capabilities.RecipesCosting],
            ["purchasing-orders"] = [Capabilities.InventoryCore, Capabilities.Purchasing],
            ["inventory-suppliers"] = [Capabilities.InventoryCore, Capabilities.Purchasing],
            ["inventory-preparations"] = [Capabilities.InventoryCore, Capabilities.Preparations],
            ["inventory-movements"] = [Capabilities.InventoryCore],
            ["lots-expiry"] = [Capabilities.InventoryCore, Capabilities.LotsAndExpiry],
            ["outlet-transfers"] = [Capabilities.InventoryCore, Capabilities.OutletTransfers],
            ["inventory-stocktakes"] = [Capabilities.InventoryCore, Capabilities.Stocktakes],
            ["central-kitchen"] = [Capabilities.InventoryCore, Capabilities.Preparations, Capabilities.OutletTransfers],
            ["financial-reports"] = [Capabilities.BasicReports, Capabilities.AdvancedPaymentReports],
            ["stock-valuation-report"] = [Capabilities.InventoryCore, Capabilities.RecipesCosting, Capabilities.StockProfitReports],
            ["gross-profit-report"] = [Capabilities.InventoryCore, Capabilities.RecipesCosting, Capabilities.StockProfitReports],
        };

    public static bool IsAdminRole(string? role) =>
        string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase);

    public static bool IsRecoveryProtected(string? role, string id) =>
        IsAdminRole(role) && id is "staff-management" or "staff-menu-access";

    public static string[]? EffectiveOverride(Employee employee)
    {
        if (employee.AllowedMenuIds == null) return null;

        var role = employee.Role?.Name ?? string.Empty;
        var filtered = employee.AllowedMenuIds
            .Where(id => CanAssign(role, id))
            .Distinct(StringComparer.Ordinal);

        return IsAdminRole(role)
            ? filtered.Concat(["staff-management", "staff-menu-access"]).Distinct(StringComparer.Ordinal).ToArray()
            : filtered.ToArray();
    }

    public static bool RoleAllows(string role, string id) =>
        Menus.TryGetValue(id, out var menu) &&
        menu.Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));

    public static bool CanAssign(string role, string id) =>
        Menus.TryGetValue(id, out var menu) && (!menu.AdminOnly || IsAdminRole(role));

    public static bool Allows(Employee employee, IEnumerable<string> alternatives)
    {
        var role = employee.Role?.Name ?? string.Empty;
        if (employee.AllowedMenuIds == null)
        {
            return alternatives.Any(id => RoleAllows(role, id));
        }

        return alternatives.Any(id =>
            IsRecoveryProtected(role, id) ||
            (CanAssign(role, id) && employee.AllowedMenuIds.Contains(id, StringComparer.Ordinal)));
    }

    public static HashSet<string>? WorkspaceCapabilitiesForMenus(Employee employee)
    {
        if (employee.AllowedMenuIds == null) return null;

        var role = employee.Role?.Name ?? string.Empty;
        if (IsAdminRole(role))
        {
            return new HashSet<string>(Capabilities.PremiumDefaults, StringComparer.OrdinalIgnoreCase);
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in Menus.Keys)
        {
            if (!Allows(employee, [id])) continue;
            if (WorkspaceCapabilities.TryGetValue(id, out var caps))
            {
                foreach (var cap in caps)
                {
                    result.Add(cap);
                }
            }
        }

        return result;
    }
}
