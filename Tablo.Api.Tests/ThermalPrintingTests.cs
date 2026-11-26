using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tablo.Api.Constants;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Services;
using Tablo.Api.Services.Printing;
using Xunit;

namespace Tablo.Api.Tests;

public class ThermalPrintingTests
{
    private ApplicationDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "Tablo_Thermal_Tests_" + Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void EscPosFormatter_Formats58mmAnd80mm_GeneratesBinaryAndReadableText()
    {
        var items = new List<KotTicketItemSnapshotDto>
        {
            new() { Name = "Butter Chicken", Quantity = 2, UnitPrice = 350, SpecialInstructions = "Less spicy" },
            new() { Name = "Garlic Naan", Quantity = 4, UnitPrice = 60 }
        };

        // 80mm test
        var (bytes80, text80) = EscPosFormatter.FormatKotTicket(
            restaurantName: "Biryani Express",
            stationName: "Main Kitchen",
            ticketNumber: "KOT-0010-1-MAIN",
            orderNumber: 10,
            tableNumber: 4,
            groupLabel: "4A",
            ticketType: "New",
            isReprint: false,
            reprintCount: 0,
            reprintReason: null,
            timestampUtc: new DateTime(2026, 11, 19, 10, 30, 0, DateTimeKind.Utc),
            items: items,
            notes: "Rush order",
            paperWidthMm: 80);

        Assert.NotNull(bytes80);
        Assert.True(bytes80.Length > 20);
        // Verify ESC @ init and GS V cut bytes
        Assert.Equal(0x1B, bytes80[0]);
        Assert.Equal(0x40, bytes80[1]);
        Assert.Contains("BIRYANI EXPRESS", text80);
        Assert.Contains("*** MAIN KITCHEN ***", text80);
        Assert.Contains("Table 4 (4A)", text80);
        Assert.Contains("2x Butter Chicken", text80);
        Assert.Contains("Less spicy", text80);
        Assert.Contains("Total Items: 6", text80);
        Assert.Contains("IST", text80);

        // 58mm test
        var (bytes58, text58) = EscPosFormatter.FormatKotTicket(
            restaurantName: "Chai Point",
            stationName: "Beverage Station",
            ticketNumber: "KOT-0010-1-BEV",
            orderNumber: 10,
            tableNumber: 2,
            groupLabel: null,
            ticketType: "New",
            isReprint: false,
            reprintCount: 0,
            reprintReason: null,
            timestampUtc: new DateTime(2026, 11, 19, 10, 30, 0, DateTimeKind.Utc),
            items: new List<KotTicketItemSnapshotDto> { new() { Name = "Masala Chai", Quantity = 1, UnitPrice = 40 } },
            notes: null,
            paperWidthMm: 58);

        Assert.NotNull(bytes58);
        Assert.Contains("CHAI POINT", text58);
        Assert.Contains("*** BEVERAGE STATION ***", text58);
        Assert.Contains("Table 2", text58);
    }

    [Fact]
    public void EscPosFormatter_ReprintMarkedClearlyWithReason()
    {
        var items = new List<KotTicketItemSnapshotDto>
        {
            new() { Name = "Masala Dosa", Quantity = 1, UnitPrice = 120 }
        };

        var (bytes, text) = EscPosFormatter.FormatKotTicket(
            restaurantName: "South Treat",
            stationName: "Food Kitchen",
            ticketNumber: "KOT-0015-1-FOOD-R1",
            orderNumber: 15,
            tableNumber: 3,
            groupLabel: null,
            ticketType: "Reprint",
            isReprint: true,
            reprintCount: 1,
            reprintReason: "Paper jam on kitchen station",
            timestampUtc: DateTime.UtcNow,
            items: items,
            notes: null,
            paperWidthMm: 80);

        Assert.Contains("*** REPRINT ***", text);
        Assert.Contains("Reason: Paper jam on kitchen station", text);
    }

