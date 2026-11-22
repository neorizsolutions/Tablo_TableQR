using System.Globalization;
using System.Text.Json;
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

public class InventoryPostgresTests
{
    private record Scenario(Guid Restaurant, Guid Table, Guid Actor, Guid Item);

    private static ApplicationDbContext Context(string connection) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);

    private static async Task WithDatabase(Func<string, Scenario, Task> test)
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TABLO_BILLING_TEST_CONNECTION"));
        if (builder.Host != "127.0.0.1" || builder.Port != 55439 || builder.Username != "tablo_test" || builder.Database != "postgres")
            throw new InvalidOperationException("Billing tests require the isolated loopback test cluster on port 55439.");

        builder.Pooling = false;
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        var name = "tablo_inv_test_" + Guid.NewGuid().ToString("N");
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin))
            await create.ExecuteNonQueryAsync();

        builder.Database = name;
        try
        {
            Scenario scenario;
            await using (var db = Context(builder.ConnectionString))
            {
                await db.Database.MigrateAsync();
                var restaurant = new Restaurant { Id = Guid.NewGuid(), Name = "Isolated Inventory Test", ContactEmail = "inv@example.test" };
                var table = new RestaurantTable { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, TableNumber = 1, IsActive = true, QRToken = Guid.NewGuid().ToString("N") };
                var role = new Role { Id = Guid.NewGuid(), Name = "Admin" };
                var actor = new Employee { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Role = role, IsActive = true, Email = "admin@example.test" };
                var category = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Mains" };
                var item = new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = category.Id, Name = "Chicken Tikka", Price = 100m, IsAvailable = true };
                db.AddRange(restaurant, table, role, actor, category, item);
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

    private static OrderService Orders(ApplicationDbContext db) =>
        new(new OrderRepository(db), Mock.Of<IHubContext<OrderHub>>(), db, NullLogger<OrderService>.Instance);

    private static StaffOrderRequestDto Request(Scenario s, int qty = 2) => new()
    {
        OperationId = Guid.NewGuid(),
        TableId = s.Table,
        Items = [new OrderItemRequestDto { MenuItemId = s.Item, Quantity = qty }]
    };

    [Fact]
    public void UnitNormalizationAndYield_CalculatesRawDeductionsAccurately()
    {
        // 1 kg = 1000 g
        Assert.Equal(1000m, UnitConverter.NormalizeToBase(1m, "kg", "g"));
        Assert.Equal(250m, UnitConverter.NormalizeToBase(250m, "g", "g"));

        // 1.5 L = 1500 ml
        Assert.Equal(1500m, UnitConverter.NormalizeToBase(1.5m, "L", "ml"));
        Assert.Equal(500m, UnitConverter.NormalizeToBase(500m, "ml", "ml"));

        // 5 pieces = 5 pieces
        Assert.Equal(5m, UnitConverter.NormalizeToBase(5m, "pieces", "pieces"));

        // Yield calculation: 80g usable at 80% yield = 100g raw
        Assert.Equal(100m, UnitConverter.CalculateRawQuantity(80m, 80m));

        // 100g usable at 100% yield = 100g raw
        Assert.Equal(100m, UnitConverter.CalculateRawQuantity(100m, 100m));

        // Incompatible conversions reject
        Assert.Throws<ArgumentException>(() => UnitConverter.NormalizeToBase(1m, "kg", "ml"));
        Assert.Throws<ArgumentException>(() => UnitConverter.NormalizeToBase(1m, "pieces", "g"));
        Assert.Throws<ArgumentException>(() => UnitConverter.NormalizeToBase(1m, "liters", "pieces"));
    }

    [BillingPostgresFact]
    public Task TenantIsolation_CannotAccessOrDeductOtherRestaurantIngredients() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var inv = new InventoryService(db);

        // Restaurant A creates an ingredient
        var chicken = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto(
            "Chicken", "g", 200m, 1m, "kg", Guid.NewGuid()
        ));

        // Create Restaurant B
        var restB = new Restaurant { Id = Guid.NewGuid(), Name = "Other Restaurant", ContactEmail = "b@example.test" };
        var adminRole = await db.Roles.FirstAsync(r => r.Name == "Admin");
        var actorB = new Employee { Id = Guid.NewGuid(), RestaurantId = restB.Id, RoleId = adminRole.Id, IsActive = true, Email = "b@example.test" };
        db.AddRange(restB, actorB);
        await db.SaveChangesAsync();

        // Restaurant B cannot see Restaurant A's ingredient in list
        var bIngredients = await inv.GetIngredientsAsync(restB.Id);
        Assert.DoesNotContain(bIngredients, i => i.Id == chicken.Id);

        // Restaurant B cannot update Restaurant A's ingredient
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            inv.UpdateIngredientAsync(restB.Id, chicken.Id, new UpdateIngredientDto("Hacked", null, null)));

        // Restaurant B cannot record movements on Restaurant A's ingredient
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            inv.RecordMovementAsync(restB.Id, actorB.Id, chicken.Id, new RecordStockMovementDto(Guid.NewGuid(), 100m, "g", "Restock", null)));
    });

    [BillingPostgresFact]
    public Task SettlementDeduction_UnsplitBill_DeductsExactRawRecipeIngredients() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var inv = new InventoryService(db);

        // 1. Add 1 kg Chicken opening stock
        var chicken = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto(
            "Chicken", "g", 200m, 1m, "kg", Guid.NewGuid()
        ));
        Assert.Equal(1000m, chicken.OnHandQuantity);

        // 2. Link recipe: 80g usable @ 80% yield = 100g raw per serving
        await inv.SetRecipeAsync(s.Restaurant, s.Actor, s.Item, new SetRecipeDto([
            new SetRecipeItemDto(chicken.Id, 80m, 80m)
        ]));

        // 3. Place order for 2 servings (2 * 100g = 200g raw)
        var submitResult = await new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, Request(s, 2));

        // Advance to Served
        var order = await db.Orders.SingleAsync(o => o.Id == submitResult.OrderId);
        order.Status = "Preparing";
        order.Status = "Ready";
        order.Status = "Served";
        await db.SaveChangesAsync();

        // Review & Settle
        var billing = new BillingService(db);
        var review = await billing.ReviewAsync(s.Restaurant, s.Table);
        var opId = Guid.NewGuid();
        await billing.SettleAsync(s.Restaurant, review.OpenBill!.Id, s.Actor, new SettleBillRequestDto
        {
            OperationId = opId,
            ExpectedRevision = review.OpenBill.Revision,
            ExpectedTotal = review.OpenBill.TotalAmount,
            PaymentMethod = "Cash"
        });

        // 4. Verify deduction: 1000 - 200 = 800 g
        var ingredients = await inv.GetIngredientsAsync(s.Restaurant);
        var chk = ingredients.Single(i => i.Id == chicken.Id);
        Assert.Equal(800m, chk.OnHandQuantity);

        // Replay settlement with same opId -> stock remains 800g
        await billing.SettleAsync(s.Restaurant, review.OpenBill.Id, s.Actor, new SettleBillRequestDto
        {
            OperationId = opId,
            ExpectedRevision = review.OpenBill.Revision,
            ExpectedTotal = review.OpenBill.TotalAmount,
            PaymentMethod = "Cash"
        });

        var replayIngredients = await inv.GetIngredientsAsync(s.Restaurant);
        Assert.Equal(800m, replayIngredients.Single(i => i.Id == chicken.Id).OnHandQuantity);
    });

    [BillingPostgresFact]
    public Task PartialAndMixedPayment_LeavesStockIntact_UntilFinalSettlement() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var inv = new InventoryService(db);

        // 1. Add 1 kg chicken
        var chicken = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto(
            "Chicken", "g", 200m, 1m, "kg", Guid.NewGuid()
        ));

        // 2. Link recipe (100g raw per serving)
        await inv.SetRecipeAsync(s.Restaurant, s.Actor, s.Item, new SetRecipeDto([
            new SetRecipeItemDto(chicken.Id, 80m, 80m)
        ]));

        // 3. Place order for 2 servings (Total: 2 * 100 = ₹200 + tax)
        var submitResult = await new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, Request(s, 2));
        var order = await db.Orders.SingleAsync(o => o.Id == submitResult.OrderId);
        order.Status = "Served";
        await db.SaveChangesAsync();

        var billing = new BillingService(db);
        var review = await billing.ReviewAsync(s.Restaurant, s.Table);
        var billId = review.OpenBill!.Id;
        var totalAmount = review.OpenBill.TotalAmount;

        // 4. Record partial payment (Cash ₹50)
        var paymentService = new BillPaymentService(db);
        var ack1 = await paymentService.RecordAsync(s.Restaurant, s.Actor, billId,
            new RecordBillPayment(Guid.NewGuid(), "Cash", 50m, review.OpenBill.Revision, totalAmount));

        Assert.True(ack1.Remaining > 0);

        // Check stock: MUST STILL BE 1000 g!
        var ingAfterP1 = await inv.GetIngredientsAsync(s.Restaurant);
        Assert.Equal(1000m, ingAfterP1.Single(i => i.Id == chicken.Id).OnHandQuantity);

        // 5. Final payment (UPI remaining amount)
        var finalOpId = Guid.NewGuid();
        var ack2 = await paymentService.RecordAsync(s.Restaurant, s.Actor, billId,
            new RecordBillPayment(finalOpId, "UPI", ack1.Remaining));

        Assert.Equal(0m, ack2.Remaining);

        // Check stock: MUST NOW BE 800 g!
        var ingAfterFinal = await inv.GetIngredientsAsync(s.Restaurant);
        Assert.Equal(800m, ingAfterFinal.Single(i => i.Id == chicken.Id).OnHandQuantity);

        // Replay final payment -> remains 800 g
        var replayAck = await paymentService.RecordAsync(s.Restaurant, s.Actor, billId,
            new RecordBillPayment(finalOpId, "UPI", ack1.Remaining));
        Assert.Equal(0m, replayAck.Remaining);

        var ingAfterReplay = await inv.GetIngredientsAsync(s.Restaurant);
        Assert.Equal(800m, ingAfterReplay.Single(i => i.Id == chicken.Id).OnHandQuantity);
    });

    [BillingPostgresFact]
    public Task ItemSplit_DeductsIndependentShares() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var inv = new InventoryService(db);

        // Add 2 ingredients: Chicken (1000g) and Paneer (500g)
        var chicken = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto("Chicken", "g", 100m, 1000m, "g", Guid.NewGuid()));
        var paneer = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto("Paneer", "g", 100m, 500m, "g", Guid.NewGuid()));

        // Add second menu item (Paneer Dish)
        var cat = await db.Categories.FirstAsync(c => c.RestaurantId == s.Restaurant);
        var paneerItem = new MenuItem { Id = Guid.NewGuid(), RestaurantId = s.Restaurant, CategoryId = cat.Id, Name = "Paneer Tikka", Price = 80m, IsAvailable = true };
        db.MenuItems.Add(paneerItem);
        await db.SaveChangesAsync();

        // Configure recipes
        await inv.SetRecipeAsync(s.Restaurant, s.Actor, s.Item, new SetRecipeDto([new SetRecipeItemDto(chicken.Id, 100m, 100m)]));
        await inv.SetRecipeAsync(s.Restaurant, s.Actor, paneerItem.Id, new SetRecipeDto([new SetRecipeItemDto(paneer.Id, 50m, 100m)]));

        // Submit order with 1 Chicken Dish + 1 Paneer Dish
        var submitResult = await new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, new StaffOrderRequestDto
        {
            OperationId = Guid.NewGuid(),
            TableId = s.Table,
            Items = [
                new OrderItemRequestDto { MenuItemId = s.Item, Quantity = 1 },
                new OrderItemRequestDto { MenuItemId = paneerItem.Id, Quantity = 1 }
            ]
        });

        var order = await db.Orders.Include(o => o.OrderItems).SingleAsync(o => o.Id == submitResult.OrderId);
        order.Status = "Served";
        await db.SaveChangesAsync();

        var billing = new BillingService(db);
        var review = await billing.ReviewAsync(s.Restaurant, s.Table);
        var originalBill = review.OpenBill!;

        // Split by item: Share 1 has Chicken dish, Share 2 has Paneer dish
        var chickenOrderItem = order.OrderItems.First(i => i.MenuItemId == s.Item);
        var paneerOrderItem = order.OrderItems.First(i => i.MenuItemId == paneerItem.Id);

        var floorService = new FloorOperationsService(db);
        var splitResult = await floorService.SplitAsync(s.Restaurant, s.Actor, originalBill.Id, new SplitBillRequest(
            Guid.NewGuid(),
            originalBill.Revision,
            null,
            [
                new Dictionary<Guid, int> { [chickenOrderItem.Id] = 1 },
                new Dictionary<Guid, int> { [paneerOrderItem.Id] = 1 }
            ]
        ));

        var childBill1 = splitResult[0];
        var childBill2 = splitResult[1];

        // Settle Child Bill 1 (Chicken dish)
        await billing.SettleAsync(s.Restaurant, childBill1.Id, s.Actor, new SettleBillRequestDto
        {
            OperationId = Guid.NewGuid(),
            ExpectedRevision = childBill1.Revision,
            ExpectedTotal = childBill1.TotalAmount,
            PaymentMethod = "Cash"
        });

        // Verify: Chicken deducted by 100g (now 900g), Paneer NOT deducted (still 500g)
        var ingsAfterShare1 = await inv.GetIngredientsAsync(s.Restaurant);
        Assert.Equal(900m, ingsAfterShare1.Single(i => i.Id == chicken.Id).OnHandQuantity);
        Assert.Equal(500m, ingsAfterShare1.Single(i => i.Id == paneer.Id).OnHandQuantity);

        // Settle Child Bill 2 (Paneer dish)
        await billing.SettleAsync(s.Restaurant, childBill2.Id, s.Actor, new SettleBillRequestDto
        {
            OperationId = Guid.NewGuid(),
            ExpectedRevision = childBill2.Revision,
            ExpectedTotal = childBill2.TotalAmount,
            PaymentMethod = "UPI"
        });

        // Verify: Paneer now deducted by 50g (450g), Chicken remains 900g
        var ingsAfterShare2 = await inv.GetIngredientsAsync(s.Restaurant);
        Assert.Equal(900m, ingsAfterShare2.Single(i => i.Id == chicken.Id).OnHandQuantity);
        Assert.Equal(450m, ingsAfterShare2.Single(i => i.Id == paneer.Id).OnHandQuantity);
    });

    [BillingPostgresFact]
    public Task AmountSplit_DeductsOnceWhenFinalShareCloses() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var inv = new InventoryService(db);

        var chicken = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto("Chicken", "g", 100m, 1000m, "g", Guid.NewGuid()));
        await inv.SetRecipeAsync(s.Restaurant, s.Actor, s.Item, new SetRecipeDto([new SetRecipeItemDto(chicken.Id, 100m, 100m)]));

        // Submit order with 2 servings (Total: 2 * 100g = 200g chicken)
        var submitResult = await new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, Request(s, 2));
        var order = await db.Orders.SingleAsync(o => o.Id == submitResult.OrderId);
        order.Status = "Served";
        await db.SaveChangesAsync();

        var billing = new BillingService(db);
        var review = await billing.ReviewAsync(s.Restaurant, s.Table);
        var originalBill = review.OpenBill!;

        // Split by amount into 2 equal shares
        var shareAmount = originalBill.TotalAmount / 2m;
        var floorService = new FloorOperationsService(db);
        var splitResult = await floorService.SplitAsync(s.Restaurant, s.Actor, originalBill.Id, new SplitBillRequest(
            Guid.NewGuid(),
            originalBill.Revision,
            [shareAmount, originalBill.TotalAmount - shareAmount],
            null
        ));

        var share1 = splitResult[0];
        var share2 = splitResult[1];

        // Settle Share 1: Stock must NOT deduct yet (amount shares do not identify dishes)
        await billing.SettleAsync(s.Restaurant, share1.Id, s.Actor, new SettleBillRequestDto
        {
            OperationId = Guid.NewGuid(),
            ExpectedRevision = share1.Revision,
            ExpectedTotal = share1.TotalAmount,
            PaymentMethod = "Cash"
        });

        var ingsAfterShare1 = await inv.GetIngredientsAsync(s.Restaurant);
        Assert.Equal(1000m, ingsAfterShare1.Single(i => i.Id == chicken.Id).OnHandQuantity);

        // Settle Share 2 (Final Share): Now the parent items are consumed once!
        await billing.SettleAsync(s.Restaurant, share2.Id, s.Actor, new SettleBillRequestDto
        {
            OperationId = Guid.NewGuid(),
            ExpectedRevision = share2.Revision,
            ExpectedTotal = share2.TotalAmount,
            PaymentMethod = "Card"
        });

        var ingsAfterShare2 = await inv.GetIngredientsAsync(s.Restaurant);
        Assert.Equal(800m, ingsAfterShare2.Single(i => i.Id == chicken.Id).OnHandQuantity);
    });

    [BillingPostgresFact]
    public Task NegativeBalance_DoesNotBlockPaymentOrSettlement() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var inv = new InventoryService(db);

        // Chicken with only 50g opening stock
        var chicken = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto("Chicken", "g", 100m, 50m, "g", Guid.NewGuid()));
        await inv.SetRecipeAsync(s.Restaurant, s.Actor, s.Item, new SetRecipeDto([new SetRecipeItemDto(chicken.Id, 100m, 100m)]));

        // Order 2 servings (requires 200g)
        var submitResult = await new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, Request(s, 2));
        var order = await db.Orders.SingleAsync(o => o.Id == submitResult.OrderId);
        order.Status = "Served";
        await db.SaveChangesAsync();

        var billing = new BillingService(db);
        var review = await billing.ReviewAsync(s.Restaurant, s.Table);

        // Settlement must SUCCEED even with insufficient stock
        await billing.SettleAsync(s.Restaurant, review.OpenBill!.Id, s.Actor, new SettleBillRequestDto
        {
            OperationId = Guid.NewGuid(),
            ExpectedRevision = review.OpenBill.Revision,
            ExpectedTotal = review.OpenBill.TotalAmount,
            PaymentMethod = "Cash"
        });

        // Stock balance becomes 50 - 200 = -150g, flagged as low stock
        var ingredients = await inv.GetIngredientsAsync(s.Restaurant);
        var chk = ingredients.Single(i => i.Id == chicken.Id);
        Assert.Equal(-150m, chk.OnHandQuantity);
        Assert.True(chk.IsLowStock);
    });

    [BillingPostgresFact]
    public Task Stocktake_PostsAdjustmentAndReconcilesInterveningMovements() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var inv = new InventoryService(db);

        // 1. Initial stock = 1000g
        var chicken = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto("Chicken", "g", 100m, 1000m, "g", Guid.NewGuid()));

        // 2. Start Stocktake: snapshots recorded stock = 1000g
        var stocktake = await inv.StartStocktakeAsync(s.Restaurant, s.Actor, Guid.NewGuid());
        var item = stocktake.Items.Single(i => i.IngredientId == chicken.Id);
        Assert.Equal(1000m, item.RecordedStockSnapshot);

        // 3. Intervening movement occurs: Restock +500g (stock is now 1500g)
        await inv.RecordMovementAsync(s.Restaurant, s.Actor, chicken.Id, new RecordStockMovementDto(
            Guid.NewGuid(), 500m, "g", "Restock", "Midday delivery"
        ));

        var currentOnHand = (await inv.GetIngredientsAsync(s.Restaurant)).Single(i => i.Id == chicken.Id).OnHandQuantity;
        Assert.Equal(1500m, currentOnHand);

        // 4. Attempt to post stocktake (physical count was 950g at count time) WITHOUT reconciliation confirmation
        // Must throw ServiceConflictException detecting intervening movements!
        var postOp = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<ServiceConflictException>(() =>
            inv.PostStocktakeAsync(s.Restaurant, s.Actor, stocktake.Id, new PostStocktakeRequestDto(
                postOp,
                false, // ConfirmReconciliation = false
                [new StocktakeCountEntryDto(chicken.Id, 950m)],
                null
            )));

        Assert.Contains("Intervening stock movements detected", ex.Message);

        // 5. Post stocktake WITH reconciliation confirmation
        var completed = await inv.PostStocktakeAsync(s.Restaurant, s.Actor, stocktake.Id, new PostStocktakeRequestDto(
            postOp,
            true, // ConfirmReconciliation = true
            [new StocktakeCountEntryDto(chicken.Id, 950m)],
            "Reconciled after delivery"
        ));

        Assert.Equal("Completed", completed.Status);

        // Expected adjustment: variance = 950 - 1000 = -50g
        // Resulting stock: 1500 + (-50) = 1450g! Intervening restock (+500g) is preserved!
        var finalIngredients = await inv.GetIngredientsAsync(s.Restaurant);
        var finalChicken = finalIngredients.Single(i => i.Id == chicken.Id);
        Assert.Equal(1450m, finalChicken.OnHandQuantity);

        // 6. Idempotent retry returns original completed stocktake
        var retry = await inv.PostStocktakeAsync(s.Restaurant, s.Actor, stocktake.Id, new PostStocktakeRequestDto(
            postOp,
            true,
            [new StocktakeCountEntryDto(chicken.Id, 950m)],
            "Reconciled after delivery"
        ));
        Assert.Equal("Completed", retry.Status);
    });
}
