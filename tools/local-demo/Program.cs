using ClosedXML.Excel;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Hubs;
using Tablo.Api.Repositories;
using Tablo.Api.Services;

string clusterDir = args.Length > 0 && Path.IsPathFullyQualified(args[0])
    ? args[0]
    : @"C:\Users\neonk\AppData\Local\Temp\tablo-demo-pg-ecada15c5b5c452f8c633bcfaaeeff27";
string artifactDir = args.Length > 1 && Path.IsPathFullyQualified(args[1])
    ? args[1]
    : Path.Combine(Path.GetTempPath(), "tablo-demo-output-" + Guid.NewGuid().ToString("N"));
string targetDbName = args.Length > 2
    ? args[2]
    : (Environment.GetEnvironmentVariable("TABLO_DEMO_DB") ?? "tablo_demo");

var password = Environment.GetEnvironmentVariable("TABLO_DEMO_PASSWORD");
if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
    password = "password123";

Directory.CreateDirectory(artifactDir);

var serverBuilder = new NpgsqlConnectionStringBuilder
{
    Host = "127.0.0.1", Port = 55439, Database = "postgres", Username = "tablo_test", Pooling = false
};

await using (var server = new NpgsqlConnection(serverBuilder.ConnectionString))
{
    await server.OpenAsync();
    if (Directory.Exists(clusterDir))
    {
        await using var verify = new NpgsqlCommand("SHOW data_directory", server);
        var actual = (string)(await verify.ExecuteScalarAsync())!;
        Check(Path.GetFullPath(actual).Equals(Path.GetFullPath(clusterDir), StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(actual).StartsWith("tablo-demo-pg-", StringComparison.Ordinal),
            "Unexpected cluster directory");
    }

    // Terminate existing connections to targetDbName if any so we can cleanly recreate
    await using var term = new NpgsqlCommand($@"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{targetDbName}' AND pid <> pg_backend_pid();", server);
    await term.ExecuteNonQueryAsync();

    await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{targetDbName}\"", server);
    await drop.ExecuteNonQueryAsync();

    await using var create = new NpgsqlCommand($"CREATE DATABASE \"{targetDbName}\"", server);
    await create.ExecuteNonQueryAsync();
}

var database = new NpgsqlConnectionStringBuilder
{
    Host = "127.0.0.1", Port = 55439, Database = targetDbName, Username = "tablo_test", Pooling = false
};

await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(database.ConnectionString).Options);
await db.Database.MigrateAsync();
DbSeeder.Seed(db, seedDummyData: false);

var restaurantId = Guid.Parse("22222222-0000-0000-0000-000000000001");
var otherRestaurantId = Guid.Parse("22222222-0000-0000-0000-000000000009");

var restaurant = new Restaurant
{
    Id = restaurantId,
    Name = "Tablo Heritage Restaurant",
    Description = "Fine dining, live counter, takeaway & car-side service.",
    ContactEmail = "contact@tablodemo.com",
    ContactPhone = "+91 98765 43210",
    Address = "42 Heritage Lane, MG Road, Bengaluru",
    CreatedAt = DateTime.UtcNow
};

var other = new Restaurant
{
    Id = otherRestaurantId,
    Name = "Tablo Coastal Cafe",
    Description = "Casual seaside café.",
    ContactEmail = "coastal@tablodemo.com",
    ContactPhone = "+91 98765 00009",
    Address = "12 Beach Road, Mangaluru",
    CreatedAt = DateTime.UtcNow
};

var roles = await db.Roles.ToDictionaryAsync(role => role.Name);