    [Fact]
    public async Task KotRouting_SingleKitchen_RoutesAllItemsToOneStation()
    {
        using var db = CreateInMemoryDb();
        var restId = Guid.NewGuid();
        var restaurant = new Restaurant { Id = restId, Name = "Solo Kitchen Diner" };
        db.Restaurants.Add(restaurant);

        var printer = new ThermalPrinter
        {
            Id = Guid.NewGuid(),
            RestaurantId = restId,
            Name = "Kitchen Printer 80mm",
            Status = "Online",
            PaperWidthMm = 80,
            CharactersPerLine = 48,
            IsActive = true
        };
        db.ThermalPrinters.Add(printer);

        var station = new KitchenStation
        {
            Id = Guid.NewGuid(),
            RestaurantId = restId,
            Name = "Main Kitchen",
            Code = "MAIN",
            IsDefault = true,
            IsActive = true,
            PrinterId = printer.Id
        };
        db.KitchenStations.Add(station);

        var settings = new RestaurantSettings
        {
            RestaurantId = restId,
            KotPrintingEnabled = true,
            KitchenSetupMode = "SingleKitchen",
            DefaultKitchenStationId = station.Id
        };
        db.RestaurantSettings.Add(settings);

        var foodCat = new Category { Id = Guid.NewGuid(), RestaurantId = restId, Name = "Food" };
        var bevCat = new Category { Id = Guid.NewGuid(), RestaurantId = restId, Name = "Beverages" };
        db.Categories.AddRange(foodCat, bevCat);

        var burger = new MenuItem { Id = Guid.NewGuid(), RestaurantId = restId, CategoryId = foodCat.Id, Name = "Veg Burger", Price = 150, IsAvailable = true };
        var cola = new MenuItem { Id = Guid.NewGuid(), RestaurantId = restId, CategoryId = bevCat.Id, Name = "Cola", Price = 50, IsAvailable = true };
        db.MenuItems.AddRange(burger, cola);

        var table = new RestaurantTable { Id = Guid.NewGuid(), RestaurantId = restId, TableNumber = 5, QRToken = "token5" };
        db.RestaurantTables.Add(table);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            RestaurantId = restId,
            TableId = table.Id,
            OrderNumber = 42,
            Status = "Accepted",
            OrderItems = new List<OrderItem>()
        };
        var item1 = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, MenuItemId = burger.Id, ItemNameSnapshot = "Veg Burger", Quantity = 2, UnitPrice = 150 };
        var item2 = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, MenuItemId = cola.Id, ItemNameSnapshot = "Cola", Quantity = 1, UnitPrice = 50 };
        order.OrderItems.Add(item1);
        order.OrderItems.Add(item2);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var subService = new SubscriptionService(db, NullLogger<SubscriptionService>.Instance);
        var routingService = new KotRoutingService(db, subService, NullLogger<KotRoutingService>.Instance);

        var tickets = await routingService.ProcessAcceptedOrderItemsAsync(order, new List<OrderItem> { item1, item2 });

        // Single kitchen should produce exactly ONE ticket with BOTH food and drinks
        Assert.Single(tickets);
        Assert.Equal("MAIN", tickets[0].KitchenStationId == station.Id ? "MAIN" : "OTHER");
        Assert.Equal("KOT-0042-1-MAIN", tickets[0].TicketNumber);

        var queuedJobs = await db.PrintJobs.Where(j => j.RestaurantId == restId).ToListAsync();
        Assert.Single(queuedJobs);
        Assert.Equal("Pending", queuedJobs[0].Status);
        Assert.Contains("2x Veg Burger", queuedJobs[0].ReadableText);
        Assert.Contains("1x Cola", queuedJobs[0].ReadableText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KotRouting_SeparateKitchens_RoutesFoodAndBeveragesToSeparateStations(bool itemOverride)
    {
        using var db = CreateInMemoryDb();
        var restId = Guid.NewGuid();
        var restaurant = new Restaurant { Id = restId, Name = "Dual Kitchen Bistro" };
        db.Restaurants.Add(restaurant);

        // Add Premium plan so multiple stations are entitled
        var premiumPlan = new Plan
        {
            Id = Guid.NewGuid(),
            Code = "premium",
            Name = "Premium Plan",
            MaxKitchenStations = 5,
            FeaturesJson = JsonSerializer.Serialize(Capabilities.PremiumDefaults)
        };
        db.Plans.Add(premiumPlan);
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            RestaurantId = restId,
            PlanId = premiumPlan.Id,
            Status = SubscriptionStatus.Active
        });

        var foodPrinter = new ThermalPrinter { Id = Guid.NewGuid(), RestaurantId = restId, Name = "Food Printer", Status = "Online", PaperWidthMm = 80, IsActive = true };
        var barPrinter = new ThermalPrinter { Id = Guid.NewGuid(), RestaurantId = restId, Name = "Bar Printer", Status = "Online", PaperWidthMm = 58, IsActive = true };
        db.ThermalPrinters.AddRange(foodPrinter, barPrinter);

        var foodStation = new KitchenStation { Id = Guid.NewGuid(), RestaurantId = restId, Name = "Food Kitchen", Code = "FOOD", IsDefault = true, IsActive = true, PrinterId = foodPrinter.Id };
        var bevStation = new KitchenStation { Id = Guid.NewGuid(), RestaurantId = restId, Name = "Beverage Station", Code = "BEV", IsDefault = false, IsActive = true, PrinterId = barPrinter.Id };
        db.KitchenStations.AddRange(foodStation, bevStation);

        var settings = new RestaurantSettings
        {
            RestaurantId = restId,
            KotPrintingEnabled = true,
            KitchenSetupMode = "SeparateFoodBeverage",
            DefaultKitchenStationId = foodStation.Id
        };
        db.RestaurantSettings.Add(settings);

        var foodCat = new Category { Id = Guid.NewGuid(), RestaurantId = restId, Name = "Food", KitchenStationId = foodStation.Id };
        var bevCat = new Category { Id = Guid.NewGuid(), RestaurantId = restId, Name = "Beverages", KitchenStationId = bevStation.Id };
        db.Categories.AddRange(foodCat, bevCat);

        var pasta = new MenuItem { Id = Guid.NewGuid(), RestaurantId = restId, CategoryId = foodCat.Id, Name = "Pasta Alfredo", Price = 280, IsAvailable = true };
        var mocktail = new MenuItem { Id = Guid.NewGuid(), RestaurantId = restId, CategoryId = bevCat.Id, Name = "Virgin Mojito", Price = 120, IsAvailable = true, KitchenStationId = itemOverride ? bevStation.Id : null };
        db.MenuItems.AddRange(pasta, mocktail);

        var table = new RestaurantTable { Id = Guid.NewGuid(), RestaurantId = restId, TableNumber = 7, QRToken = "token7" };
        db.RestaurantTables.Add(table);

        var order = new Order { Id = Guid.NewGuid(), RestaurantId = restId, TableId = table.Id, OrderNumber = 55, Status = "Accepted", OrderItems = new List<OrderItem>() };
        var item1 = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, MenuItemId = pasta.Id, ItemNameSnapshot = "Pasta Alfredo", Quantity = 1, UnitPrice = 280 };
        var item2 = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, MenuItemId = mocktail.Id, ItemNameSnapshot = "Virgin Mojito", Quantity = 2, UnitPrice = 120 };
        order.OrderItems.Add(item1);
        order.OrderItems.Add(item2);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var subService = new SubscriptionService(db, NullLogger<SubscriptionService>.Instance);
        var routingService = new KotRoutingService(db, subService, NullLogger<KotRoutingService>.Instance);

        var tickets = await routingService.ProcessAcceptedOrderItemsAsync(order, new List<OrderItem> { item1, item2 });

        // Should produce TWO station tickets: one for FOOD, one for BEV
        Assert.Equal(2, tickets.Count);

        var foodTicket = tickets.FirstOrDefault(t => t.KitchenStationId == foodStation.Id);
        var bevTicket = tickets.FirstOrDefault(t => t.KitchenStationId == bevStation.Id);

        Assert.NotNull(foodTicket);
        Assert.NotNull(bevTicket);
        Assert.Contains("Pasta Alfredo", foodTicket.ItemsJson);
        Assert.DoesNotContain("Virgin Mojito", foodTicket.ItemsJson);
        Assert.Contains("Virgin Mojito", bevTicket.ItemsJson);
        Assert.DoesNotContain("Pasta Alfredo", bevTicket.ItemsJson);

        var jobs = await db.PrintJobs.Where(j => j.RestaurantId == restId).ToListAsync();
        Assert.Equal(2, jobs.Count);
    }

    [Fact]
    public async Task KotRouting_IncrementalAdditions_ProducesIncrementalBatch()
    {
        using var db = CreateInMemoryDb();
        var restId = Guid.NewGuid();
        var restaurant = new Restaurant { Id = restId, Name = "Additions Test Diner" };
        db.Restaurants.Add(restaurant);

        var printer = new ThermalPrinter { Id = Guid.NewGuid(), RestaurantId = restId, Name = "P1", Status = "Online", PaperWidthMm = 80, IsActive = true };
        db.ThermalPrinters.Add(printer);

        var station = new KitchenStation { Id = Guid.NewGuid(), RestaurantId = restId, Name = "Main", Code = "MAIN", IsDefault = true, IsActive = true, PrinterId = printer.Id };
        db.KitchenStations.Add(station);

        db.RestaurantSettings.Add(new RestaurantSettings
        {
            RestaurantId = restId,
            KotPrintingEnabled = true,
            KitchenSetupMode = "SingleKitchen",
            DefaultKitchenStationId = station.Id
        });

        var cat = new Category { Id = Guid.NewGuid(), RestaurantId = restId, Name = "Main" };
        db.Categories.Add(cat);

        var itemA = new MenuItem { Id = Guid.NewGuid(), RestaurantId = restId, CategoryId = cat.Id, Name = "Soup", Price = 100, IsAvailable = true };
        var itemB = new MenuItem { Id = Guid.NewGuid(), RestaurantId = restId, CategoryId = cat.Id, Name = "Bread", Price = 50, IsAvailable = true };
        db.MenuItems.AddRange(itemA, itemB);

        var table = new RestaurantTable { Id = Guid.NewGuid(), RestaurantId = restId, TableNumber = 2, QRToken = "token2" };
        db.RestaurantTables.Add(table);

        var order = new Order { Id = Guid.NewGuid(), RestaurantId = restId, TableId = table.Id, OrderNumber = 88, Status = "Accepted", OrderItems = new List<OrderItem>() };
        var oi1 = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, MenuItemId = itemA.Id, ItemNameSnapshot = "Soup", Quantity = 1, UnitPrice = 100 };
        order.OrderItems.Add(oi1);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var subService = new SubscriptionService(db, NullLogger<SubscriptionService>.Instance);
        var routingService = new KotRoutingService(db, subService, NullLogger<KotRoutingService>.Instance);

        // Batch 1: initial order
        var tickets1 = await routingService.ProcessAcceptedOrderItemsAsync(order, new List<OrderItem> { oi1 });
        Assert.Single(tickets1);
        Assert.Equal("New", tickets1[0].TicketType);
        Assert.Equal("KOT-0088-1-MAIN", tickets1[0].TicketNumber);

        // Batch 2: addition
        var oi2 = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, MenuItemId = itemB.Id, ItemNameSnapshot = "Bread", Quantity = 2, UnitPrice = 50 };
        db.OrderItems.Add(oi2);
        await db.SaveChangesAsync();

        var tickets2 = await routingService.ProcessAcceptedOrderItemsAsync(order, new List<OrderItem> { oi2 });
        Assert.Single(tickets2);
        Assert.Equal("Incremental", tickets2[0].TicketType);
        Assert.Equal("KOT-0088-2-MAIN", tickets2[0].TicketNumber);
        Assert.Contains("Bread", tickets2[0].ItemsJson);
        Assert.DoesNotContain("Soup", tickets2[0].ItemsJson);
    }
}
