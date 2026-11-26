using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Attributes;
using Tablo.Api.Constants;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Extensions;
using Tablo.Api.Hubs;
using Tablo.Api.Services;

namespace Tablo.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Owner,Waiter,Cashier,Kitchen")]
[Route("api/restaurants/{restaurantId:guid}/service-desk")]
public class ServiceDeskController(ApplicationDbContext db, BillingService billing, StaffOrderingService ordering,
    IHubContext<OrderHub> hub, ILogger<ServiceDeskController> logger, Tablo.Api.Services.Printing.IKotRoutingService? kotRouting = null) : ControllerBase
{
    [HttpGet("tables")]
    public Task<IActionResult> Tables(Guid restaurantId) => Execute(restaurantId, async () =>
        (object)await db.RestaurantTables.AsNoTracking().Where(t => t.RestaurantId == restaurantId)
            .OrderBy(t => t.TableNumber).Select(t => new ServiceTableDto(t.Id, t.TableNumber, t.IsActive)).ToListAsync());

    [HttpPost("orders")]
    [RequireCapability(Capabilities.StaffOrdering)]
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
    public Task<IActionResult> Review(Guid restaurantId, Guid tableId, [FromQuery] Guid? diningGroupId=null) => Execute(restaurantId,
        async () => await billing.ReviewAsync(restaurantId, tableId,diningGroupId));

    [HttpGet("tables/{tableId:guid}/groups")]
    public Task<IActionResult> Groups(Guid restaurantId,Guid tableId)=>Execute(restaurantId,async()=>await new DiningGroupService(db).ListAsync(restaurantId,tableId));

    public record CreateGroupRequest(Guid OperationId,string Label);
    public record CloseGroupRequest(Guid OperationId);
    [HttpPost("tables/{tableId:guid}/groups/{groupId:guid}/close")]
    public Task<IActionResult> CloseGroup(Guid restaurantId,Guid tableId,Guid groupId,CloseGroupRequest request)=>Execute(restaurantId,async()=>{
        if(request.OperationId==Guid.Empty)throw new ArgumentException("Operation ID is required.");
        await using var tx=await TableOrderTransaction.BeginAsync(db);await TableOrderTransaction.LockAsync(db,restaurantId,tableId);
        var hash=BillingService.Hash($"Close:{User.GetUserId()}:{tableId}:{groupId}");
        var old=await db.FloorOperations.SingleOrDefaultAsync(o=>o.RestaurantId==restaurantId && o.OperationId==request.OperationId);
        if(old!=null){if(old.Hash!=hash)throw new ServiceConflictException("Operation ID used for another action.");return new{closed=true};}
        var group=await db.DiningGroups.SingleOrDefaultAsync(g=>g.RestaurantId==restaurantId && g.TableId==tableId && g.Id==groupId && g.ClosedAtUtc==null)??throw new KeyNotFoundException();
        if(await db.TableBills.AnyAsync(b=>b.RestaurantId==restaurantId && b.DiningGroupId==groupId && b.SettledAtUtc==null && b.SplitAtUtc==null) || await db.Orders.AnyAsync(o=>o.RestaurantId==restaurantId && o.DiningGroupId==groupId && o.Status!="Served" && o.Status!="Cancelled" && o.Status!="Rejected"))throw new ServiceConflictException("Settle all bills and finish active food before closing the group.");
        group.ClosedAtUtc=DateTime.UtcNow;db.FloorOperations.Add(new(){RestaurantId=restaurantId,ActorEmployeeId=User.GetUserId(),OperationId=request.OperationId,Hash=hash,Kind="Close",ResultJson="{\"closed\":true}"});
        await db.SaveChangesAsync();if(tx!=null)await tx.CommitAsync();await Notify(restaurantId,tableId);return new{closed=true};
    });
    [HttpPost("tables/{tableId:guid}/groups")]
    [RequireCapability(Capabilities.DiningGroups)]
    public Task<IActionResult> CreateGroup(Guid restaurantId,Guid tableId,CreateGroupRequest request)=>Execute(restaurantId,async()=>{
        var result=await new DiningGroupService(db).CreateAsync(restaurantId,User.GetUserId(),tableId,request.Label,request.OperationId);await Notify(restaurantId,tableId);return result;
    });

    [HttpPost("bills/{billId:guid}/payments")]
    public Task<IActionResult> Pay(Guid restaurantId,Guid billId,RecordBillPayment request)=>Execute(restaurantId,async()=>{
        var result=await new BillPaymentService(db).RecordAsync(restaurantId,User.GetUserId(),billId,request);await Notify(restaurantId,Guid.Empty);return result;
    });

    [HttpPost("bills/{billId:guid}/split-preview")]
    [RequireCapability(Capabilities.SplitBilling)]
    public Task<IActionResult> PreviewSplit(Guid restaurantId,Guid billId,SplitBillRequest request)=>Execute(restaurantId,async()=>await new FloorOperationsService(db).SplitAsync(restaurantId,User.GetUserId(),billId,request,true));

    [HttpPost("bills/{billId:guid}/split")]
    [RequireCapability(Capabilities.SplitBilling)]
    public Task<IActionResult> Split(Guid restaurantId,Guid billId,SplitBillRequest request)=>Execute(restaurantId,async()=>{
        var result=await new FloorOperationsService(db).SplitAsync(restaurantId,User.GetUserId(),billId,request);await Notify(restaurantId,Guid.Empty);return result;
    });

    [HttpPost("tables/{tableId:guid}/transfer")]
    [RequireCapability(Capabilities.TableTransfers)]
    public Task<IActionResult> Transfer(Guid restaurantId,Guid tableId,TransferGroupRequest request)=>Execute(restaurantId,async()=>{
        var result=await new FloorOperationsService(db).TransferAsync(restaurantId,User.GetUserId(),tableId,request);await Notify(restaurantId,Guid.Empty);
        var srcTable = await db.RestaurantTables.FindAsync(result.SourceTableId);
        var destTable = await db.RestaurantTables.FindAsync(result.DestinationTableId);
        var group = result.DiningGroupId.HasValue ? await db.DiningGroups.FindAsync(result.DiningGroupId.Value) : null;
        var orders=await db.Orders.AsNoTracking().Where(o=>o.RestaurantId==restaurantId && o.TableId==result.DestinationTableId && o.DiningGroupId==result.DiningGroupId).Select(o=>new{o.Id,o.Status}).ToListAsync();
        foreach(var order in orders)
        {
            try{await hub.Clients.Group($"order_{order.Id}").SendAsync("OrderStatusUpdated",order.Id,order.Status);}catch(Exception ex){logger.LogWarning(ex,"Transfer notification failed");}
            if (kotRouting != null)
            {
                try{await kotRouting.RecordTableTransferNoticeAsync(restaurantId, order.Id, srcTable?.TableNumber ?? 0, destTable?.TableNumber ?? 0, group?.Label, group?.Label);}catch(Exception ex){logger.LogWarning(ex,"Transfer KOT notice failed");}
            }
        }
        return result;
    });

    [HttpGet("bills/{billId:guid}/receipt")]
    public Task<IActionResult> Receipt(Guid restaurantId, Guid billId) => Execute(restaurantId,
        async () => await billing.ReceiptAsync(restaurantId, billId));

    [HttpPost("tables/{tableId:guid}/bill/open")]
    public Task<IActionResult> Open(Guid restaurantId, Guid tableId, [FromQuery] Guid? diningGroupId=null) => Execute(restaurantId, async () =>
    {
        var result = await billing.OpenAsync(restaurantId, tableId, User.GetUserId(),diningGroupId);
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
        try { await hub.Clients.Group($"restaurant_{restaurantId}").SendAsync("BillUpdated", tableId); await hub.Clients.Group($"restaurant_{restaurantId}").SendAsync("NewOrder", Guid.Empty); }
        catch (Exception ex) { logger.LogWarning(ex, "Bill committed but staff notification failed for restaurant {RestaurantId}", restaurantId); }
    }
}