var admin = new Employee
{
    Id = Guid.Parse("33333333-0000-0000-0000-000000000001"),
    RestaurantId = restaurant.Id,
    RoleId = roles["Admin"].Id,
    FirstName = "Aditi",
    LastName = "Sharma (Admin)",
    Email = "admin@tablodemo.com",
    IsActive = true,
    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
};
var waiter = new Employee
{
    Id = Guid.Parse("33333333-0000-0000-0000-000000000002"),
    RestaurantId = restaurant.Id,
    RoleId = roles["Waiter"].Id,
    FirstName = "Rohan",
    LastName = "Verma (Waiter)",
    Email = "waiter@tablodemo.com",
    IsActive = true,
    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
};
var kitchen = new Employee
{
    Id = Guid.Parse("33333333-0000-0000-0000-000000000003"),
    RestaurantId = restaurant.Id,
    RoleId = roles["Kitchen"].Id,
    FirstName = "Suresh",
    LastName = "Chef (Kitchen)",
    Email = "kitchen@tablodemo.com",
    IsActive = true,
    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
};
var cashier = new Employee
{
    Id = Guid.Parse("33333333-0000-0000-0000-000000000004"),
    RestaurantId = restaurant.Id,
    RoleId = roles["Waiter"].Id,
    FirstName = "Pooja",
    LastName = "Nair (Cashier)",
    Email = "cashier@tablodemo.com",
    IsActive = true,
    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
};
var otherAdmin = new Employee
{
    Id = Guid.Parse("33333333-0000-0000-0000-000000000009"),
    RestaurantId = other.Id,
    RoleId = roles["Admin"].Id,
    FirstName = "Vikram",
    LastName = "Mehta (Coastal Admin)",
    Email = "coastal.admin@tablodemo.com",
    IsActive = true,
    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
};

// Categories
var starters = new Category { Id = Guid.Parse("88888888-0000-0000-0000-000000000001"), RestaurantId = restaurant.Id, Name = "Starters & Appetizers", DisplayOrder = 1 };
var mains = new Category { Id = Guid.Parse("88888888-0000-0000-0000-000000000002"), RestaurantId = restaurant.Id, Name = "Main Course Curries", DisplayOrder = 2 };
var breads = new Category { Id = Guid.Parse("88888888-0000-0000-0000-000000000003"), RestaurantId = restaurant.Id, Name = "Tandoor & Breads", DisplayOrder = 3 };
var drinks = new Category { Id = Guid.Parse("88888888-0000-0000-0000-000000000004"), RestaurantId = restaurant.Id, Name = "Artisan Beverages", DisplayOrder = 4 };
var desserts = new Category { Id = Guid.Parse("88888888-0000-0000-0000-000000000005"), RestaurantId = restaurant.Id, Name = "Desserts", DisplayOrder = 5 };

var otherCategory = new Category { Id = Guid.NewGuid(), RestaurantId = other.Id, Name = "Café plates", DisplayOrder = 1 };

// Virtual Thermal Printers & Stations
var foodPrinter = new ThermalPrinter
{
    Id = Guid.NewGuid(),
    RestaurantId = restaurant.Id,
    Name = "Food Kitchen Printer",
    ConnectionType = "NetworkTcp",
    IpAddress = "127.0.0.1",
    Port = 9100,
    Status = "Online",
    PaperWidthMm = 80,
    CharactersPerLine = 48,
    IsActive = true,
    CreatedAtUtc = DateTime.UtcNow
};
var bevPrinter = new ThermalPrinter
{
    Id = Guid.NewGuid(),
    RestaurantId = restaurant.Id,
    Name = "Beverage Station Printer",
    ConnectionType = "NetworkTcp",
    IpAddress = "127.0.0.1",
    Port = 9101,
    Status = "Online",
    PaperWidthMm = 80,
    CharactersPerLine = 48,
    IsActive = true,
    CreatedAtUtc = DateTime.UtcNow
};
var foodStation = new KitchenStation
{
    Id = Guid.NewGuid(),
    RestaurantId = restaurant.Id,
    Name = "Food Kitchen",
    Code = "FOOD",
    IsDefault = true,
    IsActive = true,
    PrinterId = foodPrinter.Id,
    DisplayOrder = 1
};
var bevStation = new KitchenStation
{
    Id = Guid.NewGuid(),
    RestaurantId = restaurant.Id,
    Name = "Beverage Station",
    Code = "BEV",
    IsDefault = false,
    IsActive = true,
    PrinterId = bevPrinter.Id,
    DisplayOrder = 2
};

drinks.KitchenStationId = bevStation.Id;
mains.KitchenStationId = foodStation.Id;
starters.KitchenStationId = foodStation.Id;
desserts.KitchenStationId = foodStation.Id;

