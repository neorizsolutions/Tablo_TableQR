using Microsoft.AspNetCore.SignalR;

namespace Tablo.Api.Hubs
{
    public class OrderHub : Hub
    {
        public async Task JoinOrderGroup(string orderId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"order_{orderId}");
        }

        public async Task LeaveOrderGroup(string orderId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"order_{orderId}");
        }

        public async Task JoinMenuUpdatesGroup(string restaurantId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"menu_{restaurantId}");
        }

        public async Task LeaveMenuUpdatesGroup(string restaurantId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"menu_{restaurantId}");
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Kitchen,Waiter")]
        public async Task JoinRestaurantGroup()
        {
            var restaurantId = Context.User?.FindFirst("RestaurantId")?.Value;
            if (!string.IsNullOrEmpty(restaurantId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"restaurant_{restaurantId}");
            }
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Kitchen,Waiter")]
        public async Task LeaveRestaurantGroup()
        {
            var restaurantId = Context.User?.FindFirst("RestaurantId")?.Value;
            if (!string.IsNullOrEmpty(restaurantId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"restaurant_{restaurantId}");
            }
        }
    }
}
