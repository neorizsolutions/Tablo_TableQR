using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequireCapabilityAttribute : Attribute, IAsyncActionFilter
{
    public string Capability { get; }
    public bool AllowBillSettlement { get; set; } = false;

    public RequireCapabilityAttribute(string capability)
    {
        Capability = capability;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // SuperAdmin has full platform access
        if (user.IsInRole("SuperAdmin"))
        {
            await next();
            return;
        }

        // Extract RestaurantId
        Guid restaurantId = Guid.Empty;
        var claimVal = user.FindFirst("RestaurantId")?.Value;
        if (!string.IsNullOrEmpty(claimVal) && Guid.TryParse(claimVal, out var parsedClaim))
        {
            restaurantId = parsedClaim;
        }
        else if (context.RouteData.Values.TryGetValue("restaurantId", out var routeVal) && routeVal != null && Guid.TryParse(routeVal.ToString(), out var parsedRoute))
        {
            restaurantId = parsedRoute;
        }
        else if (context.HttpContext.Request.Query.TryGetValue("restaurantId", out var queryVal) && Guid.TryParse(queryVal, out var parsedQuery))
        {
            restaurantId = parsedQuery;
        }

        if (restaurantId == Guid.Empty)
        {
            context.Result = new ForbidResult();
            return;
        }

        // Check exception for open bill settlement completion
        if (AllowBillSettlement && context.HttpContext.Request.Path.Value?.Contains("settle", StringComparison.OrdinalIgnoreCase) == true)
        {
            await next();
            return;
        }

        var subscriptionService = context.HttpContext.RequestServices.GetRequiredService<ISubscriptionService>();
        var hasCap = await subscriptionService.HasCapabilityAsync(restaurantId, Capability);

        if (!hasCap)
        {
            context.Result = new ObjectResult(new
            {
                code = "FEATURE_NOT_IN_PLAN",
                feature = Capability,
                message = $"The '{Capability}' feature is not included in your restaurant's current subscription plan. Please upgrade your subscription to access this feature."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}
