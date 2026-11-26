using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Hubs;
using Tablo.Api.Interfaces;
using Tablo.Api.Repositories;
using Tablo.Api.Services;

namespace Tablo.Api.Tests;

public class CategoryKitchenAssignmentTests
{
    [Theory]
    [InlineData("active")]
    [InlineData("inactive")]
    [InlineData("foreign")]
    [InlineData("missing")]
    public async Task CreateAndEdit_OnlyAcceptActiveKitchensFromSameRestaurant(string kind)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var restaurant = new Restaurant { Name = "Kitchen test" };
        var other = new Restaurant { Name = "Other restaurant" };
        db.Restaurants.AddRange(restaurant, other);
        var station = new KitchenStation { RestaurantId = kind == "foreign" ? other.Id : restaurant.Id,
            Name = "Beverage Station", Code = "BEV", IsActive = kind != "inactive" };
        if (kind != "missing") db.KitchenStations.Add(station);
        var category = new Category { RestaurantId = restaurant.Id, Name = "Existing" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(Mock.Of<IClientProxy>());
        var hub = new Mock<IHubContext<OrderHub>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);
        var service = new MenuService(new MenuRepository(db), hub.Object, Mock.Of<IImageStorageService>());
        var create = new CreateCategoryRequestDto { Name = "Juices", KitchenStationId = station.Id };
        var update = new UpdateCategoryRequestDto { Name = "Tea", KitchenStationId = station.Id };
        if (kind == "active")
        {
            var created = await service.CreateCategoryAsync(restaurant.Id, create);
            Assert.Equal(station.Id, created.KitchenStationId);
            var edited = await service.UpdateCategoryAsync(category.Id, update);
            Assert.Equal(station.Id, edited.KitchenStationId);
            var reset = await service.UpdateCategoryAsync(category.Id, new UpdateCategoryRequestDto { Name = "Tea", KitchenStationId = null });
            Assert.Null(reset.KitchenStationId);
        }
        else
        {
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCategoryAsync(restaurant.Id, create));
            await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateCategoryAsync(category.Id, update));
            Assert.Null(category.KitchenStationId);
            Assert.Equal("Existing", category.Name);
            Assert.Single(await db.Categories.ToListAsync());
        }
    }
}
