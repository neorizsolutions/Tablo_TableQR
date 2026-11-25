using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.Extensions;
using Tablo.Api.Services;

namespace Tablo.Api.Hubs
{
    public class OrderHub(ApplicationDbContext db) : Hub
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

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Owner,Kitchen,Waiter,Cashier")]
        public async Task JoinRestaurantGroup()
        {
            var user = Context.User;
            if (user == null) throw new HubException("Access denied.");
            var employee = await db.Employees.AsNoTracking().Include(e => e.Role).FirstOrDefaultAsync(e =>
                e.Id == user.GetUserId() && e.RestaurantId == user.GetRestaurantId() && e.IsActive);
            if (employee == null || !user.IsInRole(employee.Role.Name) ||
                !NavigationAccess.Allows(employee, ["cashier-billing", "overview", "orders-servicedesk", "staff-order-taking", "kitchen-queue", "table-billing", "parcel-counter"]))
                throw new HubException("Access denied.");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"restaurant_{employee.RestaurantId}");
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Owner,Kitchen,Waiter,Cashier")]
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
