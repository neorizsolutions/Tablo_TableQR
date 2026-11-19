using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Hubs;
using Tablo.Api.Repositories;
using Tablo.Api.Services;
using Xunit;

namespace Tablo.Api.Tests;

public class OrderWorkflowTests
{
    private record Scenario(Guid Restaurant, Guid Table, string QRToken, Guid Actor, Guid Item1, Guid Item2);

    private static ApplicationDbContext Context(string connection) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);

    private static OrderService Orders(ApplicationDbContext db) =>
        new(new OrderRepository(db), Mock.Of<IHubContext<OrderHub>>(), db, NullLogger<OrderService>.Instance);

    private static async Task WithDatabase(Func<string, Scenario, Task> test)
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TABLO_BILLING_TEST_CONNECTION"));
        if (builder.Host != "127.0.0.1" || builder.Port != 55439 || builder.Username != "tablo_test" || builder.Database != "postgres")
            throw new InvalidOperationException("Billing tests require the isolated loopback test cluster on port 55439.");
        builder.Pooling = false;
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        var name = "tablo_workflow_test_" + Guid.NewGuid().ToString("N");
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin)) await create.ExecuteNonQueryAsync();
        builder.Database = name;
        try
        {
            Scenario scenario;
            await using (var db = Context(builder.ConnectionString))
            {
                await db.Database.MigrateAsync();
                var restaurant = new Restaurant { Id = Guid.NewGuid(), Name = "Workflow Test Bistro", ContactEmail = "test@example.test" };
                var settings = new RestaurantSettings { RestaurantId = restaurant.Id, IsAcceptingOrders = true };
                var table = new RestaurantTable { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, TableNumber = 5, IsActive = true, QRToken = Guid.NewGuid().ToString("N") };
                var role = new Role { Id = Guid.NewGuid(), Name = "Waiter" };
                var actor = new Employee { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Role = role, IsActive = true, Email = "waiter@example.test" };
                var category = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Mains" };
                var item1 = new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = category.Id, Name = "Paneer Tikka", Price = 100m, IsAvailable = true };
                var item2 = new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = category.Id, Name = "Butter Naan", Price = 50m, IsAvailable = true };
                db.AddRange(restaurant, settings, table, actor, category, item1, item2);
                await db.SaveChangesAsync();
                scenario = new(restaurant.Id, table.Id, table.QRToken, actor.Id, item1.Id, item2.Id);
            }
            await test(builder.ConnectionString, scenario);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [BillingPostgresFact]
    public Task StaffOrder_StartsAsAccepted_AndAllowsAdvanceToPreparing() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var staffOrdering = new StaffOrderingService(db, Orders(db));

        var request = new StaffOrderRequestDto
        {
            OperationId = Guid.NewGuid(),
            TableId = s.Table,
            Items = [new OrderItemRequestDto { MenuItemId = s.Item1, Quantity = 2 }]
        };

        var response = await staffOrdering.SubmitAsync(s.Restaurant, s.Actor, request);

        // 1. Staff order starts as Accepted
        Assert.Equal("Accepted", response.Status);

        var savedOrder = await db.Orders.Include(o => o.OrderItems).SingleAsync(o => o.Id == response.OrderId);
        Assert.Equal("Accepted", savedOrder.Status);
        Assert.Equal(200m, savedOrder.Subtotal);
        Assert.Equal(10m, savedOrder.Tax); // 5% of 200
        Assert.Equal(210m, savedOrder.TotalAmount);

        // 2. Next action is Mark as Preparing (valid transition Accepted -> Preparing)
        var updated = await Orders(db).UpdateOrderStatusAsync(s.Restaurant, savedOrder.Id, new UpdateOrderStatusRequestDto
        {
            RestaurantId = s.Restaurant,
            Status = "Preparing"
        });
        Assert.Equal("Preparing", updated.Status);

        // 3. Attempting invalid jump from Preparing to Placed or Accepted throws ArgumentException
        await Assert.ThrowsAsync<ArgumentException>(() => Orders(db).UpdateOrderStatusAsync(s.Restaurant, savedOrder.Id, new UpdateOrderStatusRequestDto
        {
            RestaurantId = s.Restaurant,
            Status = "Accepted"
        }));
    });

    [BillingPostgresFact]
    public Task CustomerTableQrOrder_StartsAsPlaced_RequiresStaffAcceptance() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);

        var qrRequest = new CreateOrderRequestDto
        {
            QRToken = s.QRToken,
            Items = [new OrderItemRequestDto { MenuItemId = s.Item1, Quantity = 1 }]
        };

        // When customer places an order from table QR, isStaff defaults to false
        var response = await Orders(db).CreateOrderAsync(qrRequest, isStaff: false);

        // 1. Starts as Placed (shown as New)
        Assert.Equal("Placed", response.Status);

        var savedOrder = await db.Orders.SingleAsync(o => o.Id == response.OrderId);
        Assert.Equal("Placed", savedOrder.Status);

        // 2. Kitchen cannot jump directly from Placed to Preparing
        await Assert.ThrowsAsync<ArgumentException>(() => Orders(db).UpdateOrderStatusAsync(s.Restaurant, savedOrder.Id, new UpdateOrderStatusRequestDto
        {
            RestaurantId = s.Restaurant,
            Status = "Preparing"
        }));

        // 3. Staff must click "Mark as Accepted" first
        var accepted = await Orders(db).UpdateOrderStatusAsync(s.Restaurant, savedOrder.Id, new UpdateOrderStatusRequestDto
        {
            RestaurantId = s.Restaurant,
            Status = "Accepted"
        });
        Assert.Equal("Accepted", accepted.Status);

        // 4. Now preparation can begin (next action: Mark as Preparing)
        var preparing = await Orders(db).UpdateOrderStatusAsync(s.Restaurant, savedOrder.Id, new UpdateOrderStatusRequestDto
        {
            RestaurantId = s.Restaurant,
            Status = "Preparing"
        });
        Assert.Equal("Preparing", preparing.Status);
    });

    [BillingPostgresFact]
    public Task StaffAddItems_ToExistingAcceptedOrder_PreservesAcceptedStatus() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var staffOrdering = new StaffOrderingService(db, Orders(db));

        // Initial staff order
        var first = await staffOrdering.SubmitAsync(s.Restaurant, s.Actor, new StaffOrderRequestDto
        {
            OperationId = Guid.NewGuid(),
            TableId = s.Table,
            Items = [new OrderItemRequestDto { MenuItemId = s.Item1, Quantity = 1 }]
        });
        Assert.Equal("Accepted", first.Status);

        // Staff adds more items
        var second = await staffOrdering.SubmitAsync(s.Restaurant, s.Actor, new StaffOrderRequestDto
        {
            OperationId = Guid.NewGuid(),
            TableId = s.Table,
            Items = [new OrderItemRequestDto { MenuItemId = s.Item2, Quantity = 2 }]
        });

        // Appended to existing order, status preserved as Accepted
        Assert.Equal(first.OrderId, second.OrderId);
        Assert.Equal("Accepted", second.Status);

        await using var check = Context(connection);
        var savedOrder = await check.Orders.Include(o => o.OrderItems).SingleAsync(o => o.Id == first.OrderId);
        Assert.Equal("Accepted", savedOrder.Status);
        Assert.Equal(2, savedOrder.OrderItems.Count); // 1 paneer + 2 naan
        Assert.Equal(200m, savedOrder.Subtotal); // 100 + 100
        Assert.Equal(10m, savedOrder.Tax);
        Assert.Equal(210m, savedOrder.TotalAmount);
    });

    [BillingPostgresFact]
    public Task StaffAddItems_ToExistingPreparingOrder_PreservesPreparingStatus() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var staffOrdering = new StaffOrderingService(db, Orders(db));

        var first = await staffOrdering.SubmitAsync(s.Restaurant, s.Actor, new StaffOrderRequestDto
        {
            OperationId = Guid.NewGuid(),
            TableId = s.Table,
            Items = [new OrderItemRequestDto { MenuItemId = s.Item1, Quantity = 1 }]
        });

        // Kitchen advances to Preparing
        await Orders(db).UpdateOrderStatusAsync(s.Restaurant, first.OrderId, new UpdateOrderStatusRequestDto
        {
            RestaurantId = s.Restaurant,
            Status = "Preparing"
        });

        // Staff adds items while order is Preparing
        var second = await staffOrdering.SubmitAsync(s.Restaurant, s.Actor, new StaffOrderRequestDto
        {
            OperationId = Guid.NewGuid(),
            TableId = s.Table,
            Items = [new OrderItemRequestDto { MenuItemId = s.Item2, Quantity = 1 }]
        });

        Assert.Equal(first.OrderId, second.OrderId);
        Assert.Equal("Preparing", second.Status);

        await using var check = Context(connection);
        var order = await check.Orders.Include(o => o.OrderItems).SingleAsync(o => o.Id == first.OrderId);
        Assert.Equal("Preparing", order.Status);
        Assert.Equal(2, order.OrderItems.Count);
        Assert.Equal(150m, order.Subtotal); // 100 + 50
    });

    [BillingPostgresFact]
    public Task PendingQrOrder_PlusStaffAddition_KeepsQrPending_AndCreatesSeparateAcceptedTicketOnSameBill() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var staffOrdering = new StaffOrderingService(db, Orders(db));

        // 1. Customer places order from Table QR -> starts as Placed
        var qrResponse = await Orders(db).CreateOrderAsync(new CreateOrderRequestDto
        {
            QRToken = s.QRToken,
            Items = [new OrderItemRequestDto { MenuItemId = s.Item1, Quantity = 1 }] // Paneer Tikka (100)
        }, isStaff: false);

        Assert.Equal("Placed", qrResponse.Status);
        var qrOrderId = qrResponse.OrderId;

        // Verify initial bill created
        var qrOrder = await db.Orders.SingleAsync(o => o.Id == qrOrderId);
        Assert.NotNull(qrOrder.TableBillId);
        var initialBillId = qrOrder.TableBillId.Value;

        // 2. While QR order is still pending/unaccepted (Placed), staff adds items via Orders & billing
        var staffResponse = await staffOrdering.SubmitAsync(s.Restaurant, s.Actor, new StaffOrderRequestDto
        {
            OperationId = Guid.NewGuid(),
            TableId = s.Table,
            Items = [new OrderItemRequestDto { MenuItemId = s.Item2, Quantity = 2 }] // 2x Butter Naan (100)
        });

        // 3. Staff items are on a separate accepted ticket
        Assert.NotEqual(qrOrderId, staffResponse.OrderId);
        Assert.Equal("Accepted", staffResponse.Status);

        await using var check = Context(connection);

        // 4. Verify QR order remains pending (Placed)
        var refreshedQrOrder = await check.Orders.Include(o => o.OrderItems).SingleAsync(o => o.Id == qrOrderId);
        Assert.Equal("Placed", refreshedQrOrder.Status);
        Assert.Single(refreshedQrOrder.OrderItems);
        Assert.Equal(s.Item1, refreshedQrOrder.OrderItems.First().MenuItemId);

        // 5. Verify staff order is Accepted
        var staffOrder = await check.Orders.Include(o => o.OrderItems).SingleAsync(o => o.Id == staffResponse.OrderId);
        Assert.Equal("Accepted", staffOrder.Status);
        Assert.Single(staffOrder.OrderItems);
        Assert.Equal(s.Item2, staffOrder.OrderItems.First().MenuItemId);

        // 6. Verify BOTH orders are attached to the same open TableBill
        Assert.Equal(initialBillId, staffOrder.TableBillId);
        Assert.Equal(refreshedQrOrder.TableBillId, staffOrder.TableBillId);

        // 7. Verify bill review includes both tickets and bills each item exactly once
        var billing = new BillingService(check);
        var review = await billing.ReviewAsync(s.Restaurant, s.Table);
        Assert.NotNull(review.OpenBill);
        Assert.Equal(initialBillId, review.OpenBill.Id);

        // Subtotal: 100 (Paneer) + 100 (2x Naan) = 200
        // Tax: 5% of 200 = 10
        // Total: 210
        Assert.Equal(200m, review.OpenBill.Subtotal);
        Assert.Equal(10m, review.OpenBill.Tax);
        Assert.Equal(210m, review.OpenBill.TotalAmount);
        Assert.Equal(2, review.OpenBill.Orders.Count);

        var billOrders = review.OpenBill.Orders;
        var billQrOrder = billOrders.Single(o => o.Id == qrOrderId);
        Assert.Equal("Placed", billQrOrder.Status);
        Assert.Single(billQrOrder.Items);
        Assert.Equal("Paneer Tikka", billQrOrder.Items[0].Name);
        Assert.Equal(1, billQrOrder.Items[0].Quantity);

        var billStaffOrder = billOrders.Single(o => o.Id == staffResponse.OrderId);
        Assert.Equal("Accepted", billStaffOrder.Status);
        Assert.Single(billStaffOrder.Items);
        Assert.Equal("Butter Naan", billStaffOrder.Items[0].Name);
        Assert.Equal(2, billStaffOrder.Items[0].Quantity);

        // Total items across both orders: 3 items (1 paneer + 2 naan)
        Assert.Equal(3, billOrders.SelectMany(o => o.Items).Sum(i => i.Quantity));
    });

    [BillingPostgresFact]
    public Task CustomerOrder_WhenTableHasAcceptedOrder_CreatesSeparatePlacedOrder() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var staffOrdering = new StaffOrderingService(db, Orders(db));

        // Staff creates an Accepted order
        var staffResult = await staffOrdering.SubmitAsync(s.Restaurant, s.Actor, new StaffOrderRequestDto
        {
            OperationId = Guid.NewGuid(),
            TableId = s.Table,
            Items = [new OrderItemRequestDto { MenuItemId = s.Item1, Quantity = 1 }]
        });
        Assert.Equal("Accepted", staffResult.Status);

        // Customer subsequently places an order via table QR
        var customerResult = await Orders(db).CreateOrderAsync(new CreateOrderRequestDto
        {
            QRToken = s.QRToken,
            Items = [new OrderItemRequestDto { MenuItemId = s.Item2, Quantity = 1 }]
        }, isStaff: false);

        // Customer order cannot bypass acceptance into the Accepted order; it starts as Placed on a separate ticket
        Assert.NotEqual(staffResult.OrderId, customerResult.OrderId);
        Assert.Equal("Placed", customerResult.Status);

        await using var check = Context(connection);
        var customerOrder = await check.Orders.SingleAsync(o => o.Id == customerResult.OrderId);
        Assert.Equal("Placed", customerOrder.Status);

        var staffOrder = await check.Orders.SingleAsync(o => o.Id == staffResult.OrderId);
        Assert.Equal("Accepted", staffOrder.Status);

        // Both attached to the same bill
        Assert.Equal(staffOrder.TableBillId, customerOrder.TableBillId);
    });
}