// Menu Items
var biryani = new MenuItem
{
    Id = Guid.Parse("99999999-0000-0000-0000-000000000005"),
    RestaurantId = restaurant.Id,
    CategoryId = mains.Id,
    Name = "Thalassery Chicken Dum Biryani",
    Price = 320m,
    IsAvailable = true,
    Description = "Authentic Malabar kaima rice biryani with bone-in chicken.",
    KitchenStationId = foodStation.Id
};
var butterChicken = new MenuItem
{
    Id = Guid.Parse("99999999-0000-0000-0000-000000000003"),
    RestaurantId = restaurant.Id,
    CategoryId = mains.Id,
    Name = "Butter Chicken Handi",
    Price = 360m,
    IsAvailable = true,
    Description = "Tender tandoori chicken cooked in creamy tomato butter gravy.",
    KitchenStationId = foodStation.Id
};
var paneerTikka = new MenuItem
{
    Id = Guid.Parse("99999999-0000-0000-0000-000000000001"),
    RestaurantId = restaurant.Id,
    CategoryId = starters.Id,
    Name = "Paneer Tikka Angara",
    Price = 280m,
    IsAvailable = false, // Sold-out dish for availability testing
    Description = "Spiced cottage cheese cubes char-grilled in tandoor.",
    KitchenStationId = foodStation.Id
};
var naan = new MenuItem
{
    Id = Guid.Parse("99999999-0000-0000-0000-000000000006"),
    RestaurantId = restaurant.Id,
    CategoryId = breads.Id,
    Name = "Butter Garlic Naan",
    Price = 55m,
    IsAvailable = true,
    Description = "Clay oven leavened bread brushed with garlic butter.",
    KitchenStationId = foodStation.Id
};
var lime = new MenuItem
{
    Id = Guid.Parse("99999999-0000-0000-0000-000000000007"),
    RestaurantId = restaurant.Id,
    CategoryId = drinks.Id,
    Name = "Mint Lime Cooler",
    Price = 80m,
    IsAvailable = true,
    Description = "Refreshing fresh lime cooler with crushed garden mint.",
    KitchenStationId = bevStation.Id
};
var filterCoffee = new MenuItem
{
    Id = Guid.Parse("99999999-0000-0000-0000-000000000008"),
    RestaurantId = restaurant.Id,
    CategoryId = drinks.Id,
    Name = "South Indian Filter Coffee",
    Price = 60m,
    IsAvailable = true,
    Description = "Traditional chicory blend frothed with hot milk.",
    KitchenStationId = bevStation.Id
};
var gulabJamun = new MenuItem
{
    Id = Guid.Parse("99999999-0000-0000-0000-000000000009"),
    RestaurantId = restaurant.Id,
    CategoryId = desserts.Id,
    Name = "Gulab Jamun with Rabdi",
    Price = 140m,
    IsAvailable = true,
    Description = "Warm berry dumplings served with thickened cardamom milk.",
    KitchenStationId = foodStation.Id
};

var otherItem = new MenuItem
{
    Id = Guid.NewGuid(),
    RestaurantId = other.Id,
    CategoryId = otherCategory.Id,
    Name = "Banana Fritters",
    Price = 95m,
    IsAvailable = true
};

// Tables
var tables = Enumerable.Range(1, 10).Select(number => new RestaurantTable
{
    Id = Guid.Parse($"77777777-0000-0000-0000-{number:D12}"),
    RestaurantId = restaurant.Id,
    TableNumber = number,
    QRToken = $"table_{number}_demo_token",
    IsActive = true
}).ToArray();

var otherTable = new RestaurantTable
{
    Id = Guid.Parse("55555555-0000-0000-0000-000000000009"),
    RestaurantId = other.Id,
    TableNumber = 101,
    QRToken = "qr-coastal-table-1",
    IsActive = true
};

