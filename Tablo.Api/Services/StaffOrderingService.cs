using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services;

public class StaffOrderingService(ApplicationDbContext db, IOrderService orders)
{
    public async Task<OrderResponseDto> SubmitAsync(Guid restaurantId, Guid actorId, StaffOrderRequestDto request)
    {
        await BillingService.RequireActorAsync(db, restaurantId, actorId);
        if (request.OperationId == Guid.Empty || request.TableId == Guid.Empty || request.Items == null || request.Items.Count is < 1 or > 200
            || request.Items.Any(i => i.MenuItemId == Guid.Empty || i.Quantity <= 0))
            throw new ArgumentException("Select a table and at least one valid menu item.");
        await using var tx = await TableOrderTransaction.BeginAsync(db);
        await TableOrderTransaction.LockAsync(db, restaurantId, request.TableId);
        var table = await db.RestaurantTables.SingleOrDefaultAsync(t => t.Id == request.TableId && t.RestaurantId == restaurantId)
            ?? throw new KeyNotFoundException("Table not found.");
        var payload=request.DiningGroupId==null?JsonSerializer.Serialize(new { actorId, request.TableId, Items=request.Items.Select(i=>new{i.MenuItemId,i.Quantity}) }):JsonSerializer.Serialize(new{actorId,request.TableId,request.DiningGroupId,Items=request.Items.Select(i=>new{i.MenuItemId,i.Quantity})});
        var hash=BillingService.Hash(payload);
        var replay = await db.StaffOrderSubmissions.SingleOrDefaultAsync(s => s.RestaurantId == restaurantId && s.OperationId == request.OperationId);
        if (replay != null)
        {
            if (replay.PayloadHash != hash) throw new ServiceConflictException("This submission ID was already used for different items or a different table.");
            return JsonSerializer.Deserialize<OrderResponseDto>(replay.OriginalResponseJson)!;
        }
        if (!table.IsActive) throw new ArgumentException("This table is inactive.");
        // Reuse server menu validation, price snapshots, tax and active-order semantics.
        var result = await orders.CreateOrderAsync(new CreateOrderRequestDto { QRToken = table.QRToken, DiningGroupId=request.DiningGroupId, Items = request.Items }, isStaff: true);
        db.StaffOrderSubmissions.Add(new StaffOrderSubmission { RestaurantId = restaurantId, TableId = table.Id,
            ActorEmployeeId = actorId, OperationId = request.OperationId, PayloadHash = hash, OriginalResponseJson = JsonSerializer.Serialize(result) });
        await db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return result;
    }
}
