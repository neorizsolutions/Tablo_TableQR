using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.Entities;
using Tablo.Api.Services;

namespace Tablo.Api.Tests;

public class FloorOperationsTests : IDisposable
{
    private readonly ApplicationDbContext db=new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Guid tenant=Guid.NewGuid(),actor=Guid.NewGuid(),table=Guid.NewGuid(),target=Guid.NewGuid();
    public FloorOperationsTests()
    {
        db.Restaurants.Add(new(){Id=tenant,Name="Floor",ContactEmail="floor@example.test"});
        db.RestaurantTables.AddRange(new(){Id=table,RestaurantId=tenant,TableNumber=4,IsActive=true},new(){Id=target,RestaurantId=tenant,TableNumber=7,IsActive=true});
        db.Employees.Add(new(){Id=actor,RestaurantId=tenant,Role=new Role{Name="Waiter"},IsActive=true});db.SaveChanges();
    }
    private TableBill Bill(Guid? group=null)
    {
        var bill=new TableBill{RestaurantId=tenant,TableId=table,TableNumberSnapshot=4,DiningGroupId=group};
        bill.Orders.Add(new(){Id=Guid.NewGuid(),RestaurantId=tenant,TableId=table,DiningGroupId=group,Status="Served",Subtotal=100,Tax=5,TotalAmount=105,
            OrderItems=[new(){Id=Guid.NewGuid(),ItemNameSnapshot="Lunch",Quantity=2,UnitPrice=50}]});
        db.TableBills.Add(bill);db.SaveChanges();return bill;
    }
    [Fact]
    public async Task SplitAmounts_PreservesKitchenOrderAndTotalsAndReplays()
    {
        var bill=Bill();var service=new FloorOperationsService(db);var op=Guid.NewGuid();
        var request=new SplitBillRequest(op,BillingServiceRevision(bill),[50m,55m],null);
        var split=await service.SplitAsync(tenant,actor,bill.Id,request);
        Assert.Equal(105,split.Sum(b=>b.TotalAmount));Assert.Equal(100,split.Sum(b=>b.Subtotal));Assert.Equal(5,split.Sum(b=>b.Tax));
        Assert.Single(await db.Orders.ToListAsync());Assert.Single(await db.OrderItems.ToListAsync());
        Assert.NotNull(bill.SplitAtUtc);Assert.Equal(split.Select(b=>b.Id),(await service.SplitAsync(tenant,actor,bill.Id,request)).Select(b=>b.Id));
        await Assert.ThrowsAsync<ServiceConflictException>(()=>service.SplitAsync(tenant,actor,bill.Id,request with{Amounts=[51m,54m]}));
    }
    private string BillingServiceRevision(TableBill bill)=>new BillingService(db).ReviewAsync(tenant,table).GetAwaiter().GetResult().OpenBills.Single(b=>b.Id==bill.Id).Revision;
    [Fact]
    public async Task SplitItems_AllocatesQuantityAndRejectsAfterPayment()
    {
        var bill=Bill();var item=bill.Orders.Single().OrderItems.Single();
        var service=new FloorOperationsService(db);
        var split=await service.SplitAsync(tenant,actor,bill.Id,new(Guid.NewGuid(),BillingServiceRevision(bill),null,
            [new(){{item.Id,1}},new(){{item.Id,1}}]));
        Assert.All(split,b=>Assert.Single(b.Orders.Single().Items));
        Assert.All(split,b=>Assert.Equal(1,b.Orders.Single().Items.Single().Quantity));
        await new BillPaymentService(db).RecordAsync(tenant,actor,split[0].Id,new(Guid.NewGuid(),"Cash",1));
        await Assert.ThrowsAsync<ServiceConflictException>(()=>service.SplitAsync(tenant,actor,split[0].Id,new(Guid.NewGuid(),split[0].Revision,[20m,32.5m],null)));
    }
    [Fact]
    public async Task Transfer_PreservesGroupPaymentsAndRejectsOccupiedDestination()
    {
        var group=await new DiningGroupService(db).CreateAsync(tenant,actor,table,"A",Guid.NewGuid());var bill=Bill(group.Id);
        await new BillPaymentService(db).RecordAsync(tenant,actor,bill.Id,new(Guid.NewGuid(),"Cash",40));
        var operation=Guid.NewGuid();var service=new FloorOperationsService(db);
        await service.TransferAsync(tenant,actor,table,new(operation,target,group.Id));
        Assert.Equal(target,bill.TableId);Assert.Equal(target,(await db.Orders.SingleAsync()).TableId);
        Assert.Equal(40,await db.BillPayments.SumAsync(p=>p.Amount));
        await service.TransferAsync(tenant,actor,table,new(operation,target,group.Id));
        Assert.Single(await db.FloorOperations.ToListAsync());
        await Assert.ThrowsAsync<ServiceConflictException>(()=>service.TransferAsync(tenant,actor,target,new(Guid.NewGuid(),target,group.Id)));
    }
    public void Dispose()=>db.Dispose();
}