// Modifiers for Biryani
var spiceGroup = new ModifierGroup
{
    Id = Guid.NewGuid(),
    RestaurantId = restaurant.Id,
    Name = "Spice Level",
    MinSelections = 1,
    MaxSelections = 1,
    DisplayOrder = 1,
    IsActive = true
};
var spiceMild = new ModifierOption { Id = Guid.NewGuid(), ModifierGroupId = spiceGroup.Id, RestaurantId = restaurant.Id, Name = "Mild", PriceDelta = 0m, IsAvailable = true };
var spiceMed = new ModifierOption { Id = Guid.NewGuid(), ModifierGroupId = spiceGroup.Id, RestaurantId = restaurant.Id, Name = "Medium Spicy", PriceDelta = 0m, IsAvailable = true };
var spiceHot = new ModifierOption { Id = Guid.NewGuid(), ModifierGroupId = spiceGroup.Id, RestaurantId = restaurant.Id, Name = "Extra Spicy", PriceDelta = 10m, IsAvailable = true };
spiceGroup.Options.Add(spiceMild);
spiceGroup.Options.Add(spiceMed);
spiceGroup.Options.Add(spiceHot);

var portionGroup = new ModifierGroup
{
    Id = Guid.NewGuid(),
    RestaurantId = restaurant.Id,
    Name = "Portion Size",
    MinSelections = 1,
    MaxSelections = 1,
    DisplayOrder = 2,
    IsActive = true
};
var portReg = new ModifierOption { Id = Guid.NewGuid(), ModifierGroupId = portionGroup.Id, RestaurantId = restaurant.Id, Name = "Regular Portion", PriceDelta = 0m, IsAvailable = true };
var portLarge = new ModifierOption { Id = Guid.NewGuid(), ModifierGroupId = portionGroup.Id, RestaurantId = restaurant.Id, Name = "Jumbo Feast", PriceDelta = 120m, IsAvailable = true };
portionGroup.Options.Add(portReg);
portionGroup.Options.Add(portLarge);

var biryaniSpiceLink = new MenuItemModifierGroup { MenuItemId = biryani.Id, ModifierGroupId = spiceGroup.Id, DisplayOrder = 1 };
var biryaniPortionLink = new MenuItemModifierGroup { MenuItemId = biryani.Id, ModifierGroupId = portionGroup.Id, DisplayOrder = 2 };

db.AddRange(
    restaurant, other,
    starters, mains, breads, drinks, desserts, otherCategory,
    foodPrinter, bevPrinter, foodStation, bevStation,
    biryani, butterChicken, paneerTikka, naan, lime, filterCoffee, gulabJamun, otherItem,
    spiceGroup, portionGroup, biryaniSpiceLink, biryaniPortionLink,
    admin, waiter, kitchen, cashier, otherAdmin,
    otherTable,
    new RestaurantSettings { RestaurantId = restaurant.Id, IsAcceptingOrders = true },
    new RestaurantSettings { RestaurantId = other.Id, IsAcceptingOrders = true }
);
db.AddRange(tables);
await db.SaveChangesAsync();

DbSeeder.EnsurePlansAndSubscriptions(db);
var subscriptions = new SubscriptionService(db, NullLogger<SubscriptionService>.Instance);
await subscriptions.ChangeSubscriptionAsync(restaurant.Id, new()
{
    TargetPlanCode = "premium", NewStatus = "Active", Reason = "Local demo enterprise tier"
}, "superadmin@example.test");
await subscriptions.ChangeSubscriptionAsync(other.Id, new()
{
    TargetPlanCode = "basic", NewStatus = "Trial", Reason = "Local demo basic trial"
}, "superadmin@example.test");

