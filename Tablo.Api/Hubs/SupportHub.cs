using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Extensions;
using Tablo.Api.Services;

namespace Tablo.Api.Hubs
{
    public static class SupportGroups
    {
        public const string SuperAdminInbox = "support_superadmin_inbox";
        public static string Restaurant(Guid restaurantId) => $"support_restaurant_{restaurantId}";
        public static string Conversation(Guid conversationId) => $"support_conv_{conversationId}";
        public static string Employee(Guid employeeId) => $"support_employee_{employeeId}";
    }

    public sealed class SupportTrackedConnection
    {
        public string ConnectionId { get; init; } = string.Empty;
        public Guid? EmployeeId { get; init; }
        public Guid? RestaurantId { get; init; }
        public bool IsSuperAdmin { get; init; }
        public string Role { get; init; } = string.Empty;
        public string? SecurityStamp { get; init; }
        public HubCallerContext Context { get; init; } = null!;
        public ConcurrentDictionary<string, byte> Groups { get; } = new(StringComparer.Ordinal);
    }

    public interface ISupportConnectionRevoker
    {
        Task RevokeEmployeeConnectionsAsync(Guid employeeId, string reason, CancellationToken cancellationToken = default);
        Task RevokeRestaurantConnectionsAsync(Guid restaurantId, string reason, CancellationToken cancellationToken = default);
        Task PruneInvalidConnectionsForRestaurantAsync(Guid restaurantId, ApplicationDbContext db, CancellationToken cancellationToken = default);
        Task PruneAllInvalidConnectionsAsync(ApplicationDbContext db, CancellationToken cancellationToken = default);
    }

