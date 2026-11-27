using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Services;

namespace Tablo.Api.Tests;

public class BillingServiceTests : IDisposable
{
    private readonly ApplicationDbContext db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Guid restaurantId = Guid.NewGuid();
    private readonly Guid actorId = Guid.NewGuid();
    private readonly RestaurantTable table;

    public BillingServiceTests()
    {
        var role = new Role { Id = Guid.NewGuid(), Name = "Waiter" };
        table = new RestaurantTable { Id = Guid.NewGuid(), RestaurantId = restaurantId, TableNumber = 4, QRToken = "test-qr" };
        db.Restaurants.Add(new Restaurant { Id = restaurantId, Name = "Test Restaurant", ContactEmail = "test@example.test" });
        db.RestaurantTables.Add(table);
        db.Employees.Add(new Employee { Id = actorId, RestaurantId = restaurantId, Role = role, IsActive = true, Email = "waiter@example.test" });
        db.SaveChanges();
    }

    private BillingService Service() => new(db);

    private Order AddOrder(string status, decimal subtotal = 200m, TableBill? bill = null)
    {
        var order = new Order { Id = Guid.NewGuid(), RestaurantId = restaurantId, TableId = table.Id,
            Status = status, Subtotal = subtotal, Tax = Math.Round(subtotal * .05m, 0, MidpointRounding.AwayFromZero),
            TotalAmount = subtotal + Math.Round(subtotal * .05m, 0, MidpointRounding.AwayFromZero),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, TableBillId = bill?.Id,
            OrderItems = [new OrderItem { Id = Guid.NewGuid(), ItemNameSnapshot = "Lunch", Quantity = 2, UnitPrice = subtotal / 2 }] };
        db.Orders.Add(order);
        db.SaveChanges();
        return order;
    }

    private async Task<TableBill> OpenBill()
    {
        await Service().OpenAsync(restaurantId, table.Id, actorId);
        return await db.TableBills.SingleAsync();
    }

    [Fact]
    public async Task ListServiceTables_ShowsServedGroupUntilItsBillIsSettled()
    {
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();
        var preparingBill = new TableBill { RestaurantId = restaurantId, TableId = table.Id, DiningGroupId = groupA, GroupLabelSnapshot = "A" };
        var servedBill = new TableBill { RestaurantId = restaurantId, TableId = table.Id, DiningGroupId = groupB, GroupLabelSnapshot = "B" };
        db.TableBills.AddRange(preparingBill, servedBill);
        AddOrder("Preparing", bill: preparingBill).DiningGroupId = groupA;
        AddOrder("Served", bill: servedBill).DiningGroupId = groupB;
        await db.SaveChangesAsync();

        var current = Assert.Single(await Service().ListServiceTablesAsync(restaurantId));
        Assert.Equal("Bill Ready", current.Status);
        Assert.Equal(1, current.ActiveOrdersCount);
        Assert.Equal(new BillReadyGroupDto(groupB, "B"), Assert.Single(current.BillReadyGroups));

        servedBill.SettledAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        current = Assert.Single(await Service().ListServiceTablesAsync(restaurantId));
        Assert.Equal("Occupied", current.Status);
        Assert.Empty(current.BillReadyGroups);
    }

    [Fact]
    public async Task OpenBill_ImportsOnlyActiveLegacyOrders()
    {
        var served = AddOrder("Served");
        var active = AddOrder("Preparing");
        await OpenBill();
        Assert.Null(served.TableBillId);
        Assert.NotNull(active.TableBillId);
    }

    [Fact]
    public async Task Review_KeepsServedOrdersAndExcludesCancelledAmounts()
    {
        AddOrder("Placed");
        var bill = await OpenBill();
        var original = await db.Orders.SingleAsync();
        original.Status = "Served";
        AddOrder("Served", 100m, bill);
        AddOrder("Cancelled", 500m, bill);
        var review = await Service().ReviewAsync(restaurantId, table.Id);
        Assert.Equal(315m, review.OpenBill!.TotalAmount);
        Assert.True(review.OpenBill.CanSettle);
        Assert.Equal(3, review.OpenBill.Orders.Count);
    }

    [Fact]
    public async Task Settlement_RejectsUnservedOrders()
    {
        AddOrder("Preparing");
        await OpenBill();
        var bill = (await Service().ReviewAsync(restaurantId, table.Id)).OpenBill!;
        await Assert.ThrowsAsync<ServiceConflictException>(() => Service().SettleAsync(restaurantId, bill.Id, actorId,
            new SettleBillRequestDto { OperationId = Guid.NewGuid(), ExpectedRevision = bill.Revision, ExpectedTotal = 210, PaymentMethod = "Cash" }));
        Assert.Null((await db.TableBills.SingleAsync()).SettledAtUtc);
    }