var inventory = new InventoryService(db);
// 21 biryani ingredients formula
(string Name, string Unit, decimal Quantity, decimal Cost)[] formula =
[
    ("Bone-in chicken", "g", 200, .30m), ("Kaima rice", "g", 150, .12m),
    ("Onion", "g", 60, .10m), ("Tomato", "g", 30, .10m),
    ("Ginger", "g", 10, .20m), ("Garlic", "g", 10, .20m),
    ("Green chilli", "g", 5, .20m), ("Mint", "g", 5, .20m),
    ("Coriander leaves", "g", 5, .20m), ("Lemon juice", "ml", 10, .10m),
    ("Ghee", "g", 10, .50m), ("Cooking oil", "ml", 10, .20m),
    ("Garam masala", "g", 2, 1m), ("Cardamom", "g", 1, 1m),
    ("Cinnamon", "g", 2, .40m), ("Cloves", "g", 1, .50m),
    ("Cashews", "g", 4, .50m), ("Raisins", "g", 5, .20m),
    ("Yogurt", "g", 10, .10m), ("Salt", "g", 5, .095m),
    ("Cucumber raita", "g", 30, .10m)
];
Check(formula.Sum(line => line.Quantity * line.Cost) == 113.775m, "Biryani formula cost");
var recipe = new List<SetRecipeItemDto>();
foreach (var line in formula)
{
    var opening = line.Quantity * 20;
    var ingredient = await inventory.CreateIngredientAsync(restaurant.Id, admin.Id,
        new(line.Name, line.Unit, line.Quantity * 3, opening, line.Unit, Guid.NewGuid(), opening * line.Cost));
    recipe.Add(new(ingredient.Id, line.Quantity, 100));
}
await inventory.SetRecipeAsync(restaurant.Id, admin.Id, biryani.Id, new(recipe));

// Low stock ingredient: Fresh Mint Leaves
await inventory.CreateIngredientAsync(restaurant.Id, admin.Id,
    new("Fresh Mint Leaves", "g", 200m, 80m, "g", Guid.NewGuid(), 16m));

var services = new ServiceCollection();
services.AddLogging();
services.AddSignalR();
using var provider = services.BuildServiceProvider();
var orders = new OrderService(new OrderRepository(db), provider.GetRequiredService<IHubContext<OrderHub>>(),
    db, NullLogger<OrderService>.Instance);
var staff = new StaffOrderingService(db, orders);
var billing = new BillingService(db);
var payments = new BillPaymentService(db);
var groups = new DiningGroupService(db);
var floor = new FloorOperationsService(db);
var requests = new ServiceRequestService(db, provider.GetRequiredService<IHubContext<OrderHub>>());

Task<OrderResponseDto> StaffOrder(int index, Guid item, int quantity = 1, Guid? group = null) =>
    staff.SubmitAsync(restaurant.Id, waiter.Id, new()
    {
        TableId = tables[index].Id, OperationId = Guid.NewGuid(), DiningGroupId = group,
        Items = [new() { MenuItemId = item, Quantity = quantity }]
    });

async Task Advance(Guid orderId, params string[] statuses)
{
    foreach (var status in statuses)
        await orders.UpdateOrderStatusAsync(restaurant.Id, orderId, new() { Status = status });
}

async Task<BillDto> OpenBill(int index, Guid? group = null) =>
    (await billing.ReviewAsync(restaurant.Id, tables[index].Id, group)).OpenBill
        ?? throw new InvalidOperationException("Expected an open bill");

// 1. Table 1: Settled sale
var sale = await StaffOrder(0, biryani.Id, 2);
Check(sale.Status == "Accepted", "Staff order starts Accepted");
await Advance(sale.OrderId, "Preparing", "Ready", "Served");
var settled = await OpenBill(0);
Check(settled.TotalAmount == 672m, "Two-portion collection total");
await payments.RecordAsync(restaurant.Id, waiter.Id, settled.Id, new(Guid.NewGuid(), "Cash", 200m));
var partial = await OpenBill(0);
Check(partial.RemainingAmount == 472m, "Partial collection balance");
var chicken = recipe[0].IngredientId;
async Task<decimal> ChickenStock() => (await inventory.GetIngredientsAsync(restaurant.Id))
    .Single(ingredient => ingredient.Id == chicken).OnHandQuantity;
Check(await ChickenStock() == 4000m, "Partial payment preserves stock");
var finalPayment = new RecordBillPayment(Guid.NewGuid(), "UPI", 472m);
var acknowledgement = await payments.RecordAsync(restaurant.Id, waiter.Id, settled.Id, finalPayment);
Check(acknowledgement == await payments.RecordAsync(restaurant.Id, waiter.Id, settled.Id, finalPayment)
    && await ChickenStock() == 3600m, "Final payment consumes stock once, including on replay");
await billing.ReceiptAsync(restaurant.Id, settled.Id);