    public sealed class SupportConnectionRegistry(
        IServiceProvider serviceProvider,
        ILogger<SupportConnectionRegistry> logger) : ISupportConnectionRevoker
    {
        private readonly ConcurrentDictionary<string, SupportTrackedConnection> _connections = new(StringComparer.Ordinal);

        public int ActiveCount => _connections.Count;

        public void Register(SupportTrackedConnection connection)
        {
            _connections[connection.ConnectionId] = connection;
        }

        public void TrackGroupAdd(string connectionId, string groupName)
        {
            if (_connections.TryGetValue(connectionId, out var conn))
            {
                conn.Groups.TryAdd(groupName, 0);
            }
        }

        public void TrackGroupRemove(string connectionId, string groupName)
        {
            if (_connections.TryGetValue(connectionId, out var conn))
            {
                conn.Groups.TryRemove(groupName, out _);
            }
        }

        public void Unregister(string connectionId)
        {
            _connections.TryRemove(connectionId, out _);
        }

        public async Task RevokeConnectionAsync(string connectionId, string reason, CancellationToken cancellationToken = default)
        {
            if (!_connections.TryRemove(connectionId, out var conn))
                return;

            try
            {
                var hub = serviceProvider.GetService<IHubContext<SupportHub>>();
                if (hub != null)
                {
                    foreach (var groupName in conn.Groups.Keys.ToList())
                    {
                        try
                        {
                            await hub.Groups.RemoveFromGroupAsync(connectionId, groupName, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            logger.LogDebug(ex, "Failed to remove connection {ConnectionId} from group {Group}", connectionId, groupName);
                        }
                    }

                    try
                    {
                        await hub.Clients.Client(connectionId).SendAsync("AccessRevoked", reason, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Failed to notify AccessRevoked to {ConnectionId}", connectionId);
                    }
                }
            }
            finally
            {
                try
                {
                    conn.Context.Abort();
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to abort revoked connection {ConnectionId}", connectionId);
                }
            }
        }

        public async Task RevokeEmployeeConnectionsAsync(Guid employeeId, string reason, CancellationToken cancellationToken = default)
        {
            var targets = _connections.Values
                .Where(c => !c.IsSuperAdmin && c.EmployeeId == employeeId)
                .Select(c => c.ConnectionId)
                .ToList();

            foreach (var connectionId in targets)
            {
                await RevokeConnectionAsync(connectionId, reason, cancellationToken);
            }
        }

        public async Task RevokeRestaurantConnectionsAsync(Guid restaurantId, string reason, CancellationToken cancellationToken = default)
        {
            var targets = _connections.Values
                .Where(c => !c.IsSuperAdmin && c.RestaurantId == restaurantId)
                .Select(c => c.ConnectionId)
                .ToList();

            foreach (var connectionId in targets)
            {
                await RevokeConnectionAsync(connectionId, reason, cancellationToken);
            }
        }

        public Task PruneInvalidConnectionsForRestaurantAsync(Guid restaurantId, ApplicationDbContext db, CancellationToken cancellationToken = default)
        {
            var targets = _connections.Values
                .Where(c => !c.IsSuperAdmin && c.RestaurantId == restaurantId && c.EmployeeId.HasValue)
                .ToList();
            return PruneTargetsAsync(targets, db, cancellationToken);
        }

        public Task PruneAllInvalidConnectionsAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
        {
            var targets = _connections.Values
                .Where(c => !c.IsSuperAdmin && c.EmployeeId.HasValue)
                .ToList();
            return PruneTargetsAsync(targets, db, cancellationToken);
        }

        private async Task PruneTargetsAsync(List<SupportTrackedConnection> targets, ApplicationDbContext db, CancellationToken cancellationToken)
        {
            if (targets.Count == 0)
                return;

            var employeeIds = targets.Select(t => t.EmployeeId!.Value).Distinct().ToList();
            var employees = await db.Employees
                .AsNoTracking()
                .Include(e => e.Role)
                .Where(e => employeeIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, cancellationToken);

            foreach (var conn in targets)
            {
                if (!conn.EmployeeId.HasValue || !employees.TryGetValue(conn.EmployeeId.Value, out var emp))
                {
                    await RevokeConnectionAsync(conn.ConnectionId, "Account not found or removed.", cancellationToken);
                    continue;
                }

                if (emp.RestaurantId != conn.RestaurantId || !emp.IsActive)
                {
                    await RevokeConnectionAsync(conn.ConnectionId, "Account deactivated or disabled.", cancellationToken);
                    continue;
                }

                if (emp.Role == null || emp.Role.Name is not ("Admin" or "Owner") || !string.Equals(emp.Role.Name, conn.Role, StringComparison.OrdinalIgnoreCase))
                {
                    await RevokeConnectionAsync(conn.ConnectionId, "Role permissions changed.", cancellationToken);
                    continue;
                }

                if (!string.IsNullOrEmpty(emp.SecurityStamp) && conn.SecurityStamp != emp.SecurityStamp)
                {
                    await RevokeConnectionAsync(conn.ConnectionId, "Session invalidated.", cancellationToken);
                    continue;
                }

                if (!NavigationAccess.Allows(emp, new[] { "support" }))
                {
                    await RevokeConnectionAsync(conn.ConnectionId, "Support menu access revoked.", cancellationToken);
                }
            }
        }
    }

    public interface ISupportRealtimeNotifier
    {
        Task PublishAsync(SupportRealtimeEventDto evt, CancellationToken cancellationToken = default);
    }

    public sealed class SupportRealtimeNotifier(
        IHubContext<SupportHub> hubContext,
        SupportConnectionRegistry registry,
        ApplicationDbContext db,
        ILogger<SupportRealtimeNotifier> logger) : ISupportRealtimeNotifier
    {
        public async Task PublishAsync(SupportRealtimeEventDto evt, CancellationToken cancellationToken = default)
        {
            try
            {
                // Prune any connected sessions whose access was revoked directly in DB before dispatching sensitive updates.
                await registry.PruneInvalidConnectionsForRestaurantAsync(evt.RestaurantId, db, cancellationToken);

                var tasks = new List<Task>
                {
                    hubContext.Clients.Group(SupportGroups.SuperAdminInbox).SendAsync("SupportEvent", evt, cancellationToken),
                    hubContext.Clients.Group(SupportGroups.Restaurant(evt.RestaurantId)).SendAsync("SupportEvent", evt, cancellationToken),
                    hubContext.Clients.Group(SupportGroups.Conversation(evt.ConversationId)).SendAsync("SupportEvent", evt, cancellationToken)
                };

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Support event {EventType} for conversation {ConversationId} failed to broadcast", evt.EventType, evt.ConversationId);
            }
        }
    }

    [Authorize(Roles = "Admin,Owner,SuperAdmin")]
    public class SupportHub(
        ApplicationDbContext db,
        SupportConnectionRegistry registry) : Hub
    {
        private sealed record CallerValidationResult(bool Authorized, bool IsSuperAdmin, Employee? Employee, string Role, string Reason);

        private async Task<CallerValidationResult> ValidateCallerAsync()
        {
            var user = Context.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return new(false, false, null, "", "Authentication required.");
            }

            if (user.IsInRole("SuperAdmin"))
            {
                var restaurantClaim = user.FindFirst("RestaurantId")?.Value;
                if (!string.IsNullOrWhiteSpace(restaurantClaim) && Guid.TryParse(restaurantClaim, out var parsed) && parsed != Guid.Empty)
                {
                    return new(false, false, null, "", "Invalid platform identity claims.");
                }
                return new(true, true, null, "SuperAdmin", "");
            }

            var sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sub, out var userId) || userId == Guid.Empty)
            {
                return new(false, false, null, "", "Invalid user identity.");
            }

            var restaurantId = user.GetRestaurantId();
            if (restaurantId == Guid.Empty)
            {
                return new(false, false, null, "", "Tenant identity missing.");
            }

            var employee = await db.Employees
                .AsNoTracking()
                .Include(e => e.Role)
                .FirstOrDefaultAsync(e => e.Id == userId && e.RestaurantId == restaurantId, Context.ConnectionAborted);

            if (employee == null || !employee.IsActive)
            {
                return new(false, false, null, "", "Account is inactive or disabled.");
            }

            if (employee.Role == null || employee.Role.Name is not ("Admin" or "Owner") || !user.IsInRole(employee.Role.Name))
            {
                return new(false, false, null, "", "Account role is no longer authorized.");
            }

            var tokenStamp = user.FindFirst("SecurityStamp")?.Value;
            if (!string.IsNullOrEmpty(employee.SecurityStamp) && tokenStamp != employee.SecurityStamp)
            {
                return new(false, false, null, "", "Session has been invalidated.");
            }

            if (!NavigationAccess.Allows(employee, new[] { "support" }))
            {
                return new(false, false, null, "", "Support menu access has been revoked.");
            }

            return new(true, false, employee, employee.Role.Name, "");
        }

