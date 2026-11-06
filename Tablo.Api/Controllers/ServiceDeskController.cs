using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Extensions;
using Tablo.Api.Hubs;
using Tablo.Api.Services;

namespace Tablo.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Waiter")]
[Route("api/restaurants/{restaurantId:guid}/service-desk")]
public class ServiceDeskController(ApplicationDbContext db, BillingService billing, StaffOrderingService ordering,
    IHubContext<OrderHub> hub, ILogger<ServiceDeskController> logger) : ControllerBase
{
    [HttpGet("tables")]
    public Task<IActionResult> Tables(Guid restaurantId) => Execute(restaurantId, async () =>
        (object)await db.RestaurantTables.AsNoTracking().Where(t => t.RestaurantId == restaurantId)
            .OrderBy(t => t.TableNumber).Select(t => new ServiceTableDto(t.Id, t.TableNumber, t.IsActive)).ToListAsync());

    [HttpPost("orders")]
    public Task<IActionResult> Submit(Guid restaurantId, StaffOrderRequestDto request) => Execute(restaurantId, async () =>
    {
        var result = await ordering.SubmitAsync(restaurantId, User.GetUserId(), request);
        try
        {
            // A replay returns the original response; notifications must describe current state.
            var currentStatus = await db.Orders.AsNoTracking().Where(o => o.Id == result.OrderId && o.RestaurantId == restaurantId)
                .Select(o => o.Status).SingleOrDefaultAsync();
            await hub.Clients.Group($"restaurant_{restaurantId}").SendAsync("NewOrder", result.OrderId);
            if (currentStatus != null)
                await hub.Clients.Group($"order_{result.OrderId}").SendAsync("OrderStatusUpdated", result.OrderId, currentStatus);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Staff order committed but order notification failed for {OrderId}", result.OrderId); }
        await Notify(restaurantId, request.TableId);
        return result;
    });

    [HttpGet("tables/{tableId:guid}/bill")]
    public Task<IActionResult> Review(Guid restaurantId, Guid tableId) => Execute(restaurantId,
        async () => await billing.ReviewAsync(restaurantId, tableId));

    [HttpGet("bills/{billId:guid}/receipt")]
    public Task<IActionResult> Receipt(Guid restaurantId, Guid billId) => Execute(restaurantId,
        async () => await billing.ReceiptAsync(restaurantId, billId));

    [HttpPost("tables/{tableId:guid}/bill/open")]
    public Task<IActionResult> Open(Guid restaurantId, Guid tableId) => Execute(restaurantId, async () =>
    {
        var result = await billing.OpenAsync(restaurantId, tableId, User.GetUserId());
        await Notify(restaurantId, tableId);
        return result;
    });

    [HttpPost("bills/{billId:guid}/settle")]
    public Task<IActionResult> Settle(Guid restaurantId, Guid billId, SettleBillRequestDto request) => Execute(restaurantId, async () =>
    {
        var result = await billing.SettleAsync(restaurantId, billId, User.GetUserId(), request);
        await Notify(restaurantId, Guid.Empty);
        return result;
    });

    private async Task<IActionResult> Execute(Guid restaurantId, Func<Task<object>> action)
    {
        if (restaurantId == Guid.Empty || User.GetRestaurantId() != restaurantId || User.GetUserId() == Guid.Empty) return Forbid();
        try
        {
            await BillingService.RequireActorAsync(db, restaurantId, User.GetUserId());
            return Ok(await action());
        }
        catch (KeyNotFoundException) { return NotFound(new { message = "The requested restaurant resource was not found." }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ServiceConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { return Conflict(new { message = "Another operation completed first. Refresh and retry using the same operation ID." }); }
    }

    private async Task Notify(Guid restaurantId, Guid tableId)
    {
        try { await hub.Clients.Group($"restaurant_{restaurantId}").SendAsync("BillUpdated", tableId); }
        catch (Exception ex) { logger.LogWarning(ex, "Bill committed but staff notification failed for restaurant {RestaurantId}", restaurantId); }
    }
}