// 2. Table 2: Pending QR order + staff addition
var qrOrder = await orders.CreateOrderAsync(new()
{
    QRToken = tables[1].QRToken, Items = [new() { MenuItemId = lime.Id, Quantity = 1 }]
});
Check(qrOrder.Status == "Placed", "QR order starts Placed");
var added = await StaffOrder(1, lime.Id);
Check(added.Status == "Accepted" && added.OrderId != qrOrder.OrderId
    && (await orders.GetOrderByIdAsync(restaurant.Id, qrOrder.OrderId)).Status == "Placed",
    "Staff addition leaves the QR ticket pending");

// 3. Table 3: Preparing
var preparing = await StaffOrder(2, butterChicken.Id);
await Advance(preparing.OrderId, "Preparing");

// 4. Table 4: Ready
var ready = await StaffOrder(3, naan.Id, 4);
await Advance(ready.OrderId, "Preparing", "Ready");

// 5. Table 5: Partially paid mixed-method bill
var partlyPaid = await StaffOrder(4, lime.Id, 2);
await Advance(partlyPaid.OrderId, "Preparing", "Ready", "Served");
var partialExample = await OpenBill(4);
await payments.RecordAsync(restaurant.Id, waiter.Id, partialExample.Id, new(Guid.NewGuid(), "Card", 50m));

// 6 & 7. Table 6 & 7: Dining groups A and B, split bill, transferred to Table 7
var groupA = await groups.CreateAsync(restaurant.Id, waiter.Id, tables[5].Id, "A", Guid.NewGuid());
await groups.CreateAsync(restaurant.Id, waiter.Id, tables[5].Id, "B", Guid.NewGuid());
var splitOrder = await StaffOrder(5, lime.Id, 2, groupA.Id);
await Advance(splitOrder.OrderId, "Preparing", "Ready", "Served");
var parent = await OpenBill(5, groupA.Id);
var half = parent.TotalAmount / 2;
var children = await floor.SplitAsync(restaurant.Id, waiter.Id, parent.Id,
    new(Guid.NewGuid(), parent.Revision, [half, half], null));
Check(children.Count == 2 && children.Sum(child => child.TotalAmount) == parent.TotalAmount, "Split totals");
await floor.TransferAsync(restaurant.Id, waiter.Id, tables[5].Id, new(Guid.NewGuid(), tables[6].Id, groupA.Id));
await requests.CreateRequestAsync(tables[1].QRToken, new() { Type = "CallWaiter" });
await requests.CreateRequestAsync(tables[6].QRToken, new() { Type = "RequestBill", DiningGroupId = groupA.Id });

// 8. Table 8: Reservation preserving on-hand stock
await inventory.UpdateInventorySettingsAsync(restaurant.Id, new("WeightedAverage", true));
var reserved = await orders.CreateOrderAsync(new()
{
    QRToken = tables[7].QRToken, Items = [new() { MenuItemId = biryani.Id, Quantity = 1 }]
});
Check(reserved.Status == "Placed"
    && await db.StockReservations.AnyAsync(reservation => reservation.OrderId == reserved.OrderId)
    && await ChickenStock() == 3600m, "Pending QR reservation preserves on-hand stock");

// 9. Table 9: Held Course
var heldOrder = await staff.SubmitAsync(restaurant.Id, waiter.Id, new()
{
    TableId = tables[8].Id,
    OperationId = Guid.NewGuid(),
    Items = [
        new() { MenuItemId = butterChicken.Id, Quantity = 1, CourseName = "Mains", ReleaseState = "Fired" },
        new() { MenuItemId = gulabJamun.Id, Quantity = 2, CourseName = "Desserts", ReleaseState = "Held" }
    ]
});
await Advance(heldOrder.OrderId, "Preparing");

// 10. Parcel Counter, Scheduled Pickup, and Car-Side Orders
var india = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
var nowUtc = DateTime.UtcNow;

