using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;

namespace Tablo.Api.Services;
public record SplitBillRequest(Guid OperationId,string ExpectedRevision,decimal[]? Amounts,Dictionary<Guid,int>[]? Items);
public record TransferGroupRequest(Guid OperationId,Guid DestinationTableId,Guid? DiningGroupId);
public record TransferResult(Guid SourceTableId,Guid DestinationTableId,Guid? DiningGroupId,DateTime TransferredAtUtc);

public class FloorOperationsService(ApplicationDbContext db)
{
    private async Task<T?> Replay<T>(Guid tenant,Guid operation,string hash) where T:class
    {
        var old=await db.FloorOperations.AsNoTracking().SingleOrDefaultAsync(o=>o.RestaurantId==tenant && o.OperationId==operation);
        if(old==null)return null;
        if(old.Hash!=hash)throw new ServiceConflictException("This operation ID was already used with different details.");
        return JsonSerializer.Deserialize<T>(old.ResultJson)!;
    }
    public async Task<List<BillDto>> SplitAsync(Guid tenant,Guid actor,Guid billId,SplitBillRequest request,bool preview=false)
    {
        await BillingService.RequireActorAsync(db,tenant,actor);
        if(request.OperationId==Guid.Empty || string.IsNullOrEmpty(request.ExpectedRevision) || (request.Amounts==null)==(request.Items==null))throw new ArgumentException("Select either amount shares or item quantities and review the bill first.");
        var hash=BillingService.Hash(JsonSerializer.Serialize(new{Kind="Split",actor,billId,request}));
        var billMeta=await db.TableBills.Where(b=>b.Id==billId && b.RestaurantId==tenant).Select(b=>new{b.TableId,b.OrderChannel}).SingleOrDefaultAsync()??throw new KeyNotFoundException("Bill not found.");
        await using var tx=await TableOrderTransaction.BeginAsync(db);
        if(billMeta.TableId.HasValue)
        {
            await TableOrderTransaction.LockAsync(db,tenant,billMeta.TableId.Value);
        }
        await TableOrderTransaction.LockBillAsync(db,tenant,billId);
        var replay=await Replay<List<BillDto>>(tenant,request.OperationId,hash);if(replay!=null)return replay;
        var bill=await db.TableBills.Include(b=>b.Orders).ThenInclude(o=>o.OrderItems).SingleAsync(b=>b.Id==billId && b.RestaurantId==tenant);
        if(bill.TableId!=billMeta.TableId || bill.SettledAtUtc!=null || bill.SplitAtUtc!=null || bill.ParentBillId!=null || bill.PaymentRevision!=null || await db.BillPayments.AnyAsync(p=>p.RestaurantId==tenant && p.TableBillId==billId))throw new ServiceConflictException("Only an unpaid, unsplit original bill can be split.");
        var original=BillingService.Project(bill);
        if(!original.CanSettle || original.Revision!=request.ExpectedRevision)throw new ServiceConflictException("Serve all food and review the latest bill before splitting.");
        var chargeable=original.Orders.Where(o=>o.Status is not ("Cancelled" or "Rejected")).ToList();
        if(request.Amounts!=null && request.Amounts.Length<2)throw new ArgumentException("Enter at least two shares.");
        var parts=request.Amounts!=null?BillSplitCalculator.ByAmounts(original.Subtotal,original.Tax,original.Discount,request.Amounts):
            BillSplitCalculator.ByItems(chargeable.SelectMany(o=>o.Items).Select(i=>new SplitItem(i.Id,i.Quantity,i.UnitPrice)).ToList(),original.Tax,original.Discount,request.Items!);
        var result=new List<BillDto>();
        for(var n=0;n<parts.Count;n++)
        {
            var share=parts[n];
            var child=new TableBill{RestaurantId=tenant,TableId=bill.TableId,DiningGroupId=bill.DiningGroupId,GroupLabelSnapshot=bill.GroupLabelSnapshot,TableNumberSnapshot=bill.TableNumberSnapshot,ParentBillId=billId,Subtotal=share.Subtotal,Tax=share.Tax,Discount=share.Discount,TotalAmount=share.Total};
            var dto=new BillDto{Id=child.Id,ParentBillId=billId,DiningGroupId=bill.DiningGroupId,GroupLabel=bill.GroupLabelSnapshot,TableNumber=bill.TableNumberSnapshot,CreatedAtUtc=child.CreatedAtUtc,
                Revision=BillingService.Hash(original.Revision+":"+request.OperationId+":"+n),Subtotal=share.Subtotal,Tax=share.Tax,Discount=share.Discount,TotalAmount=share.Total,RemainingAmount=share.Total,CanSettle=true,
                ShareDescription=$"{(request.Amounts!=null?"Amount":"Item")} share {n+1} of {parts.Count} · source receipt {billId}"};
            if(request.Items!=null)
                dto.Orders=chargeable.Select(o=>new BillOrderDto{Id=o.Id,OrderNumber=o.OrderNumber,Status=o.Status,
                    Items=o.Items.Where(i=>request.Items[n].ContainsKey(i.Id)).Select(i=>new OrderItemDto{Id=i.Id,MenuItemId=i.MenuItemId,Name=i.Name,ItemNameSnapshot=i.ItemNameSnapshot,UnitPrice=i.UnitPrice,Quantity=request.Items[n][i.Id]}).ToList()}).Where(o=>o.Items.Count>0).ToList();
            child.AllocationJson=JsonSerializer.Serialize(dto);result.Add(dto);
            if(!preview)db.TableBills.Add(child);
        }
        if(preview)return result;
        bill.SplitAtUtc=DateTime.UtcNow;
        db.FloorOperations.Add(new(){RestaurantId=tenant,ActorEmployeeId=actor,OperationId=request.OperationId,Kind="Split",Hash=hash,ResultJson=JsonSerializer.Serialize(result)});
        await db.SaveChangesAsync();if(tx!=null)await tx.CommitAsync();return result;
    }

