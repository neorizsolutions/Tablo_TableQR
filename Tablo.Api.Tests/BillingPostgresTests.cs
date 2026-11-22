using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Hubs;
using Tablo.Api.Repositories;
using Tablo.Api.Services;

namespace Tablo.Api.Tests;

public sealed class BillingPostgresFactAttribute : FactAttribute
{
    public BillingPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TABLO_BILLING_TEST_CONNECTION")))
            Skip = "Set TABLO_BILLING_TEST_CONNECTION to the dedicated loopback PostgreSQL test instance.";
    }
}

public class BillingPostgresTests
{
    private record Scenario(Guid Restaurant, Guid Table, Guid Actor, Guid Item);

    // Never connects to an application connection string or an existing application database.
    private static async Task WithDatabase(Func<string, Scenario, Task> test)
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TABLO_BILLING_TEST_CONNECTION"));
        if (builder.Host != "127.0.0.1" || builder.Port != 55439 || builder.Username != "tablo_test" || builder.Database != "postgres")
            throw new InvalidOperationException("Billing tests require the isolated loopback test cluster on port 55439.");
        builder.Pooling = false;
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        var name = "tablo_billing_test_" + Guid.NewGuid().ToString("N");
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin)) await create.ExecuteNonQueryAsync();
        builder.Database = name;
        try
        {
            Scenario scenario;
            await using (var db = Context(builder.ConnectionString))
            {
                await db.Database.MigrateAsync();
                var restaurant = new Restaurant { Id = Guid.NewGuid(), Name = "Isolated billing test", ContactEmail = "test@example.test" };
                var table = new RestaurantTable { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, TableNumber = 1, IsActive = true, QRToken = Guid.NewGuid().ToString("N") };
                var role = new Role { Id = Guid.NewGuid(), Name = "Waiter" };
                var actor = new Employee { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Role = role, IsActive = true, Email = "test@example.test" };
                var category = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Lunch" };
                var item = new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = category.Id, Name = "Meal", Price = 100m, IsAvailable = true };
                db.AddRange(restaurant, table, actor, category, item);
                await db.SaveChangesAsync();
                scenario = new(restaurant.Id, table.Id, actor.Id, item.Id);
            }
            await test(builder.ConnectionString, scenario);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static ApplicationDbContext Context(string connection) => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);

    [BillingPostgresFact]
    public Task GroupsSplitMixedPaymentsAndTransfer_ReconcileOriginalOrdersAndCollectionLocations() => WithDatabase(async(connection,s)=>
    {
        Guid destinationId;Guid groupId;Guid originalId;Guid childId;
        await using(var db=Context(connection))
        {
            var destination=new RestaurantTable{Id=Guid.NewGuid(),RestaurantId=s.Restaurant,TableNumber=7,IsActive=true,QRToken=Guid.NewGuid().ToString("N")};db.RestaurantTables.Add(destination);await db.SaveChangesAsync();destinationId=destination.Id;
            groupId=(await new DiningGroupService(db).CreateAsync(s.Restaurant,s.Actor,s.Table,"A",Guid.NewGuid())).Id;
            var groupB=(await new DiningGroupService(db).CreateAsync(s.Restaurant,s.Actor,s.Table,"B",Guid.NewGuid())).Id;
            var a=Request(s);a.DiningGroupId=groupId;var b=Request(s);b.DiningGroupId=groupB;
            await new StaffOrderingService(db,Orders(db)).SubmitAsync(s.Restaurant,s.Actor,a);
            await new StaffOrderingService(db,Orders(db)).SubmitAsync(s.Restaurant,s.Actor,b);
            Assert.Equal(2,await db.Orders.CountAsync());
            var token=(await db.RestaurantTables.SingleAsync(t=>t.Id==s.Table)).QRToken;
            await Assert.ThrowsAsync<ArgumentException>(()=>Orders(db).CreateOrderAsync(new(){QRToken=token,Items=a.Items}));
            var order=await db.Orders.SingleAsync(o=>o.DiningGroupId==groupId);order.Status="Served";originalId=order.Id;await db.SaveChangesAsync();
            var bill=(await new BillingService(db).ReviewAsync(s.Restaurant,s.Table,groupId)).OpenBill!;
            var split=await new FloorOperationsService(db).SplitAsync(s.Restaurant,s.Actor,bill.Id,new(Guid.NewGuid(),bill.Revision,[50m,55m],null));childId=split[0].Id;
            await new BillPaymentService(db).RecordAsync(s.Restaurant,s.Actor,childId,new(Guid.NewGuid(),"Cash",20));
            await new FloorOperationsService(db).TransferAsync(s.Restaurant,s.Actor,s.Table,new(Guid.NewGuid(),destinationId,groupId));
        }
        await using(var db=Context(connection))
        {
            Assert.Equal(destinationId,(await db.Orders.SingleAsync(o=>o.Id==originalId)).TableId);
            Assert.Equal(2,await db.Orders.CountAsync());
            var view=(await new BillingService(db).ReviewAsync(s.Restaurant,destinationId,groupId)).OpenBills.Single(b=>b.Id==childId);
            Assert.Equal(30,view.RemainingAmount);Assert.Equal(7,view.TableNumber);
            await new BillPaymentService(db).RecordAsync(s.Restaurant,s.Actor,childId,new(Guid.NewGuid(),"UPI",30));
            var receipt=await new BillingService(db).ReceiptAsync(s.Restaurant,childId);Assert.Equal("Mixed",receipt.PaymentMethod);Assert.Equal(7,receipt.TableNumber);Assert.Equal("A",receipt.GroupLabel);
            var reports=new PaymentReportService(db);var source=await reports.GetAsync(s.Restaurant,new(){TableId=s.Table});var target=await reports.GetAsync(s.Restaurant,new(){TableId=destinationId});
            Assert.Equal(20,source.TotalCollected);Assert.Equal(30,target.TotalCollected);
            Assert.Equal(2,(await new ReportService(db).GetAnalyticsSummaryAsync(s.Restaurant,null,null,null,null)).TotalOrders);
        }
    });

    [BillingPostgresFact]
    public Task ConcurrentTransfers_OneDestinationCannotAcquireTwoVisits() => WithDatabase(async(connection,s)=>
    {
        Guid secondSource,destination;
        await using(var db=Context(connection))
        {
            var tables=new[]{2,7}.Select(n=>new RestaurantTable{Id=Guid.NewGuid(),RestaurantId=s.Restaurant,TableNumber=n,IsActive=true,QRToken=Guid.NewGuid().ToString("N")}).ToArray();
            db.AddRange(tables);await db.SaveChangesAsync();secondSource=tables[0].Id;destination=tables[1].Id;
            await new StaffOrderingService(db,Orders(db)).SubmitAsync(s.Restaurant,s.Actor,Request(s));
            var second=Request(s);second.TableId=secondSource;await new StaffOrderingService(db,Orders(db)).SubmitAsync(s.Restaurant,s.Actor,second);
        }
        await using var gate=Context(connection);await using var tx=await gate.Database.BeginTransactionAsync();
        await gate.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"RestaurantTables\" WHERE \"Id\"={destination} FOR UPDATE");
        async Task<bool> Move(Guid source){await using var db=Context(connection);try{await new FloorOperationsService(db).TransferAsync(s.Restaurant,s.Actor,source,new(Guid.NewGuid(),destination,null));return true;}catch(ServiceConflictException){return false;}}
        var pending=new[]{Move(s.Table),Move(secondSource)};await WaitForTableWaiters(gate,2);await tx.CommitAsync();
        Assert.Single((await Task.WhenAll(pending)).Where(won=>won));
        await using var check=Context(connection);Assert.Single(await check.Orders.Where(o=>o.TableId==destination).ToListAsync());
        Assert.Equal(2,await check.Orders.CountAsync());Assert.Single(await check.FloorOperations.ToListAsync());
    });

    [BillingPostgresFact]
    public Task FoodAfterPartialPayment_CreatesAnotherBillWithoutChangingFrozenContents() => WithDatabase(async(connection,s)=>
    {
        await using var db=Context(connection);
        await new StaffOrderingService(db,Orders(db)).SubmitAsync(s.Restaurant,s.Actor,Request(s));
        (await db.Orders.SingleAsync()).Status="Served";await db.SaveChangesAsync();
        var original=(await new BillingService(db).ReviewAsync(s.Restaurant,s.Table)).OpenBill!;
        await new BillPaymentService(db).RecordAsync(s.Restaurant,s.Actor,original.Id,new(Guid.NewGuid(),"Cash",20));
        await new StaffOrderingService(db,Orders(db)).SubmitAsync(s.Restaurant,s.Actor,Request(s));
        var bills=(await new BillingService(db).ReviewAsync(s.Restaurant,s.Table)).OpenBills;
        Assert.Equal(2,bills.Count);var frozen=bills.Single(b=>b.Id==original.Id);
        Assert.Equal(original.Revision,frozen.Revision);Assert.Equal(85,frozen.RemainingAmount);
        Assert.Equal(105,bills.Single(b=>b.Id!=original.Id).TotalAmount);Assert.Equal(2,await db.Orders.CountAsync());
    });

    [BillingPostgresFact]
    public Task FoundationLedgerUpgrade_BackfillsCollectionLocationWithoutChangingAcknowledgement() => WithDatabase(async(connection,s)=>
    {
        var bill=Guid.NewGuid();var payment=Guid.NewGuid();var operation=Guid.NewGuid();
        const string acknowledgement="{\"Remaining\":65}";
        await using(var db=Context(connection))
        {
            var migrator=db.Database.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
            await migrator.MigrateAsync("20261116092356_AddDiningGroupsAndPaymentLedgerFoundation");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"TableBills\" (\"Id\",\"RestaurantId\",\"TableId\",\"TableNumberSnapshot\",\"CreatedAtUtc\",\"Subtotal\",\"Tax\",\"Discount\",\"TotalAmount\") VALUES ({bill},{s.Restaurant},{s.Table},1,{DateTime.UtcNow},100,5,0,105)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"BillPayments\" (\"Id\",\"RestaurantId\",\"TableBillId\",\"ActorEmployeeId\",\"OperationId\",\"PayloadHash\",\"Method\",\"Amount\",\"CollectedAtUtc\",\"AcknowledgementJson\") VALUES ({payment},{s.Restaurant},{bill},{s.Actor},{operation},'test','Cash',40,{DateTime.UtcNow},CAST({acknowledgement} AS jsonb))");
            await migrator.MigrateAsync();
        }
        await using var check=Context(connection);
        var saved=await check.BillPayments.SingleAsync(p=>p.Id==payment);
        Assert.Equal(s.Table,saved.TableIdSnapshot);Assert.Equal(1,saved.TableNumberSnapshot);
        Assert.Equal(40,saved.Amount);Assert.Equal(operation,saved.OperationId);
        Assert.Equal(65,System.Text.Json.JsonDocument.Parse(saved.AcknowledgementJson).RootElement.GetProperty("Remaining").GetDecimal());
    });

    [BillingPostgresFact]
    public Task ConcurrentBillSplits_OnlyOneAllocationSetCanWin() => WithDatabase(async(connection,s)=>
    {
        Guid id;string revision;
        await using(var db=Context(connection))
        {
            await new StaffOrderingService(db,Orders(db)).SubmitAsync(s.Restaurant,s.Actor,Request(s));(await db.Orders.SingleAsync()).Status="Served";await db.SaveChangesAsync();
            var bill=(await new BillingService(db).ReviewAsync(s.Restaurant,s.Table)).OpenBill!;id=bill.Id;revision=bill.Revision;
        }
        await using var gate=Context(connection);await using var tx=await gate.Database.BeginTransactionAsync();
        await gate.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"RestaurantTables\" WHERE \"Id\"={s.Table} FOR UPDATE");
        async Task<bool> Split(){await using var db=Context(connection);try{await new FloorOperationsService(db).SplitAsync(s.Restaurant,s.Actor,id,new(Guid.NewGuid(),revision,[50m,55m],null));return true;}catch(ServiceConflictException){return false;}}
        var pending=new[]{Split(),Split()};await WaitForTableWaiters(gate,2);await tx.CommitAsync();Assert.Single((await Task.WhenAll(pending)).Where(b=>b));
        await using var check=Context(connection);Assert.Equal(2,await check.TableBills.CountAsync(b=>b.ParentBillId==id));Assert.Single(await check.Orders.ToListAsync());
    });

    [BillingPostgresFact]
    public Task FoundationUpgrade_PreservesExistingSettledBillAndReceipt() => WithDatabase(async(connection,s)=>
    {
        var billId=Guid.NewGuid();
        const string receipt="{\"Id\":\"00000000-0000-0000-0000-000000000004\",\"Status\":\"Settled\",\"TotalAmount\":105}";
        await using(var db=Context(connection))
        {
            var migrator=db.Database.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
            await migrator.MigrateAsync("20261030102832_AddStaffOrderingAndBilling");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"TableBills\" (\"Id\",\"RestaurantId\",\"TableId\",\"TableNumberSnapshot\",\"CreatedAtUtc\",\"SettledAtUtc\",\"SettledByEmployeeId\",\"PaymentMethod\",\"Subtotal\",\"Tax\",\"Discount\",\"TotalAmount\",\"ReceiptJson\") VALUES ({billId},{s.Restaurant},{s.Table},1,{DateTime.UtcNow},{DateTime.UtcNow},{s.Actor},'Cash',100,5,0,105,CAST({receipt} AS jsonb))");
            await migrator.MigrateAsync();
        }
        await using var check=Context(connection);
        var saved=await check.TableBills.SingleAsync(b=>b.Id==billId);
        Assert.Equal(105,saved.TotalAmount);Assert.Equal("Cash",saved.PaymentMethod);Assert.NotNull(saved.SettledAtUtc);
        Assert.Equal(105,System.Text.Json.JsonDocument.Parse(saved.ReceiptJson!).RootElement.GetProperty("TotalAmount").GetDecimal());
        Assert.Null(saved.PaymentRevision);Assert.Empty(await check.BillPayments.ToListAsync());
    });

    [BillingPostgresFact]
    public Task ConcurrentPartialPayments_CannotOverpayAndReplaysStayStable() => WithDatabase(async(connection,s)=>
    {
        Guid billId;
        await using(var db=Context(connection))
        {
            await new StaffOrderingService(db,Orders(db)).SubmitAsync(s.Restaurant,s.Actor,Request(s));
            (await db.Orders.SingleAsync()).Status="Served"; await db.SaveChangesAsync();
            billId=(await db.TableBills.SingleAsync()).Id;
        }
        await using var gate=Context(connection);
        await using var tx=await gate.Database.BeginTransactionAsync();
        await gate.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"RestaurantTables\" WHERE \"Id\"={s.Table} FOR UPDATE");
        async Task<PaymentAcknowledgement?> Pay(Guid operation)
        {
            await using var db=Context(connection);
            try{return await new BillPaymentService(db).RecordAsync(s.Restaurant,s.Actor,billId,new(operation,"Cash",70));}
            catch(ServiceConflictException){return null;}
        }
        var operations=new[]{Guid.NewGuid(),Guid.NewGuid()};
        var pending=operations.Select(Pay).ToArray();
        await WaitForTableWaiters(gate,2); await tx.CommitAsync();
        var results=await Task.WhenAll(pending);
        Assert.Single(results.Where(r=>r!=null));
        await using var check=Context(connection);
        Assert.Equal(70,await check.BillPayments.SumAsync(p=>p.Amount));
        Assert.Null((await check.TableBills.SingleAsync()).SettledAtUtc);
        var winner=results[0]!=null?0:1;
        var replay=await Pay(operations[winner]); Assert.Equal(results[winner],replay);
        var final=await new BillPaymentService(check).RecordAsync(s.Restaurant,s.Actor,billId,new(Guid.NewGuid(),"UPI",35));
        Assert.Equal(0,final.Remaining);Assert.Equal(105,await check.BillPayments.SumAsync(p=>p.Amount));
    });

    [BillingPostgresFact]
    public Task ConcurrentDiningGroups_AllowOneActiveLabel() => WithDatabase(async(connection,s)=>
    {
        await using var gate=Context(connection);await using var tx=await gate.Database.BeginTransactionAsync();
        await gate.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"RestaurantTables\" WHERE \"Id\"={s.Table} FOR UPDATE");
        async Task<DiningGroupDto?> Create()
        {
            await using var db=Context(connection);
            try{return await new DiningGroupService(db).CreateAsync(s.Restaurant,s.Actor,s.Table,"A",Guid.NewGuid());}
            catch(ServiceConflictException){return null;}
        }
        var requests=new[]{Create(),Create()};await WaitForTableWaiters(gate,2);await tx.CommitAsync();
        Assert.Single((await Task.WhenAll(requests)).Where(g=>g!=null));
        await using var check=Context(connection);Assert.Equal(1,await check.DiningGroups.CountAsync());
    });
    private static OrderService Orders(ApplicationDbContext db) => new(new OrderRepository(db), Mock.Of<IHubContext<OrderHub>>(), db, NullLogger<OrderService>.Instance);
    private static StaffOrderRequestDto Request(Scenario s, Guid? operation = null) => new()
    {
        OperationId = operation ?? Guid.NewGuid(), TableId = s.Table,
        Items = [new OrderItemRequestDto { MenuItemId = s.Item, Quantity = 1 }]
    };

    private static async Task WaitForTableWaiters(ApplicationDbContext gate, int count)
    {
        // PostgreSQL caches statistics within a transaction. Observe from a separate
        // autocommit connection, not the transaction deliberately holding the row lock.
        await using var observer = new NpgsqlConnection(gate.Database.GetConnectionString());
        await observer.OpenAsync();
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var command = new NpgsqlCommand("SELECT COUNT(*)::int FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'", observer);
            var waiting = (int)(await command.ExecuteScalarAsync())!;
            if (waiting >= count) return;
            await Task.Delay(30);
        }
        Assert.Fail($"Expected {count} independent PostgreSQL connections waiting on the table lock.");
    }

    [BillingPostgresFact]
    public Task ConcurrentStaffRetries_CommitOneOrderAndOneReceipt() => WithDatabase(async (connection, s) =>
    {
        await using var gate = Context(connection);
        await using var transaction = await gate.Database.BeginTransactionAsync();
        await gate.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"RestaurantTables\" WHERE \"Id\" = {s.Table} FOR UPDATE");
        var operation = Guid.NewGuid();
        async Task<Guid> Submit()
        {
            await using var db = Context(connection);
            return (await new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, Request(s, operation))).OrderId;
        }
        var tasks = new[] { Submit(), Submit(), Submit() };
        await WaitForTableWaiters(gate, 3);
        await transaction.CommitAsync();
        var ids = await Task.WhenAll(tasks);
        Assert.Single(ids.Distinct());
        await using var check = Context(connection);
        Assert.Equal(1, await check.Orders.CountAsync());
        Assert.Equal(1, await check.OrderItems.CountAsync());
        Assert.Equal(1, await check.StaffOrderSubmissions.CountAsync());
        Assert.Equal(1, await check.TableBills.CountAsync());
        Assert.Equal(105m, (await check.Orders.SingleAsync()).TotalAmount);
    });

    [BillingPostgresFact]
    public Task ConcurrentDistinctSubmissions_AppendWithoutLosingTotals() => WithDatabase(async (connection, s) =>
    {
        await using var gate = Context(connection);
        await using var transaction = await gate.Database.BeginTransactionAsync();
        await gate.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"RestaurantTables\" WHERE \"Id\" = {s.Table} FOR UPDATE");
        async Task Submit()
        {
            await using var db = Context(connection);
            await new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, Request(s));
        }
        var tasks = new[] { Submit(), Submit(), Submit() };
        await WaitForTableWaiters(gate, 3);
        await transaction.CommitAsync();
        await Task.WhenAll(tasks);
        await using var check = Context(connection);
        Assert.Equal(1, await check.Orders.CountAsync());
        Assert.Equal(3, await check.OrderItems.CountAsync());
        Assert.Equal(3, await check.StaffOrderSubmissions.CountAsync());
        Assert.Equal(315m, (await check.Orders.SingleAsync()).TotalAmount);
    });

    [BillingPostgresFact]
    public Task InvalidItem_RollsBackOrderBillAndReceipt() => WithDatabase(async (connection, s) =>
    {
        await using (var db = Context(connection))
        {
            var request = Request(s);
            request.Items.Add(new OrderItemRequestDto { MenuItemId = Guid.NewGuid(), Quantity = 1 });
            await Assert.ThrowsAsync<ArgumentException>(() => new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, request));
        }
        await using var check = Context(connection);
        Assert.Equal(0, await check.Orders.CountAsync());
        Assert.Equal(0, await check.TableBills.CountAsync());
        Assert.Equal(0, await check.StaffOrderSubmissions.CountAsync());
    });

    [BillingPostgresFact]
    public Task SettledBill_IsImmutableAndNextQrOrderStartsNewBill() => WithDatabase(async (connection, s) =>
    {
        Guid firstBill;
        await using (var db = Context(connection))
        {
            var result = await new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, Request(s));
            foreach (var status in new[] { "Accepted", "Preparing", "Ready", "Served" })
                await Orders(db).UpdateOrderStatusAsync(s.Restaurant, result.OrderId, new UpdateOrderStatusRequestDto { RestaurantId = s.Restaurant, Status = status });
            var bill = (await new BillingService(db).ReviewAsync(s.Restaurant, s.Table)).OpenBill!;
            firstBill = bill.Id;
            await new BillingService(db).SettleAsync(s.Restaurant, bill.Id, s.Actor,
                new SettleBillRequestDto { OperationId = Guid.NewGuid(), ExpectedRevision = bill.Revision, ExpectedTotal = 105, PaymentMethod = "Cash" });
        }
        await using (var db = Context(connection))
        {
            var table = await db.RestaurantTables.SingleAsync();
            await Orders(db).CreateOrderAsync(new CreateOrderRequestDto { QRToken = table.QRToken, Items = Request(s).Items });
        }
        await using var check = Context(connection);
        var review = await new BillingService(check).ReviewAsync(s.Restaurant, s.Table);
        Assert.NotEqual(firstBill, review.OpenBill!.Id);
        Assert.Equal(105m, review.RecentSettlements.Single().TotalAmount);
        Assert.Equal(2, await check.TableBills.CountAsync());
    });

    [BillingPostgresFact]
    public Task SettlementRacingWithSubmission_NeverClosesUnservedWork() => WithDatabase(async (connection, s) =>
    {
        BillDto reviewed;
        await using (var db = Context(connection))
        {
            await new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, Request(s));
            var order = await db.Orders.SingleAsync();
            order.Status = "Served";
            await db.SaveChangesAsync();
            reviewed = (await new BillingService(db).ReviewAsync(s.Restaurant, s.Table)).OpenBill!;
        }
        async Task Settle()
        {
            await using var db = Context(connection);
            try { await new BillingService(db).SettleAsync(s.Restaurant, reviewed.Id, s.Actor,
                new SettleBillRequestDto { OperationId = Guid.NewGuid(), ExpectedRevision = reviewed.Revision, ExpectedTotal = 105, PaymentMethod = "UPI" }); }
            catch (ServiceConflictException) { /* Submission won the table lock; bill must be reviewed again. */ }
        }
        async Task Submit()
        {
            await using var db = Context(connection);
            await new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, Request(s));
        }
        await Task.WhenAll(Settle(), Submit());
        await using var check = Context(connection);
        var bills = await check.TableBills.Include(b => b.Orders).ToListAsync();
        Assert.Single(bills.Where(b => b.SettledAtUtc == null));
        foreach (var paid in bills.Where(b => b.SettledAtUtc != null)) Assert.All(paid.Orders, o => Assert.Equal("Served", o.Status));
        Assert.Equal(2, await check.Orders.CountAsync());
    });

    private sealed class FailStaffReceiptSave : SaveChangesInterceptor
    {
        public bool ObservedSavedOrder { get; private set; }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (data.Context is ApplicationDbContext db && db.ChangeTracker.Entries<StaffOrderSubmission>().Any(e => e.State == EntityState.Added))
            {
                ObservedSavedOrder = await db.Orders.AnyAsync(cancellationToken);
                throw new InvalidOperationException("Injected failure after order save, before staff receipt save");
            }
            return result;
        }
    }

    [BillingPostgresFact]
    public Task FailureAfterOrderSave_RollsBackOrderItemsBillSequenceAndSubmission() => WithDatabase(async (connection, s) =>
    {
        var failure = new FailStaffReceiptSave();
        await using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).AddInterceptors(failure).Options))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, Request(s)));
            Assert.True(failure.ObservedSavedOrder, "Fault injection must occur after an order row actually exists inside the transaction.");
        }
        await using var check = Context(connection);
        Assert.Equal(0, await check.Orders.CountAsync());
        Assert.Equal(0, await check.OrderItems.CountAsync());
        Assert.Equal(0, await check.TableBills.CountAsync());
        Assert.Equal(0, await check.StaffOrderSubmissions.CountAsync());
        Assert.Equal(0, await check.RestaurantOrderSequences.CountAsync());
    });

    private static Task CheckConcurrentSettlements(bool sameOperation) => WithDatabase(async (connection, s) =>
    {
        BillDto bill;
        await using (var db = Context(connection))
        {
            await new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, Request(s));
            (await db.Orders.SingleAsync()).Status = "Served";
            await db.SaveChangesAsync();
            bill = (await new BillingService(db).ReviewAsync(s.Restaurant, s.Table)).OpenBill!;
        }
        await using var gate = Context(connection);
        await using var transaction = await gate.Database.BeginTransactionAsync();
        await gate.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"RestaurantTables\" WHERE \"Id\" = {s.Table} FOR UPDATE");
        var operation = Guid.NewGuid();
        async Task<BillDto?> Settle(Guid id)
        {
            await using var db = Context(connection);
            try { return await new BillingService(db).SettleAsync(s.Restaurant, bill.Id, s.Actor,
                new SettleBillRequestDto { OperationId = id, ExpectedRevision = bill.Revision, ExpectedTotal = 105, PaymentMethod = "Cash" }); }
            catch (ServiceConflictException) { return null; }
        }
        var requests = new[] { Settle(operation), Settle(sameOperation ? operation : Guid.NewGuid()) };
        await WaitForTableWaiters(gate, 2);
        await transaction.CommitAsync();
        var results = await Task.WhenAll(requests);
        Assert.Equal(sameOperation ? 2 : 1, results.Count(r => r != null));
        Assert.Single(results.Where(r => r != null).Select(r => r!.SettledAtUtc).Distinct());
        await using var check = Context(connection);
        Assert.Equal(1, await check.TableBills.CountAsync(b => b.SettledAtUtc != null));
        Assert.Equal(105m, (await check.TableBills.SingleAsync()).TotalAmount);
    });

    [BillingPostgresFact]
    public Task ConcurrentSameSettlementOperation_ReturnsOneRecordedPayment() => CheckConcurrentSettlements(true);

    [BillingPostgresFact]
    public Task ConcurrentDifferentSettlementOperations_OnlyOneCanPay() => CheckConcurrentSettlements(false);

    [BillingPostgresFact]
    public Task PaymentReport_ExecutesRealPostgresProjectionAndReconcilesAllMethods() => WithDatabase(async (connection,s)=>
    {
        foreach(var method in new[]{"Cash","UPI","Card"})
        {
            await using var db=Context(connection);
            await new StaffOrderingService(db,Orders(db)).SubmitAsync(s.Restaurant,s.Actor,Request(s));
            var order=await db.Orders.SingleAsync(o=>o.Status=="Accepted"); order.Status="Served";
            await db.SaveChangesAsync();
            var bill=(await new BillingService(db).ReviewAsync(s.Restaurant,s.Table)).OpenBill!;
            await new BillingService(db).SettleAsync(s.Restaurant,bill.Id,s.Actor,new() {
                OperationId=Guid.NewGuid(),ExpectedRevision=bill.Revision,ExpectedTotal=105,PaymentMethod=method });
        }
        await using var check=Context(connection);
        var reports=new PaymentReportService(check);
        var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata")));
        var report=await reports.GetAsync(s.Restaurant,new(){StartDate=today,EndDate=today,TableId=s.Table});
        Assert.Equal(315,report.TotalCollected); Assert.Equal(3,report.TotalCount);
        Assert.Equal(105,report.Cash); Assert.Equal(105,report.UPI); Assert.Equal(105,report.Card);
        Assert.True((await reports.ExportAsync(s.Restaurant,new())).Length>100);
        Assert.Equal(0,(await reports.GetAsync(Guid.NewGuid(),new())).TotalCount);
    });
}