var parcelOrderReady = new Order
{
    Id = Guid.NewGuid(),
    RestaurantId = restaurant.Id,
    OrderNumber = 201,
    OrderChannel = "ParcelTakeaway",
    OrderSource = "Counter",
    CustomerName = "Anand Kumar",
    CustomerPhone = "+91 98765 11111",
    DailyTokenNumber = 201,
    BusinessDate = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, india).ToString("yyyy-MM-dd"),
    Status = "Ready",
    CreatedAt = nowUtc.AddMinutes(-20),
    UpdatedAt = nowUtc.AddMinutes(-5),
    OrderItems = [
        new OrderItem { Id = Guid.NewGuid(), MenuItemId = biryani.Id, ItemNameSnapshot = biryani.Name, Quantity = 2, UnitPrice = biryani.Price, ReleaseState = "Fired", PackingIntent = "Takeaway", PackagingFeeApplied = 20m }
    ]
};
parcelOrderReady.TotalAmount = 640m + 32m + 20m;

var scheduledUtc = nowUtc.AddHours(2);
var parcelOrderScheduled = new Order
{
    Id = Guid.NewGuid(),
    RestaurantId = restaurant.Id,
    OrderNumber = 202,
    OrderChannel = "ParcelTakeaway",
    OrderSource = "Phone",
    CustomerName = "Sneha Reddy",
    CustomerPhone = "+91 98765 22222",
    DailyTokenNumber = 202,
    IsScheduled = true,
    ScheduledPickupTimeUtc = scheduledUtc,
    ScheduledPickupTimeLocal = TimeZoneInfo.ConvertTimeFromUtc(scheduledUtc, india).ToString("yyyy-MM-dd HH:mm"),
    PreparationLeadMinutes = 20,
    BusinessDate = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, india).ToString("yyyy-MM-dd"),
    Status = "Placed",
    CreatedAt = nowUtc.AddMinutes(-10),
    UpdatedAt = nowUtc.AddMinutes(-10),
    OrderItems = [
        new OrderItem { Id = Guid.NewGuid(), MenuItemId = butterChicken.Id, ItemNameSnapshot = butterChicken.Name, Quantity = 1, UnitPrice = butterChicken.Price, ReleaseState = "Fired", PackingIntent = "Takeaway", PackagingFeeApplied = 10m },
        new OrderItem { Id = Guid.NewGuid(), MenuItemId = naan.Id, ItemNameSnapshot = naan.Name, Quantity = 3, UnitPrice = naan.Price, ReleaseState = "Fired", PackingIntent = "Takeaway", PackagingFeeApplied = 0m }
    ]
};
parcelOrderScheduled.TotalAmount = 360m + 165m + 26.25m + 10m;

var carSideOrder = new Order
{
    Id = Guid.NewGuid(),
    RestaurantId = restaurant.Id,
    OrderNumber = 203,
    OrderChannel = "CarParking",
    OrderSource = "WhatsApp",
    CustomerName = "Vikram Sen",
    CustomerPhone = "+91 98765 33333",
    VehiclePlate = "KA-01-MJ-5522",
    VehicleModel = "White Creta",
    ParkingBay = "Bay 3",
    DailyTokenNumber = 203,
    BusinessDate = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, india).ToString("yyyy-MM-dd"),
    Status = "Preparing",
    CreatedAt = nowUtc.AddMinutes(-12),
    UpdatedAt = nowUtc.AddMinutes(-2),
    OrderItems = [
        new OrderItem { Id = Guid.NewGuid(), MenuItemId = biryani.Id, ItemNameSnapshot = biryani.Name, Quantity = 1, UnitPrice = biryani.Price, ReleaseState = "Fired", PackingIntent = "Takeaway", PackagingFeeApplied = 10m },
        new OrderItem { Id = Guid.NewGuid(), MenuItemId = lime.Id, ItemNameSnapshot = lime.Name, Quantity = 2, UnitPrice = lime.Price, ReleaseState = "Fired", PackingIntent = "Takeaway", PackagingFeeApplied = 0m }
    ]
};
carSideOrder.TotalAmount = 320m + 160m + 24m + 10m;

db.Orders.AddRange(parcelOrderReady, parcelOrderScheduled, carSideOrder);
await db.SaveChangesAsync();

// Movement history for chicken
await inventory.RecordMovementAsync(restaurant.Id, admin.Id, chicken, new(Guid.NewGuid(), 1000m, "g", "Restock",
    "Restock at a different rate after settlement", 400m));

