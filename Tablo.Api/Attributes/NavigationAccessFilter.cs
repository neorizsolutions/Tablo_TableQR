using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.Extensions;
using Tablo.Api.Services;
using Tablo.Api.Interfaces;
namespace Tablo.Api.Attributes;

// Runs in addition to existing role, tenant, and subscription checks. Shared read
// resources accept any consuming menu; mutations require their owning module.
public sealed class NavigationAccessFilter(ApplicationDbContext db, ISubscriptionService subscriptions) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true || user.IsInRole("SuperAdmin") ||
            context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()) { await next(); return; }
        var action = (ControllerActionDescriptor)context.ActionDescriptor;
        if (action.ControllerName is "Auth" or "Subscriptions" or "SystemAdmin") { await next(); return; }
        var employee = await db.Employees.AsNoTracking().Include(e => e.Role).FirstOrDefaultAsync(e =>
            e.Id == user.GetUserId() && e.RestaurantId == user.GetRestaurantId() && e.IsActive);
        if (employee == null) { context.Result = new ForbidResult(); return; }
        // Role claims can outlive a role change. Never let a stale elevated token
        // authorize requests after the employee has been demoted.
        if (!user.IsInRole(employee.Role.Name)) { context.Result = new ForbidResult(); return; }
        if (action.ControllerName == "NavigationAccess" || (employee.AllowedMenuIds == null && employee.Role.Name != "Cashier")) { await next(); return; }
        if (employee.Role.Name == "Cashier" && !await subscriptions.HasCapabilityAsync(employee.RestaurantId, "standard_billing"))
        {
            context.Result = new ForbidResult(); return;
        }
        var menus = RequiredMenus(action.ControllerName, action.ActionName, context.HttpContext.Request.Method);
        if (menus.Length == 0 ? true : !NavigationAccess.Allows(employee, menus))
        {
            context.Result = new ObjectResult(new { code = "MENU_ACCESS_DENIED", message = "You do not have access to this module." }) { StatusCode = 403 };
            return;
        }
        await next();
    }

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
            "Employees" => action == "VerifyManagerPin" ? ["orders-servicedesk", "table-billing", "parcel-counter"] : ["staff-management"],
            "AdminMenu" or "Categories" or "Modifiers" => ["menu-management"],
            "MenuItems" => action.Contains("Availability") ? ["menu-management", "kitchen-queue"] : ["menu-management"],
            "Restaurants" => ["menu-management", "staff-order-taking", "orders-servicedesk", "parcel-counter", "kitchen-queue"],
            "Tables" => read ? ["tables-qr", "orders-servicedesk", "staff-order-taking", "table-billing"] : ["tables-qr"],
            "Orders" => read ? ["orders-servicedesk", "staff-order-taking", "kitchen-queue", "table-billing", "overview"] : action.Contains("Status") ? ["kitchen-queue", "orders-servicedesk"] : ["staff-order-taking", "orders-servicedesk", "kitchen-queue"],
            "ServiceRequests" => ["orders-servicedesk"],
            "ServiceDesk" => action == "Submit" || action.Contains("Order") ? ["staff-order-taking"] : action.Contains("Tables") || action.Contains("Transfer") ? ["orders-servicedesk", "staff-order-taking", "table-billing"] : ["table-billing", "orders-servicedesk"],
            "Parcel" => action.Contains("Marketplace") ? ["marketplace-automation"] : ["parcel-counter"],
            "Reports" or "PaymentReports" => ["financial-reports"],
            "Storefront" => ["storefront-settings"],
            "Settings" => read ? ["storefront-settings", "orders-servicedesk", "staff-order-taking", "kitchen-queue", "table-billing", "parcel-counter", "menu-management"] : ["storefront-settings", "menu-management"],
            "KitchenStations" or "Printers" => ["kitchen-queue", "storefront-settings", "menu-management"],
            "CentralKitchen" => ["central-kitchen"],
            "Inventory" => read && action == "GetIngredients" ? ["inventory-stock", "inventory-recipes", "inventory-preparations", "inventory-suppliers", "purchasing-orders", "inventory-stocktakes", "central-kitchen", "stock-valuation-report", "gross-profit-report", "outlet-transfers", "inventory-movements"] : read && action == "GetRecipes" ? ["inventory-recipes", "gross-profit-report", "stock-valuation-report"] : InventoryMenus(action),
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
