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
    }
}
