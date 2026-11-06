using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Tablo.Api.Controllers;
using Tablo.Api.Hubs;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Interfaces;
using Tablo.Api.Services;

namespace Tablo.Api.Tests;

public class StaffOrderingTests
{
    [Fact]
    public async Task ReplayedSubmission_DoesNotBroadcastItsOriginalStaleStatus()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var restaurant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var table = new RestaurantTable { Id = Guid.NewGuid(), RestaurantId = restaurant, QRToken = "table-qr", IsActive = true };
        db.RestaurantTables.Add(table);
        db.Employees.Add(new Employee { Id = actor, RestaurantId = restaurant, IsActive = true, Role = new Role { Name = "Waiter" } });
        var order = new Order { Id = Guid.NewGuid(), RestaurantId = restaurant, TableId = table.Id, Status = "Placed" };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var orders = new Mock<IOrderService>();
        orders.Setup(o => o.CreateOrderAsync(It.IsAny<CreateOrderRequestDto>())).ReturnsAsync(new OrderResponseDto { OrderId = order.Id, Status = "Placed" });
        var messages = new List<(string Method, object?[] Args)>();
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((method, args, _) => messages.Add((method, args))).Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        var hub = new Mock<IHubContext<OrderHub>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);
        var controller = new ServiceDeskController(db, new BillingService(db), new StaffOrderingService(db, orders.Object), hub.Object,
            NullLogger<ServiceDeskController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("RestaurantId", restaurant.ToString()), new Claim(ClaimTypes.NameIdentifier, actor.ToString()), new Claim(ClaimTypes.Role, "Waiter")], "test")) } } };
        var request = new StaffOrderRequestDto { OperationId = Guid.NewGuid(), TableId = table.Id,
            Items = [new OrderItemRequestDto { MenuItemId = Guid.NewGuid(), Quantity = 1 }] };
        await controller.Submit(restaurant, request);
        order.Status = "Preparing";
        await db.SaveChangesAsync();
        await controller.Submit(restaurant, request);
        Assert.Equal("Preparing", messages.Last(m => m.Method == "OrderStatusUpdated").Args[1]);
    }

    [Fact]
    public async Task RetriedStaffSubmission_StoresOneReceipt_AndChangedPayloadConflicts()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var restaurant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var table = new RestaurantTable { Id = Guid.NewGuid(), RestaurantId = restaurant, QRToken = "table-qr", IsActive = true };
        db.RestaurantTables.Add(table);
        db.Employees.Add(new Employee { Id = actor, RestaurantId = restaurant, IsActive = true, Role = new Role { Name = "Waiter" } });
        await db.SaveChangesAsync();
        var orders = new Mock<IOrderService>();
        var result = new OrderResponseDto { OrderId = Guid.NewGuid(), OrderNumber = 15, TotalAmount = 105, Status = "Placed" };
        orders.Setup(o => o.CreateOrderAsync(It.IsAny<CreateOrderRequestDto>())).ReturnsAsync(result);
        var service = new StaffOrderingService(db, orders.Object);
        var request = new StaffOrderRequestDto { OperationId = Guid.NewGuid(), TableId = table.Id,
            Items = [new OrderItemRequestDto { MenuItemId = Guid.NewGuid(), Quantity = 1 }] };
        var first = await service.SubmitAsync(restaurant, actor, request);
        var second = await service.SubmitAsync(restaurant, actor, request);
        Assert.Equal(first.OrderId, second.OrderId);
        Assert.Single(await db.StaffOrderSubmissions.ToListAsync());
        orders.Verify(o => o.CreateOrderAsync(It.Is<CreateOrderRequestDto>(r => r.QRToken == "table-qr" && r.Items[0].Quantity == 1)), Times.Once);
        request.Items[0].Quantity = 2;
        await Assert.ThrowsAsync<ServiceConflictException>(() => service.SubmitAsync(restaurant, actor, request));
    }

    [Fact]
    public async Task ForeignTable_CannotBeUsedForStaffOrdering()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var restaurant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        db.Employees.Add(new Employee { Id = actor, RestaurantId = restaurant, IsActive = true, Role = new Role { Name = "Admin" } });
        var foreign = new RestaurantTable { Id = Guid.NewGuid(), RestaurantId = Guid.NewGuid(), IsActive = true };
        db.RestaurantTables.Add(foreign);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new StaffOrderingService(db, Mock.Of<IOrderService>()).SubmitAsync(restaurant, actor,
            new StaffOrderRequestDto { OperationId = Guid.NewGuid(), TableId = foreign.Id,
                Items = [new OrderItemRequestDto { MenuItemId = Guid.NewGuid(), Quantity = 1 }] }));
    }
}
