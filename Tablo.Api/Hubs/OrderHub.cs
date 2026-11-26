using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.Entities;
using Tablo.Api.Extensions;
using Tablo.Api.Services;

namespace Tablo.Api.Hubs;

public sealed record TrackedOrderConnection(string ConnectionId, Guid EmployeeId, Guid RestaurantId);

public interface IOrderConnectionRevoker
{
    Task SyncEmployeePermissionsAsync(Employee employee, string reason);
}

public sealed class OrderConnectionRegistry(
    IHubContext<OrderHub> hubContext,
    ILogger<OrderConnectionRegistry> logger) : IOrderConnectionRevoker
{
    public static readonly string[] OrderHubMenus =
    [
        "cashier-billing",
        "overview",
        "orders-servicedesk",
        "staff-order-taking",
        "kitchen-queue",
        "table-billing",
        "parcel-counter"
    ];

    private readonly ConcurrentDictionary<string, TrackedOrderConnection> _connections = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _employeeConnections = new();
    private readonly object _indexLock = new();

    public static string EmployeeGroup(Guid employeeId) => $"employee_{employeeId}";

    public void Register(string connectionId, Guid employeeId, Guid restaurantId)
    {
        var tracked = new TrackedOrderConnection(connectionId, employeeId, restaurantId);
        _connections[connectionId] = tracked;
        lock (_indexLock)
        {
            if (!_employeeConnections.TryGetValue(employeeId, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                _employeeConnections[employeeId] = set;
            }

            set.Add(connectionId);
        }
    }

    public void Unregister(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var removed))
        {
            lock (_indexLock)
            {
                if (_employeeConnections.TryGetValue(removed.EmployeeId, out var set))
                {
                    set.Remove(connectionId);
                    if (set.Count == 0)
                    {
                        _employeeConnections.TryRemove(removed.EmployeeId, out _);
                    }
                }
            }
        }
    }

    public async Task SyncEmployeePermissionsAsync(Employee employee, string reason)
    {
        var empGroup = EmployeeGroup(employee.Id);
        var effectiveIds = NavigationAccess.EffectiveOverride(employee);
        await hubContext.Clients.Group(empGroup).SendAsync("PermissionsUpdated", new
        {
            employeeId = employee.Id,
            allowedMenuIds = effectiveIds,
            reason
        });

        if (!employee.IsDeleted && employee.IsActive && NavigationAccess.Allows(employee, OrderHubMenus))
        {
            return;
        }

        List<TrackedOrderConnection> targets;
        lock (_indexLock)
        {
            if (!_employeeConnections.TryGetValue(employee.Id, out var ids) || ids.Count == 0)
            {
                return;
            }

            targets = ids
                .Select(id => _connections.TryGetValue(id, out var conn) ? conn : null)
                .Where(c => c != null)
                .Cast<TrackedOrderConnection>()
                .ToList();
        }

        foreach (var conn in targets)
        {
            try
            {
                await hubContext.Clients.Client(conn.ConnectionId).SendAsync("AccessRevoked", reason);
                await hubContext.Groups.RemoveFromGroupAsync(conn.ConnectionId, $"restaurant_{conn.RestaurantId}");
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed revoking OrderHub stream for connection {ConnectionId}.", conn.ConnectionId);
            }
        }
    }
}

public class OrderHub(ApplicationDbContext db, OrderConnectionRegistry? registry = null) : Hub
{
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        registry?.Unregister(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    // Waiters & Kitchen staff join their restaurant group to receive real-time KOTs
    [Authorize(Roles = "Admin,Owner,Kitchen,Waiter,Cashier")]
    public async Task JoinRestaurantGroup()
    {
        var user = Context.User;
        if (user == null)
        {
            throw new HubException("Unauthorized access to restaurant order stream.");
        }

        var employee = await db.Employees.AsNoTracking().Include(e => e.Role)
            .FirstOrDefaultAsync(e =>
                e.Id == user.GetUserId() &&
                e.RestaurantId == user.GetRestaurantId() &&
                !e.IsDeleted &&
                e.IsActive);

        if (employee == null || !user.IsInRole(employee.Role.Name))
        {
            throw new HubException("Unauthorized access to restaurant order stream.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, OrderConnectionRegistry.EmployeeGroup(employee.Id));
        registry?.Register(Context.ConnectionId, employee.Id, employee.RestaurantId);

        if (!NavigationAccess.Allows(employee, OrderConnectionRegistry.OrderHubMenus))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"restaurant_{employee.RestaurantId}");
            throw new HubException("Menu access denied for restaurant order stream.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"restaurant_{employee.RestaurantId}");
    }

    // Allows any authenticated restaurant employee session to listen for live menu permission updates
    [Authorize(Roles = "Admin,Owner,Kitchen,Waiter,Cashier")]
    public async Task JoinPermissionUpdates()
    {
        var user = Context.User;
        if (user == null)
        {
            throw new HubException("Unauthorized.");
        }

        var employee = await db.Employees.AsNoTracking().Include(e => e.Role)
            .FirstOrDefaultAsync(e =>
                e.Id == user.GetUserId() &&
                e.RestaurantId == user.GetRestaurantId() &&
                !e.IsDeleted &&
                e.IsActive);

        if (employee == null || !user.IsInRole(employee.Role.Name))
        {
            throw new HubException("Unauthorized.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, OrderConnectionRegistry.EmployeeGroup(employee.Id));
        registry?.Register(Context.ConnectionId, employee.Id, employee.RestaurantId);
    }

    // Customers join their specific order group (with tenant claim or QR token scoping)
    public async Task JoinOrderGroup(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId) || !Guid.TryParse(orderId, out _))
        {
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"order_{orderId}");
    }

    public async Task LeaveOrderGroup(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId) || !Guid.TryParse(orderId, out _))
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"order_{orderId}");
    }

    public async Task JoinMenuUpdatesGroup(string restaurantId)
    {
        if (string.IsNullOrWhiteSpace(restaurantId) || !Guid.TryParse(restaurantId, out _))
        {
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"menu_{restaurantId}");
    }

    public async Task LeaveMenuUpdatesGroup(string restaurantId)
    {
        if (string.IsNullOrWhiteSpace(restaurantId) || !Guid.TryParse(restaurantId, out _))
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"menu_{restaurantId}");
    }
}
