using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Hubs;
using Tablo.Api.Services;

namespace Tablo.Api.Tests;

public class BillingNotificationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Settlement_CompletesAndNotifiesOnlyItsBillRequests_AndSurvivesSignalRFailure(bool failNotification)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var restaurant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var table = new RestaurantTable { Id = Guid.NewGuid(), RestaurantId = restaurant, TableNumber = 4 };
        db.Restaurants.Add(new Restaurant { Id=restaurant, Name="Notification test", ContactEmail="notify@example.test" });
        db.RestaurantTables.Add(table);
        db.Employees.Add(new Employee { Id = actor, RestaurantId = restaurant, Role = new Role { Name = "Waiter" }, IsActive = true });
        db.Orders.Add(new Order { Id = Guid.NewGuid(), RestaurantId = restaurant, TableId = table.Id, Status = "Placed", Subtotal = 100, Tax = 5, TotalAmount = 105 });
        var billRequest = new ServiceRequest { Id = Guid.NewGuid(), RestaurantId = restaurant, TableId = table.Id, Type = "RequestBill", Status = "Pending" };
        var waiterRequest = new ServiceRequest { Id = Guid.NewGuid(), RestaurantId = restaurant, TableId = table.Id, Type = "CallWaiter", Status = "Pending" };
        var otherTableRequest = new ServiceRequest { Id = Guid.NewGuid(), RestaurantId = restaurant, TableId = Guid.NewGuid(), Type = "RequestBill", Status = "Pending" };
        db.ServiceRequests.AddRange(billRequest, waiterRequest, otherTableRequest);
        await db.SaveChangesAsync();
        var seen = new List<ServiceRequestDto>();
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync("ServiceRequestUpdated", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns<string, object?[], CancellationToken>((_, args, _) =>
            {
                seen.Add(Assert.IsType<ServiceRequestDto>(args[0]));
                return failNotification ? Task.FromException(new IOException("Simulated connection failure")) : Task.CompletedTask;
            });
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group($"restaurant_{restaurant}")).Returns(proxy.Object);
        var hub = new Mock<IHubContext<OrderHub>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);
        var service = new BillingService(db, hub.Object, NullLogger<BillingService>.Instance);
        await service.OpenAsync(restaurant, table.Id, actor);
        (await db.Orders.SingleAsync()).Status = "Served";
        await db.SaveChangesAsync();
        var bill = (await service.ReviewAsync(restaurant, table.Id)).OpenBill!;
        var request = new SettleBillRequestDto { OperationId = Guid.NewGuid(), ExpectedRevision = bill.Revision, ExpectedTotal = 105, PaymentMethod = "Cash" };
        var settled = await service.SettleAsync(restaurant, bill.Id, actor, request);
        Assert.Equal("Settled", settled.Status);
        Assert.NotNull((await db.TableBills.SingleAsync()).SettledAtUtc);
        var message = Assert.Single(seen);
        Assert.Equal(billRequest.Id, message.Id);
        Assert.Equal("Completed", message.Status);
        Assert.Equal("Table 4", message.TableName);
        Assert.Equal("Pending", waiterRequest.Status);
        Assert.Equal("Pending", otherTableRequest.Status);
        await service.SettleAsync(restaurant, bill.Id, actor, request);
        Assert.Single(seen); // Replaying payment must not create a second completion event.
    }
}
