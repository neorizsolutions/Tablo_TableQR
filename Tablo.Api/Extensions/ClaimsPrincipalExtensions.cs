using System.Security.Claims;

namespace Tablo.Api.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetRestaurantId(this ClaimsPrincipal user)
        {
            var claim = user.FindFirst("RestaurantId")?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }
}