    public async Task<TransferResult> TransferAsync(Guid tenant,Guid actor,Guid source,TransferGroupRequest request)
    {
        await BillingService.RequireActorAsync(db,tenant,actor);
        if(request.OperationId==Guid.Empty)throw new ArgumentException("An operation ID is required.");
        if(source==request.DestinationTableId)throw new ServiceConflictException("Choose a different destination table.");
        var hash=BillingService.Hash(JsonSerializer.Serialize(new{Kind="Transfer",actor,source,request}));
        await using var tx=await TableOrderTransaction.BeginAsync(db);
        foreach(var id in new[]{source,request.DestinationTableId}.Order())await TableOrderTransaction.LockAsync(db,tenant,id);
        var replay=await Replay<TransferResult>(tenant,request.OperationId,hash);if(replay!=null)return replay;
        if(!await db.RestaurantTables.AnyAsync(t=>t.Id==source && t.RestaurantId==tenant))throw new KeyNotFoundException("Source table not found.");
        var destination=await db.RestaurantTables.SingleOrDefaultAsync(t=>t.Id==request.DestinationTableId && t.RestaurantId==tenant && t.IsActive)??throw new ArgumentException("Destination table is inactive or unavailable.");
        if(await db.DiningGroups.AnyAsync(g=>g.RestaurantId==tenant && g.TableId==destination.Id && g.ClosedAtUtc==null) || await db.TableBills.AnyAsync(b=>b.RestaurantId==tenant && b.TableId==destination.Id && b.SettledAtUtc==null && b.SplitAtUtc==null) || await db.Orders.AnyAsync(o=>o.RestaurantId==tenant && o.TableId==destination.Id && o.Status!="Served" && o.Status!="Cancelled" && o.Status!="Rejected"))throw new ServiceConflictException("Destination table is occupied.");
        DiningGroup? group=null;
        if(request.DiningGroupId is {} groupId)group=await db.DiningGroups.SingleOrDefaultAsync(g=>g.Id==groupId && g.RestaurantId==tenant && g.TableId==source && g.ClosedAtUtc==null)??throw new ServiceConflictException("This group moved or closed. Refresh before transferring.");
        var bills=await db.TableBills.Where(b=>b.RestaurantId==tenant && b.TableId==source && b.DiningGroupId==request.DiningGroupId && b.SettledAtUtc==null).ToListAsync();
        var ids=bills.Select(b=>b.Id).ToList();
        var orders=await db.Orders.Where(o=>o.RestaurantId==tenant && o.TableId==source && o.DiningGroupId==request.DiningGroupId && ((o.TableBillId!=null && ids.Contains(o.TableBillId.Value)) || (o.TableBillId==null && o.Status!="Served" && o.Status!="Cancelled" && o.Status!="Rejected"))).ToListAsync();
        if(!bills.Any(b=>b.SplitAtUtc==null) && orders.Count==0)throw new ServiceConflictException("There is no open visit to transfer.");
        // Detach nullable order links before changing the composite bill/table key in one transaction.
        var links=orders.ToDictionary(o=>o.Id,o=>o.TableBillId);
        foreach(var o in orders){o.TableBill=null;o.TableBillId=null;}await db.SaveChangesAsync();
        foreach(var b in bills){b.TableId=destination.Id;b.TableNumberSnapshot=destination.TableNumber;}
        if(group!=null)group.TableId=destination.Id;
        foreach(var o in orders)o.TableId=destination.Id;
        var requests=await db.ServiceRequests.Where(s=>s.RestaurantId==tenant && s.TableId==source && s.DiningGroupId==request.DiningGroupId && (s.Status=="Pending" || s.Status=="Acknowledged")).ToListAsync();
        foreach(var sr in requests){sr.TableId=destination.Id;sr.UpdatedAt=DateTime.UtcNow;}
        await db.SaveChangesAsync();
        foreach(var o in orders)o.TableBillId=links[o.Id];
        var result=new TransferResult(source,destination.Id,request.DiningGroupId,DateTime.UtcNow);
        db.FloorOperations.Add(new(){RestaurantId=tenant,ActorEmployeeId=actor,OperationId=request.OperationId,Kind="Transfer",Hash=hash,ResultJson=JsonSerializer.Serialize(result)});
        await db.SaveChangesAsync();if(tx!=null)await tx.CommitAsync();return result;
    }
}
