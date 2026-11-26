using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.Extensions;
using Tablo.Api.Interfaces;
using Tablo.Api.Services;

namespace Tablo.Api.Attributes;

public sealed class NavigationAccessFilter(ApplicationDbContext db, ISubscriptionService subscriptions) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true || user.IsInRole("SuperAdmin") ||
            context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()) { await next(); return; }

        var action = (ControllerActionDescriptor)context.ActionDescriptor;
        if (action.ControllerName is "Auth" or "Subscriptions" or "SystemAdmin" or "SupportAdmin") { await next(); return; }

        var employee = await db.Employees.AsNoTracking().Include(e => e.Role).FirstOrDefaultAsync(e =>
            e.Id == user.GetUserId() && e.RestaurantId == user.GetRestaurantId() && e.IsActive);
        if (employee == null) { context.Result = new ForbidResult(); return; }
        if (!user.IsInRole(employee.Role.Name)) { context.Result = new ForbidResult(); return; }

        if (action.ControllerName == "NavigationAccess" && action.ActionName == "Get")
        {
            await next();
            return;
        }

        // Public storefront or customer QR endpoints do not require staff workspace navigation menus
        if (IsPublicCustomerEndpoint(action.ControllerName, action.ActionName))
        {
            await next();
            return;
        }

        var menus = RequiredMenus(action.ControllerName, action.ActionName, context.HttpContext.Request.Method);
        var matchingMenus = menus.Where(id => NavigationAccess.Allows(employee, [id])).ToArray();
        if (matchingMenus.Length == 0)
        {
            context.Result = new ObjectResult(new { code = "MENU_ACCESS_DENIED", message = "You do not have access to this menu." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        var entitled = false;
        foreach (var menuId in matchingMenus)
        {
            if (NavigationAccess.Menus.TryGetValue(menuId, out var menuDef))
            {
                if (menuDef.Capabilities.Length == 0)
                {
                    entitled = true;
                    break;
                }

                foreach (var cap in menuDef.Capabilities)
                {
                    if (await subscriptions.HasCapabilityAsync(employee.RestaurantId, cap))
                    {
                        entitled = true;
                        break;
                    }
                }
            }

            if (entitled) break;
        }

        if (!entitled)
        {
            context.Result = new ObjectResult(new
            {
                code = "FEATURE_NOT_IN_PLAN",
                message = "This workspace feature is not included in your restaurant's current subscription plan."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }

    private static bool IsPublicCustomerEndpoint(string controller, string action) => controller switch
    {
        "QR" => true,
        "Storefront" => action != "UpdateOutletSettings",
        "Orders" => action is "CreateOrder" or "GetActiveOrderByQr",
        "ServiceRequests" => action.Contains("Qr", StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    public static string[] RequiredMenus(string controller, string action, string method)
    {
        var regular = ModuleMenus(controller, action, method);
        return CashierSupports(controller, action, method) ? regular.Append("cashier-billing").ToArray() : regular;
    }

    public static bool CashierSupports(string controller, string action, string method) => controller switch
    {
        "ServiceDesk" => action is "Tables" or "Review" or "Groups" or "Pay" or "PreviewSplit" or "Split" or "Receipt" or "Open" or "Settle",
        "Orders" => method == "GET",
        "Tables" => method == "GET" && action == "GetTables",
        "Settings" => method == "GET" && action == "GetSettings",
        _ => false
    };

    private static string[] ModuleMenus(string controller, string action, string method)
    {
        var read = method == "GET";
        return controller switch
        {
            "NavigationAccess" => ["staff-menu-access"],
            "Employees" => action == "VerifyManagerPin"
                ? ["orders-servicedesk", "table-billing", "parcel-counter", "kitchen-queue"]
                : ["staff-management"],
            "Categories" or "Modifiers" => ["menu-management"],
            "MenuItems" => action.Contains("Availability") ? ["menu-management", "kitchen-queue"] : ["menu-management"],
            "Restaurants" => ["menu-management", "staff-order-taking", "orders-servicedesk", "parcel-counter", "kitchen-queue", "cashier-billing"],
            "Tables" => action == "GetTables" && read
                ? ["tables-qr", "orders-servicedesk", "staff-order-taking", "table-billing", "overview", "financial-reports"]
                : ["tables-qr"],
            "Orders" => read
                ? ["orders-servicedesk", "staff-order-taking", "kitchen-queue", "table-billing", "overview", "parcel-counter"]
                : action.Contains("Status")
                    ? ["kitchen-queue", "orders-servicedesk"]
                    : ["staff-order-taking", "orders-servicedesk", "kitchen-queue"],
            "ServiceRequests" => read ? ["orders-servicedesk", "overview"] : ["orders-servicedesk"],
            "ServiceDesk" => action == "Submit" || action.Contains("Order")
                ? ["staff-order-taking"]
                : action == "Tables"
                    ? ["orders-servicedesk", "staff-order-taking", "table-billing", "financial-reports"]
                    : action.Contains("Transfer")
                        ? ["orders-servicedesk", "staff-order-taking", "table-billing"]
                        : ["table-billing", "orders-servicedesk"],
            "AdminMenu" => read ? ["menu-management", "parcel-counter"] : ["menu-management"],
            "Parcel" => action.Contains("Marketplace")
                ? ["marketplace-automation"]
                : action == "UpdateSettings"
                    ? ["parcel-counter", "storefront-settings"]
                    : ["parcel-counter"],
            "Reports" or "PaymentReports" => ["financial-reports"],
            "Storefront" => ["storefront-settings"],
            "Settings" => read
                ? ["storefront-settings", "orders-servicedesk", "staff-order-taking", "kitchen-queue", "table-billing", "parcel-counter", "menu-management"]
                : ["storefront-settings", "menu-management"],
            "KitchenStations" or "Printers" => ["kitchen-queue", "storefront-settings", "menu-management"],
            "CentralKitchen" => ["central-kitchen", "outlet-transfers"],
            "Support" => ["support"],
            "Inventory" => read && action == "GetIngredients"
                ? ["inventory-stock", "inventory-recipes", "inventory-preparations", "inventory-suppliers", "purchasing-orders", "inventory-stocktakes", "lots-expiry", "central-kitchen", "stock-valuation-report", "gross-profit-report", "outlet-transfers", "inventory-movements"]
                : read && action == "GetRecipes"
                    ? ["inventory-recipes", "gross-profit-report", "stock-valuation-report"]
                    : read && (action == "GetInventorySettings" || action == "GetOutlets" || action == "GetMenuAvailability")
                        ? ["inventory-stock", "inventory-recipes", "inventory-preparations", "inventory-suppliers", "purchasing-orders", "inventory-stocktakes", "lots-expiry", "central-kitchen", "stock-valuation-report", "gross-profit-report", "outlet-transfers", "inventory-movements"]
                        : InventoryMenus(action),
            _ => []
        };
    }

    private static string[] InventoryMenus(string action) => action switch
    {
        var a when a.Contains("GrossProfit") => ["gross-profit-report"],
        var a when a.Contains("StockReport") || a.Contains("StockValuation") || a.Contains("ExportStock") => ["stock-valuation-report"],
        var a when a.Contains("Stocktake") || a == "SaveDraft" => ["inventory-stocktakes"],
        var a when a.Contains("Supplier") || a.Contains("PriceComparison") || a == "CompareIngredientPrices" => ["inventory-suppliers", "purchasing-orders"],
        var a when a.Contains("Purchase") || a.Contains("Invoice") || a.Contains("GoodsReceipt") || a == "ReceiveGoods" => ["purchasing-orders"],
        var a when a.Contains("Preparation") => ["inventory-preparations"],
        var a when a.Contains("Recipe") => ["inventory-recipes"],
        var a when a.Contains("Lot") => ["lots-expiry"],
        var a when a.Contains("Transfer") || a.Contains("Outlet") || a.Contains("Shortage") => ["outlet-transfers"],
        var a when a.Contains("Movement") => ["inventory-movements", "inventory-stock"],
        _ => ["inventory-stock"]
    };
}
