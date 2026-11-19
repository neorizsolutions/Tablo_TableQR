using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.Entities;
using Tablo.Api.Services;

namespace Tablo.Api.Tests;

public class BillPaymentTests : IDisposable
{
    private readonly ApplicationDbContext db=new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Guid tenant=Guid.NewGuid(),actor=Guid.NewGuid(),table=Guid.NewGuid();
    private readonly TableBill bill;
    public BillPaymentTests()
    {
        db.Restaurants.Add(new(){Id=tenant,Name="Payments",ContactEmail="test@example.test"});
        db.RestaurantTables.Add(new(){Id=table,RestaurantId=tenant,TableNumber=4,IsActive=true});
        db.Employees.Add(new(){Id=actor,RestaurantId=tenant,Role=new Role{Name="Waiter"},IsActive=true});
        bill=new(){RestaurantId=tenant,TableId=table,TableNumberSnapshot=4};
        bill.Orders.Add(new(){RestaurantId=tenant,TableId=table,Status="Served",Subtotal=100,Tax=5,TotalAmount=105});
        db.TableBills.Add(bill);db.SaveChanges();
    }
    [Fact]
    public async Task PartialPayments_SettleOnlyAtZeroAndRetryOriginalAcknowledgement()
    {
        var service=new BillPaymentService(db);var op=Guid.NewGuid();
        var first=await service.RecordAsync(tenant,actor,bill.Id,new(op,"Cash",40));
        Assert.Equal(65,first.Remaining);Assert.Null(bill.SettledAtUtc);
        var second=await service.RecordAsync(tenant,actor,bill.Id,new(Guid.NewGuid(),"UPI",65));
        Assert.Equal(0,second.Remaining);Assert.NotNull(bill.SettledAtUtc);
        Assert.Equal("Mixed",bill.PaymentMethod);
        var retry=await service.RecordAsync(tenant,actor,bill.Id,new(op,"Cash",40));
        Assert.Equal(first,retry);
        Assert.Equal(2,await db.BillPayments.CountAsync());
        var receipt=await new BillingService(db).ReceiptAsync(tenant,bill.Id);
        Assert.Equal(2,receipt.Payments.Count);Assert.Equal(105,receipt.Payments.Sum(p=>p.Amount));
    }
    [Fact]
    public async Task RejectsOverpaymentChangedReplayInvalidAmountAndUnservedFood()
    {
        var service=new BillPaymentService(db);var op=Guid.NewGuid();
        await service.RecordAsync(tenant,actor,bill.Id,new(op,"Cash",40));
        await Assert.ThrowsAsync<ServiceConflictException>(()=>service.RecordAsync(tenant,actor,bill.Id,new(op,"Card",40)));
        await Assert.ThrowsAsync<ServiceConflictException>(()=>service.RecordAsync(tenant,actor,bill.Id,new(Guid.NewGuid(),"Cash",66)));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.RecordAsync(tenant,actor,bill.Id,new(Guid.NewGuid(),"Cash",.001m)));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.RecordAsync(tenant,actor,bill.Id,new(Guid.NewGuid(),"Cash",0)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.RecordAsync(Guid.NewGuid(),actor,bill.Id,new(Guid.NewGuid(),"Cash",1)));
    }
    [Fact]
    public async Task UnservedOrdersAndChangedBillAmountsCannotAcceptPayments()
    {
        var service=new BillPaymentService(db);
        bill.Orders.Single().Status="Preparing"; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ServiceConflictException>(()=>service.RecordAsync(tenant,actor,bill.Id,new(Guid.NewGuid(),"Cash",1)));
        bill.Orders.Single().Status="Served";await db.SaveChangesAsync();
        await service.RecordAsync(tenant,actor,bill.Id,new(Guid.NewGuid(),"Cash",1));
        bill.Orders.Single().TotalAmount=106;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ServiceConflictException>(()=>service.RecordAsync(tenant,actor,bill.Id,new(Guid.NewGuid(),"Cash",1)));
    }
    [Fact]
    public async Task ExistingFullSettlement_CannotIgnoreAnAlreadyRecordedPartialPayment()
    {
        await new BillPaymentService(db).RecordAsync(tenant,actor,bill.Id,new(Guid.NewGuid(),"Cash",40));
        var review=(await new BillingService(db).ReviewAsync(tenant,table)).OpenBill!;
        await Assert.ThrowsAsync<ServiceConflictException>(()=>new BillingService(db).SettleAsync(tenant,bill.Id,actor,new(){
            OperationId=Guid.NewGuid(),ExpectedRevision=review.Revision,ExpectedTotal=105,PaymentMethod="Card"}));
        Assert.Equal(40,await db.BillPayments.SumAsync(p=>p.Amount));Assert.Null(bill.SettledAtUtc);
    }
    [Fact]
    public async Task BillRequestCompletesOnlyAfterEveryShareAndOnlyForItsGroup()
    {
        var group=Guid.NewGuid();bill.DiningGroupId=group;
        var other=new TableBill{RestaurantId=tenant,TableId=table,DiningGroupId=group,TableNumberSnapshot=4};
        other.Orders.Add(new(){RestaurantId=tenant,TableId=table,DiningGroupId=group,Status="Served",Subtotal=10,TotalAmount=10});
        var request=new ServiceRequest{Id=Guid.NewGuid(),RestaurantId=tenant,TableId=table,DiningGroupId=group,Type="RequestBill"};
        var unrelated=new ServiceRequest{Id=Guid.NewGuid(),RestaurantId=tenant,TableId=table,DiningGroupId=Guid.NewGuid(),Type="RequestBill"};
        db.AddRange(other,request,unrelated);await db.SaveChangesAsync();
        var payments=new BillPaymentService(db);
        await payments.RecordAsync(tenant,actor,bill.Id,new(Guid.NewGuid(),"Cash",105));
        Assert.Equal("Pending",request.Status);
        await payments.RecordAsync(tenant,actor,other.Id,new(Guid.NewGuid(),"Card",10));
        Assert.Equal("Completed",request.Status);Assert.Equal("Pending",unrelated.Status);
    }

    public void Dispose()=>db.Dispose();
}