        private async Task<CallerValidationResult> RequireValidCallerAsync()
        {
            var result = await ValidateCallerAsync();
            if (!result.Authorized)
            {
                await registry.RevokeConnectionAsync(Context.ConnectionId, result.Reason);
                throw new HubException(result.Reason);
            }
            return result;
        }

        private async Task AddGroupTrackedAsync(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            registry.TrackGroupAdd(Context.ConnectionId, groupName);
        }

        private async Task RemoveGroupTrackedAsync(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            registry.TrackGroupRemove(Context.ConnectionId, groupName);
        }

        public override async Task OnConnectedAsync()
        {
            var result = await ValidateCallerAsync();
            if (!result.Authorized)
            {
                try
                {
                    await Clients.Caller.SendAsync("AccessRevoked", result.Reason);
                }
                catch
                {
                    // Best effort notice before aborting.
                }
                Context.Abort();
                return;
            }

            var tracked = new SupportTrackedConnection
            {
                ConnectionId = Context.ConnectionId,
                IsSuperAdmin = result.IsSuperAdmin,
                EmployeeId = result.Employee?.Id,
                RestaurantId = result.Employee?.RestaurantId,
                Role = result.Role,
                SecurityStamp = result.Employee?.SecurityStamp,
                Context = Context
            };

            registry.Register(tracked);

            if (result.IsSuperAdmin)
            {
                await AddGroupTrackedAsync(SupportGroups.SuperAdminInbox);
            }
            else if (result.Employee != null)
            {
                await AddGroupTrackedAsync(SupportGroups.Restaurant(result.Employee.RestaurantId));
                await AddGroupTrackedAsync(SupportGroups.Employee(result.Employee.Id));
            }

            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            registry.Unregister(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        public async Task JoinSuperAdminInbox()
        {
            var caller = await RequireValidCallerAsync();
            if (!caller.IsSuperAdmin)
            {
                throw new HubException("Access denied: Super Admin role required.");
            }

            await AddGroupTrackedAsync(SupportGroups.SuperAdminInbox);
        }

        public async Task JoinRestaurantSupport(Guid? restaurantId = null)
        {
            var caller = await RequireValidCallerAsync();
            if (caller.IsSuperAdmin || caller.Employee == null)
            {
                throw new HubException("Access denied: Restaurant administrator role required.");
            }

            if (restaurantId.HasValue && restaurantId.Value != caller.Employee.RestaurantId)
            {
                throw new HubException("Access denied: Foreign restaurant subscription is prohibited.");
            }

            await AddGroupTrackedAsync(SupportGroups.Restaurant(caller.Employee.RestaurantId));
        }

        public async Task JoinConversation(Guid conversationId)
        {
            var caller = await RequireValidCallerAsync();

            if (caller.IsSuperAdmin)
            {
                var exists = await db.SupportConversations.AsNoTracking().AnyAsync(c => c.Id == conversationId);
                if (!exists)
                {
                    throw new HubException("Conversation not found.");
                }
            }
            else
            {
                var ownsConversation = await db.SupportConversations.AsNoTracking()
                    .AnyAsync(c => c.Id == conversationId && c.RestaurantId == caller.Employee!.RestaurantId);
                if (!ownsConversation)
                {
                    throw new HubException("Access denied: Conversation not found for your restaurant.");
                }
            }

            await AddGroupTrackedAsync(SupportGroups.Conversation(conversationId));
        }

        public async Task LeaveConversation(Guid conversationId)
        {
            await RemoveGroupTrackedAsync(SupportGroups.Conversation(conversationId));
        }

        public async Task Ping()
        {
            await RequireValidCallerAsync();
        }
    }
}