    [Fact]
    public async Task Settlement_RecordsFullPaymentWithoutChangingOrderStatus_AndReplays()
    {
        var order = AddOrder("Placed");
        await OpenBill();
        order.Status = "Served";
        await db.SaveChangesAsync();
        var bill = (await Service().ReviewAsync(restaurantId, table.Id)).OpenBill!;
        var request = new SettleBillRequestDto { OperationId = Guid.NewGuid(), ExpectedRevision = bill.Revision, ExpectedTotal = 210, PaymentMethod = "Cash" };
        var first = await Service().SettleAsync(restaurantId, bill.Id, actorId, request);
        var replay = await Service().SettleAsync(restaurantId, bill.Id, actorId, request);
        Assert.Equal("Settled", first.Status);
        Assert.Equal(first.SettledAtUtc, replay.SettledAtUtc);
        Assert.Equal(210m, first.TotalAmount);
        Assert.Equal("Served", order.Status);
        Assert.Equal(actorId, (await db.TableBills.SingleAsync()).SettledByEmployeeId);
        Assert.Equal("Test Restaurant", first.RestaurantName);
        (await db.Restaurants.SingleAsync()).Name = "Renamed after payment";
        table.TableNumber = 99;
        await db.SaveChangesAsync();
        var historical = await Service().ReceiptAsync(restaurantId, bill.Id);
        Assert.Equal("Test Restaurant", historical.RestaurantName);
        Assert.Equal(4, historical.TableNumber);
        Assert.Equal(actorId, historical.SettledByEmployeeId);
    }

    [Fact]
    public async Task Settlement_RejectsStaleReviewEvenWhenAmountIsUnchanged()
    {
        var order = AddOrder("Placed");
        var entity = await OpenBill();
        order.Status = "Served";
        await db.SaveChangesAsync();
        var old = (await Service().ReviewAsync(restaurantId, table.Id)).OpenBill!;
        AddOrder("Cancelled", 100, entity);
        await Assert.ThrowsAsync<ServiceConflictException>(() => Service().SettleAsync(restaurantId, old.Id, actorId,
            new SettleBillRequestDto { OperationId = Guid.NewGuid(), ExpectedRevision = old.Revision, ExpectedTotal = 210, PaymentMethod = "UPI" }));
    }

    [Fact]
    public async Task Settlement_RejectsChangedReplayPayload()
    {
        var order = AddOrder("Placed");
        await OpenBill();
        order.Status = "Served";
        await db.SaveChangesAsync();
        var bill = (await Service().ReviewAsync(restaurantId, table.Id)).OpenBill!;
        var request = new SettleBillRequestDto { OperationId = Guid.NewGuid(), ExpectedRevision = bill.Revision, ExpectedTotal = 210, PaymentMethod = "Cash" };
        await Service().SettleAsync(restaurantId, bill.Id, actorId, request);
        request.PaymentMethod = "Card";
        await Assert.ThrowsAsync<ServiceConflictException>(() => Service().SettleAsync(restaurantId, bill.Id, actorId, request));
    }

    [Fact]
    public async Task ForeignRestaurant_CannotReviewOrSettleBill()
    {
        AddOrder("Placed");
        var bill = await OpenBill();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service().ReviewAsync(Guid.NewGuid(), table.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service().SettleAsync(Guid.NewGuid(), bill.Id, actorId,
            new SettleBillRequestDto { OperationId = Guid.NewGuid(), ExpectedRevision = "x", PaymentMethod = "Cash" }));
    }

    [Fact]
    public async Task Review_PreservesPerOrderTaxRounding()
    {
        var first = AddOrder("Placed", 10m);
        var bill = await OpenBill();
        first.Status = "Served";
        AddOrder("Served", 10m, bill);
        var result = (await Service().ReviewAsync(restaurantId, table.Id)).OpenBill!;
        Assert.Equal(20m, result.Subtotal);
        Assert.Equal(2m, result.Tax);
        Assert.Equal(22m, result.TotalAmount);
    }

    [Fact]
    public async Task FullyCancelledBill_CanCloseWithZeroPayment()
    {
        var order = AddOrder("Placed");
        await OpenBill();
        order.Status = "Cancelled";
        await db.SaveChangesAsync();
        var bill = (await Service().ReviewAsync(restaurantId, table.Id)).OpenBill!;
        Assert.Equal(0m, bill.TotalAmount);
        var result = await Service().SettleAsync(restaurantId, bill.Id, actorId,
            new SettleBillRequestDto { OperationId = Guid.NewGuid(), ExpectedRevision = bill.Revision, ExpectedTotal = 0, PaymentMethod = "Cash" });
        Assert.Equal("Settled", result.Status);
        Assert.Equal("Cancelled", order.Status);
    }

    [Fact]
    public async Task Settlement_RejectsAmountThatDiffersFromReviewedBill()
    {
        var order = AddOrder("Placed");
        await OpenBill();
        order.Status = "Served";
        await db.SaveChangesAsync();
        var bill = (await Service().ReviewAsync(restaurantId, table.Id)).OpenBill!;
        await Assert.ThrowsAsync<ServiceConflictException>(() => Service().SettleAsync(restaurantId, bill.Id, actorId,
            new SettleBillRequestDto { OperationId = Guid.NewGuid(), ExpectedRevision = bill.Revision, ExpectedTotal = 1, PaymentMethod = "Cash" }));
    }

    [Fact]
    public async Task InactiveActor_CannotOpenBill()
    {
        (await db.Employees.SingleAsync()).IsActive = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service().OpenAsync(restaurantId, table.Id, actorId));
    }

    public void Dispose() => db.Dispose();
}