// Reports reconciliation
var reports = new InventoryReportService(db);
var saleDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(acknowledgement.CollectedAtUtc, india));
var filter = new SalesGrossProfitFilter { MenuItemId = biryani.Id, StartDate = saleDate, EndDate = saleDate };
var report = await reports.GetSalesGrossProfitReportAsync(restaurant.Id, filter);
var row = report.Rows.Single();
var detail = row.DrillDown.Single();
Check(detail.BillId == settled.Id && detail.Quantity == 2 && detail.IngredientBreakdown.Count == formula.Length
    && row.NetSalesExcludingTax == 640m && row.TotalCogs == 227.55m && row.TotalTax == 32m
    && row.GrossProfit == 412.45m && Math.Round(row.GrossMarginPercent, 2) == 64.45m, "Biryani report reconciliation");
var workbookBytes = await reports.ExportSalesGrossProfitReportAsync(restaurant.Id, filter);
using var workbook = new XLWorkbook(new MemoryStream(workbookBytes));
var sheet = workbook.Worksheet(1);
var exported = sheet.RowsUsed().Single(r => r.Cell(1).GetString() == biryani.Name);
Check(exported.Cell(3).GetValue<int>() == 2 && exported.Cell(6).GetValue<decimal>() == 640m
    && exported.Cell(8).GetValue<decimal>() == 227.55m && exported.Cell(9).GetValue<decimal>() == 412.45m,
    "Biryani workbook reconciliation");

// Advanced Inventory (Suppliers, POs, Invoices, Lots, Transfers, Preps, Stocktakes)
var purchasing = await AdvancedInventory.SeedAsync(inventory, restaurant.Id, admin.Id);
var stockWorkbook = await reports.ExportStockReportAsync(restaurant.Id, new());
var paymentReport = new PaymentReportService(db);
var lastDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, india));
var paymentFilter = new PaymentReportFilter { StartDate = saleDate, EndDate = lastDate };
var collections = await paymentReport.GetAsync(restaurant.Id, paymentFilter);
Check(collections.TotalCollected == 722m && collections.Cash == 200m && collections.UPI == 472m
    && collections.Card == 50m && collections.Items.Count(entry => entry.BillId == settled.Id) == 2,
    "IST payment report reconciliation");
var paymentWorkbook = await paymentReport.ExportAsync(restaurant.Id, paymentFilter);

await File.WriteAllBytesAsync(Path.Combine(artifactDir, "biryani-gross-profit.xlsx"), workbookBytes);
await File.WriteAllBytesAsync(Path.Combine(artifactDir, "stock.xlsx"), stockWorkbook);
await File.WriteAllBytesAsync(Path.Combine(artifactDir, "payments.xlsx"), paymentWorkbook);

var manifest = new
{
    database = database.Database,
    primaryRestaurant = new
    {
        id = restaurant.Id,
        name = restaurant.Name,
        admin = admin.Email,
        waiter = waiter.Email,
        kitchen = kitchen.Email,
        cashier = cashier.Email,
        plan = "Premium"
    },
    secondTenant = new
    {
        id = other.Id,
        name = other.Name,
        admin = otherAdmin.Email,
        plan = "Basic (Trial)"
    },
    purchasing,
    biryani = new
    {
        billId = settled.Id,
        orderId = sale.OrderId,
        menuItemId = biryani.Id,
        report.StartDate,
        report.EndDate,
        row.QuantitySold,
        row.NetSalesExcludingTax,
        row.TotalTax,
        row.TotalCogs,
        row.GrossProfit,
        row.GrossMarginPercent
    },
    tables = tables.Append(otherTable).Select(table => new
    {
        table.RestaurantId,
        table.TableNumber,
        table.Id,
        url = $"http://127.0.0.1:5173/t/{table.QRToken}"
    })
};
await File.WriteAllTextAsync(Path.Combine(artifactDir, "demo.json"),
    System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"[SUCCESS] Demo database '{database.Database}' seeded and artifacts written to {Path.GetFullPath(artifactDir)}");

static void Check(bool condition, string scenario)
{
    if (!condition)
        throw new InvalidOperationException($"Demo verification failed: {scenario}");
}
