using System.Globalization;
using ClosedXML.Excel;
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

public class InventoryCostingAndReportsTests
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
        var name = "tablo_cost_test_" + Guid.NewGuid().ToString("N");
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin))
            await create.ExecuteNonQueryAsync();

        builder.Database = name;
        try
        {
            Scenario scenario;
            await using (var db = Context(builder.ConnectionString))
            {
                await db.Database.MigrateAsync();
                var restaurant = new Restaurant { Id = Guid.NewGuid(), Name = "Costing & Reports Test", ContactEmail = "cost@example.test" };
                var table = new RestaurantTable { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, TableNumber = 1, IsActive = true, QRToken = Guid.NewGuid().ToString("N") };
                var role = new Role { Id = Guid.NewGuid(), Name = "Admin" };
                var actor = new Employee { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Role = role, IsActive = true, Email = "admin@example.test" };
                var category = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Mains" };
                var item = new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = category.Id, Name = "Chicken Dum Biryani", Price = 250m, IsAvailable = true };
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

    [BillingPostgresFact]
    public Task MovingWeightedAverageCost_UpdatesOnPurchaseReceipts() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var inv = new InventoryService(db);

        // 1. Create ingredient with opening stock: 1,000 g @ INR 300 total (INR 0.30/g)
        var chicken = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto(
            Name: "Chicken",
            BaseUnit: "g",
            LowStockThreshold: 200m,
            InitialStock: 1m,
            InitialStockUnit: "kg",
            OperationId: Guid.NewGuid(),
            InitialCost: 300m
        ));

        Assert.Equal(1000m, chicken.OnHandQuantity);
        Assert.Equal(0.30m, chicken.CurrentAverageCost);

        // 2. Record purchase receipt at different price:
        // Buy 1,000 g @ INR 400 total (INR 0.40/g)
        // Expected new average: ((1000 * 0.30) + (1000 * 0.40)) / (1000 + 1000) = (300 + 400) / 2000 = 0.35 INR/g
        await inv.RecordMovementAsync(s.Restaurant, s.Actor, chicken.Id, new RecordStockMovementDto(
            OperationId: Guid.NewGuid(),
            Quantity: 1m,
            Unit: "kg",
            Reason: "Restock",
            Notes: "Restock at INR 400/kg",
            TotalCost: 400m
        ));

        var updated = (await inv.GetIngredientsAsync(s.Restaurant)).Single(i => i.Id == chicken.Id);
        Assert.Equal(2000m, updated.OnHandQuantity);
        Assert.Equal(0.35m, updated.CurrentAverageCost);
    });

    [BillingPostgresFact]
    public Task Settlement_PreservesHistoricalCost_AfterSubsequentPriceChanges() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var inv = new InventoryService(db);
        var reports = new InventoryReportService(db);

        // 1. Opening stock: 1,000 g Chicken @ INR 0.30/g (Total INR 300)
        var chicken = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto(
            Name: "Chicken",
            BaseUnit: "g",
            InitialStock: 1000m,
            InitialStockUnit: "g",
            InitialCost: 300m
        ));

        // 2. Link recipe: 200g raw per portion
        await inv.SetRecipeAsync(s.Restaurant, s.Actor, s.Item, new SetRecipeDto([
            new SetRecipeItemDto(chicken.Id, 200m, 100m)
        ]));

        // 3. Order 1: Settle 2 portions (400g raw @ INR 0.30 = INR 120 COGS)
        var submit1 = await new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, Request(s, 2));
        var order1 = await db.Orders.SingleAsync(o => o.Id == submit1.OrderId);
        order1.Status = "Served";
        await db.SaveChangesAsync();

        var billing = new BillingService(db);
        var review1 = await billing.ReviewAsync(s.Restaurant, s.Table);
        await billing.SettleAsync(s.Restaurant, review1.OpenBill!.Id, s.Actor, new SettleBillRequestDto
        {
            OperationId = Guid.NewGuid(),
            ExpectedRevision = review1.OpenBill.Revision,
            ExpectedTotal = review1.OpenBill.TotalAmount,
            PaymentMethod = "Cash"
        });

        // Verify remaining stock is 600g @ INR 0.30/g
        var stockAfterSale1 = (await inv.GetIngredientsAsync(s.Restaurant)).Single(i => i.Id == chicken.Id);
        Assert.Equal(600m, stockAfterSale1.OnHandQuantity);
        Assert.Equal(0.30m, stockAfterSale1.CurrentAverageCost);

        // 4. Restock: Buy 1,400 g @ INR 0.40/g (Total INR 560)
        // New average: ((600 * 0.30) + (1400 * 0.40)) / (600 + 1400) = (180 + 560) / 2000 = 740 / 2000 = 0.37 INR/g
        await inv.RecordMovementAsync(s.Restaurant, s.Actor, chicken.Id, new RecordStockMovementDto(
            OperationId: Guid.NewGuid(),
            Quantity: 1400m,
            Unit: "g",
            Reason: "Restock",
            TotalCost: 560m
        ));

        var stockAfterRestock = (await inv.GetIngredientsAsync(s.Restaurant)).Single(i => i.Id == chicken.Id);
        Assert.Equal(2000m, stockAfterRestock.OnHandQuantity);
        Assert.Equal(0.37m, stockAfterRestock.CurrentAverageCost);

        // 5. Order 2: Settle 2 portions (400g raw @ INR 0.37 = INR 148 COGS)
        var submit2 = await new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, Request(s, 2));
        var order2 = await db.Orders.SingleAsync(o => o.Id == submit2.OrderId);
        order2.Status = "Served";
        await db.SaveChangesAsync();

        var review2 = await billing.ReviewAsync(s.Restaurant, s.Table);
        await billing.SettleAsync(s.Restaurant, review2.OpenBill!.Id, s.Actor, new SettleBillRequestDto
        {
            OperationId = Guid.NewGuid(),
            ExpectedRevision = review2.OpenBill.Revision,
            ExpectedTotal = review2.OpenBill.TotalAmount,
            PaymentMethod = "UPI"
        });

        // 6. Check Sales & Gross Profit Report:
        // Order 1's COGS MUST be exactly INR 120 (preserved at historical 0.30/g)
        // Order 2's COGS MUST be exactly INR 148 (at new 0.37/g)
        // Total COGS = 120 + 148 = INR 268!
        var gpReport = await reports.GetSalesGrossProfitReportAsync(s.Restaurant, new SalesGrossProfitFilter());
        Assert.Equal(4, gpReport.TotalPortionsSold);
        Assert.Equal(268m, gpReport.TotalCogs);

        var biryaniRow = gpReport.Rows.Single(r => r.MenuItemId == s.Item);
        Assert.Equal(4, biryaniRow.QuantitySold);
        Assert.Equal(268m, biryaniRow.TotalCogs);

        var drillDown1 = biryaniRow.DrillDown.Single(d => d.BillId == review1.OpenBill.Id);
        Assert.Equal(120m, drillDown1.TotalIngredientCost);

        var drillDown2 = biryaniRow.DrillDown.Single(d => d.BillId == review2.OpenBill.Id);
        Assert.Equal(148m, drillDown2.TotalIngredientCost);
    });

    [BillingPostgresFact]
    public Task SimpleReportFixture_NetSales500_Cogs180_Yields320GrossProfitAnd64PercentMargin() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var inv = new InventoryService(db);
        var reports = new InventoryReportService(db);

        // Net sales excluding tax = INR 500, recorded COGS = INR 180.
        // Gross Profit = 500 - 180 = 320.
        // Gross Margin = (320 / 500) * 100 = 64%.
        // 2 portions @ INR 250 price = INR 500 total.
        // Recipe: 100g of Ingredient @ INR 0.90/g -> 2 portions = 200g @ 0.90 = INR 180 COGS.
        var spice = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto(
            Name: "Gourmet Spices",
            BaseUnit: "g",
            InitialStock: 500m,
            InitialCost: 450m // 450 / 500 = 0.90 INR/g
        ));

        await inv.SetRecipeAsync(s.Restaurant, s.Actor, s.Item, new SetRecipeDto([
            new SetRecipeItemDto(spice.Id, 100m, 100m)
        ]));

        var submit = await new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, Request(s, 2));
        var order = await db.Orders.SingleAsync(o => o.Id == submit.OrderId);
        order.Status = "Served";
        await db.SaveChangesAsync();

        var billing = new BillingService(db);
        var review = await billing.ReviewAsync(s.Restaurant, s.Table);
        await billing.SettleAsync(s.Restaurant, review.OpenBill!.Id, s.Actor, new SettleBillRequestDto
        {
            OperationId = Guid.NewGuid(),
            ExpectedRevision = review.OpenBill.Revision,
            ExpectedTotal = review.OpenBill.TotalAmount,
            PaymentMethod = "Cash"
        });

        var report = await reports.GetSalesGrossProfitReportAsync(s.Restaurant, new SalesGrossProfitFilter());
        Assert.Equal(500m, report.TotalNetSalesExcludingTax);
        Assert.Equal(180m, report.TotalCogs);
        Assert.Equal(320m, report.TotalGrossProfit);
        Assert.Equal(64.00m, report.OverallGrossMarginPercent);
    });

    [BillingPostgresFact]
    public Task SecondFixture_WithDiscountAndTax_CalculatesReconciledGrossProfit() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var inv = new InventoryService(db);
        var reports = new InventoryReportService(db);

        // Dish price: INR 250 * 2 = INR 500 gross sales.
        // Add a 10% discount on bill = INR 50 discount -> Net sales excl tax = INR 450.
        // Tax at 5% = INR 22.50.
        // Ingredient COGS = INR 180.
        // Gross Profit = 450 - 180 = 270.
        // Gross Margin = (270 / 450) * 100 = 60%.
        var spice = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto(
            Name: "Spices",
            BaseUnit: "g",
            InitialStock: 500m,
            InitialCost: 450m // 0.90 INR/g
        ));

        await inv.SetRecipeAsync(s.Restaurant, s.Actor, s.Item, new SetRecipeDto([
            new SetRecipeItemDto(spice.Id, 100m, 100m)
        ]));

        var submit = await new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, Request(s, 2));
        var order = await db.Orders.SingleAsync(o => o.Id == submit.OrderId);
        order.Status = "Served";
        await db.SaveChangesAsync();

        var billing = new BillingService(db);
        var review = await billing.ReviewAsync(s.Restaurant, s.Table);

        // Apply discount and tax to the order
        order.Discount = 50m;
        order.Tax = 22.50m;
        order.TotalAmount = 450m + 22.50m;
        await db.SaveChangesAsync();

        var updatedReview = await billing.ReviewAsync(s.Restaurant, s.Table);

        await billing.SettleAsync(s.Restaurant, updatedReview.OpenBill!.Id, s.Actor, new SettleBillRequestDto
        {
            OperationId = Guid.NewGuid(),
            ExpectedRevision = updatedReview.OpenBill.Revision,
            ExpectedTotal = updatedReview.OpenBill.TotalAmount,
            PaymentMethod = "UPI"
        });

        var report = await reports.GetSalesGrossProfitReportAsync(s.Restaurant, new SalesGrossProfitFilter());
        Assert.Equal(500m, report.TotalGrossSales);
        Assert.Equal(50m, report.TotalDiscounts);
        Assert.Equal(450m, report.TotalNetSalesExcludingTax);
        Assert.Equal(22.50m, report.TotalTax);
        Assert.Equal(180m, report.TotalCogs);
        Assert.Equal(270m, report.TotalGrossProfit);
        Assert.Equal(60.00m, report.OverallGrossMarginPercent);
    });

    [BillingPostgresFact]
    public Task StockReport_ReconcilesToMovementLedgerAndValuation() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var inv = new InventoryService(db);
        var reports = new InventoryReportService(db);

        // 1. Create ingredient with opening stock: 1,000 g @ INR 0.50/g (INR 500 value)
        var chicken = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto(
            Name: "Fresh Chicken",
            BaseUnit: "g",
            LowStockThreshold: 300m,
            InitialStock: 1000m,
            InitialCost: 500m
        ));

        // 2. Restock: +500 g @ INR 0.50/g
        await inv.RecordMovementAsync(s.Restaurant, s.Actor, chicken.Id, new RecordStockMovementDto(
            OperationId: Guid.NewGuid(),
            Quantity: 500m,
            Unit: "g",
            Reason: "Restock",
            TotalCost: 250m
        ));

        // 3. Waste: -100 g
        await inv.RecordMovementAsync(s.Restaurant, s.Actor, chicken.Id, new RecordStockMovementDto(
            OperationId: Guid.NewGuid(),
            Quantity: 100m,
            Unit: "g",
            Reason: "Waste"
        ));

        // 4. Manual adjustment: -50 g
        await inv.RecordMovementAsync(s.Restaurant, s.Actor, chicken.Id, new RecordStockMovementDto(
            OperationId: Guid.NewGuid(),
            Quantity: -50m,
            Unit: "g",
            Reason: "ManualCorrection"
        ));

        // Verify Stock Report:
        // Receipts = 1000 (opening) + 500 (restock) = 1500 g
        // Waste = -100 g
        // Adjustments = -50 g
        // Closing stock = 1350 g
        // Valuation = 1350 * 0.50 = INR 675.00
        var stockReport = await reports.GetStockReportAsync(s.Restaurant, new StockReportFilter());
        var row = stockReport.Rows.Single(r => r.IngredientId == chicken.Id);

        Assert.Equal(1500m, row.ReceiptsQuantity);
        Assert.Equal(-100m, row.WasteQuantity);
        Assert.Equal(-50m, row.AdjustmentsQuantity);
        Assert.Equal(1350m, row.ClosingQuantity);
        Assert.Equal(0.50m, row.CostPerBaseUnit);
        Assert.Equal(675m, row.ClosingStockValue);

        // Reconciles: Closing = Opening (0) + Receipts (1500) + Sales (0) + Waste (-100) + Adjustments (-50)
        Assert.Equal(row.ClosingQuantity, row.OpeningQuantity + row.ReceiptsQuantity + row.SalesQuantity + row.WasteQuantity + row.AdjustmentsQuantity);

        // Excel Export test
        var excelBytes = await reports.ExportStockReportAsync(s.Restaurant, new StockReportFilter());
        Assert.NotEmpty(excelBytes);

        using var memoryStream = new MemoryStream(excelBytes);
        using var workbook = new XLWorkbook(memoryStream);
        var worksheet = workbook.Worksheet("Stock Report");
        Assert.NotNull(worksheet);
    });

    [BillingPostgresFact]
    public Task IncompleteCosting_SurfacesDishesWithoutRecipeOrCost() => WithDatabase(async (connection, s) =>
    {
        await using var db = Context(connection);
        var inv = new InventoryService(db);
        var reports = new InventoryReportService(db);

        // Place and settle order on dish WITHOUT configuring recipe
        var submit = await new StaffOrderingService(db, Orders(db)).SubmitAsync(s.Restaurant, s.Actor, Request(s, 2));
        var order = await db.Orders.SingleAsync(o => o.Id == submit.OrderId);
        order.Status = "Served";
        await db.SaveChangesAsync();

        var billing = new BillingService(db);
        var review = await billing.ReviewAsync(s.Restaurant, s.Table);
        await billing.SettleAsync(s.Restaurant, review.OpenBill!.Id, s.Actor, new SettleBillRequestDto
        {
            OperationId = Guid.NewGuid(),
            ExpectedRevision = review.OpenBill.Revision,
            ExpectedTotal = review.OpenBill.TotalAmount,
            PaymentMethod = "Cash"
        });

        // Report should explicitly surface incomplete costing
        var report = await reports.GetSalesGrossProfitReportAsync(s.Restaurant, new SalesGrossProfitFilter());
        Assert.Equal(1, report.IncompleteCostingCount);
        Assert.Equal(500m, report.IncompleteSalesAmount);
        Assert.Contains(report.IncompleteCostingItems, i => i.MenuItemId == s.Item);

        var row = report.Rows.Single(r => r.MenuItemId == s.Item);
        Assert.False(row.IsCostComplete);
        Assert.Contains("No recipe configured", row.IncompleteReason);
    });
}
