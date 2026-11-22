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

public class AdvancedPurchasingAndInventoryPostgresTests
{
    private record Scenario(Guid Restaurant, Guid Table, Guid Actor, Guid Item);

    private static ApplicationDbContext Context(string connection) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);

    private static async Task WithDatabase(Func<string, Scenario, Task> test)
    {
        var connStr = Environment.GetEnvironmentVariable("TABLO_BILLING_TEST_CONNECTION");
        if (string.IsNullOrEmpty(connStr))
            connStr = "Host=127.0.0.1;Port=55439;Database=postgres;Username=tablo_test;Pooling=false";

        var builder = new NpgsqlConnectionStringBuilder(connStr);
        if (builder.Host != "127.0.0.1" || builder.Port != 55439 || builder.Username != "tablo_test" || builder.Database != "postgres")
            throw new InvalidOperationException("Tests require the isolated loopback test cluster on port 55439.");

        builder.Pooling = false;
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        var name = "tablo_adv_inv_" + Guid.NewGuid().ToString("N");
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin))
            await create.ExecuteNonQueryAsync();

        builder.Database = name;
        try
        {
            Scenario scenario;
            await using (var db = Context(builder.ConnectionString))
            {
                await db.Database.MigrateAsync();
                var restaurant = new Restaurant { Id = Guid.NewGuid(), Name = "Malabar Dum Biryani House", ContactEmail = "admin@biryani.test" };
                var table = new RestaurantTable { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, TableNumber = 5, IsActive = true, QRToken = Guid.NewGuid().ToString("N") };
                var role = new Role { Id = Guid.NewGuid(), Name = "Admin" };
                var actor = new Employee { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Role = role, IsActive = true, Email = "admin@biryani.test" };
                var category = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Biryani" };
                var item = new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, CategoryId = category.Id, Name = "Malabar Chicken Dum Biryani", Price = 320m, IsAvailable = true };
                var settings = new RestaurantSettings { RestaurantId = restaurant.Id, IsAcceptingOrders = true, CostingMethod = "WeightedAverage", AutoManageAvailability = false };

                db.AddRange(restaurant, table, role, actor, category, item, settings);
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

    [Fact]
    public async Task Area1_SupplierManagementAndPriceComparison_NormalizesCorrectly()
    {
        await WithDatabase(async (conn, s) =>
        {
            await using var db = Context(conn);
            var inv = new InventoryService(db);

            // Create ingredient: Jeerakasala Rice (base unit: g)
            var rice = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto(
                Name: "Jeerakasala Khaima Rice",
                BaseUnit: "g",
                LowStockThreshold: 1000m
            ));

            // Create 2 suppliers
            var sup1 = await inv.CreateSupplierAsync(s.Restaurant, new CreateSupplierDto(
                Name: "Malabar Rice Mills",
                ContactPerson: "Ashraf",
                Phone: "9876543210"
            ));

            var sup2 = await inv.CreateSupplierAsync(s.Restaurant, new CreateSupplierDto(
                Name: "Calicut Agro Traders",
                ContactPerson: "Mustafa",
                Phone: "9876543211"
            ));

            // Supplier 1 offers 25 kg sack for ₹2,250 (Conversion factor = 25,000 g -> ₹0.09/g = ₹90/kg)
            await inv.SetSupplierIngredientAsync(s.Restaurant, new SetSupplierIngredientDto(
                SupplierId: sup1.Id,
                IngredientId: rice.Id,
                PackUnit: "25 kg sack",
                PackQuantity: 25m,
                PackUnitType: "kg",
                ConversionFactor: 25000m,
                LastPurchasePricePerPack: 2250m
            ));

            // Supplier 2 offers 10 kg bag for ₹950 (Conversion factor = 10,000 g -> ₹0.095/g = ₹95/kg)
            await inv.SetSupplierIngredientAsync(s.Restaurant, new SetSupplierIngredientDto(
                SupplierId: sup2.Id,
                IngredientId: rice.Id,
                PackUnit: "10 kg bag",
                PackQuantity: 10m,
                PackUnitType: "kg",
                ConversionFactor: 10000m,
                LastPurchasePricePerPack: 950m
            ));

            // Compare prices
            var comparison = await inv.CompareIngredientPricesAsync(s.Restaurant, rice.Id);
            Assert.Equal(2, comparison.Suppliers.Count);

            // Supplier 1 should be first (cheapest normalized rate: 0.090000 INR/g)
            Assert.Equal(sup1.Id, comparison.Suppliers[0].SupplierId);
            Assert.Equal(0.09m, comparison.Suppliers[0].NormalizedPricePerBaseUnit);

            // Supplier 2 should be second (0.095000 INR/g)
            Assert.Equal(sup2.Id, comparison.Suppliers[1].SupplierId);
            Assert.Equal(0.095m, comparison.Suppliers[1].NormalizedPricePerBaseUnit);
        });
    }

    [Fact]
    public async Task Area2_PurchaseOrdersAndGoodsReceipt_IsIdempotentAndUpdatesStock()
    {
        await WithDatabase(async (conn, s) =>
        {
            await using var db = Context(conn);
            var inv = new InventoryService(db);

            var rice = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto(
                Name: "Khaima Rice",
                BaseUnit: "g",
                InitialStock: 5000m,
                InitialCost: 450m // 5,000 g @ 0.09/g
            ));

            var supplier = await inv.CreateSupplierAsync(s.Restaurant, new CreateSupplierDto(Name: "Malabar Rice Mills"));

            // 1. Create Purchase Order for 2 sacks (50 kg)
            var po = await inv.CreatePurchaseOrderAsync(s.Restaurant, s.Actor, new CreatePurchaseOrderDto(
                SupplierId: supplier.Id,
                ExpectedDeliveryDateUtc: DateTime.UtcNow.AddDays(2),
                Notes: "Weekly biryani rice order",
                Items: [
                    new CreatePurchaseOrderItemDto(
                        IngredientId: rice.Id,
                        PackUnit: "25 kg sack",
                        OrderedPackQuantity: 2m,
                        UnitPricePerPack: 2250m,
                        ConversionFactor: 25000m
                    )
                ]
            ));

            Assert.Equal("Draft", po.Status);
            Assert.Equal(4500m, po.TotalAmount);

            // Approve PO
            var approved = await inv.UpdatePurchaseOrderStatusAsync(s.Restaurant, s.Actor, po.Id, "Approved");
            Assert.Equal("Approved", approved.Status);

            // Stock should STILL be untouched at 5,000 g
            var currentIng = (await inv.GetIngredientsAsync(s.Restaurant)).Single(i => i.Id == rice.Id);
            Assert.Equal(5000m, currentIng.OnHandQuantity);

            // 2. Receive Goods against Invoice "INV-MRM-2026-001"
            var opId = Guid.NewGuid();
            var receiveDto = new ReceiveGoodsDto(
                OperationId: opId,
                SupplierId: supplier.Id,
                InvoiceNumber: "INV-MRM-2026-001",
                InvoiceDate: DateTime.UtcNow,
                Subtotal: 4500m,
                DiscountAmount: 0m,
                TaxAmount: 0m,
                ShippingCharges: 100m, // Landed shipping cost
                GrandTotal: 4600m,
                Notes: "Delivered intact",
                PurchaseOrderId: po.Id,
                Items: [
                    new ReceiveGoodsItemDto(
                        IngredientId: rice.Id,
                        PurchaseOrderItemId: po.Items[0].Id,
                        PackUnit: "25 kg sack",
                        ReceivedPackQuantity: 2m,
                        ConversionFactor: 25000m,
                        PackPrice: 2250m,
                        LineDiscount: 0m,
                        LineTax: 0m,
                        TotalLineCost: 4600m // Landed cost = 4600 / 50000 = 0.092/g
                    )
                ]
            );

            var invoice = await inv.ReceiveGoodsAsync(s.Restaurant, s.Actor, receiveDto);
            Assert.NotNull(invoice);
            Assert.Single(invoice.Items);
            Assert.Equal(50000m, invoice.Items[0].BaseQuantity);
            Assert.Equal(0.092m, invoice.Items[0].UnitCostPerBaseUnit);

            // Verify Stock on hand increased: 5,000 + 50,000 = 55,000 g
            var updatedIng = (await inv.GetIngredientsAsync(s.Restaurant)).Single(i => i.Id == rice.Id);
            Assert.Equal(55000m, updatedIng.OnHandQuantity);

            // Verify moving average cost updated: (450 + 4600) / 55000 = 0.091818 INR/g
            Assert.Equal(Math.Round(5050m / 55000m, 6), updatedIng.CurrentAverageCost);

            // Verify PO status transitioned to "Received"
            var refreshedPo = await inv.GetPurchaseOrderByIdAsync(s.Restaurant, po.Id);
            Assert.Equal("Received", refreshedPo.Status);

            // 3. IDEMPOTENCY CHECK: Replay exact same receive operation
            var replayInvoice = await inv.ReceiveGoodsAsync(s.Restaurant, s.Actor, receiveDto);
            Assert.Equal(invoice.Id, replayInvoice.Id);

            // Stock must NOT double-increase
            var afterReplayIng = (await inv.GetIngredientsAsync(s.Restaurant)).Single(i => i.Id == rice.Id);
            Assert.Equal(55000m, afterReplayIng.OnHandQuantity);
        });
    }

    [Fact]
    public async Task Area3_PurchaseReturns_ValidatesAndReconcilesLotAndLedger()
    {
        await WithDatabase(async (conn, s) =>
        {
            await using var db = Context(conn);
            var inv = new InventoryService(db);

            var chicken = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto(
                Name: "Fresh Chicken",
                BaseUnit: "g"
            ));

            var supplier = await inv.CreateSupplierAsync(s.Restaurant, new CreateSupplierDto(Name: "Malabar Poultry"));

            // Receive 10 kg chicken (10,000 g) @ ₹2,400 (₹0.24/g)
            var invoice = await inv.ReceiveGoodsAsync(s.Restaurant, s.Actor, new ReceiveGoodsDto(
                OperationId: Guid.NewGuid(),
                SupplierId: supplier.Id,
                InvoiceNumber: "INV-CHICKEN-01",
                InvoiceDate: DateTime.UtcNow,
                Subtotal: 2400m,
                DiscountAmount: 0m,
                TaxAmount: 0m,
                ShippingCharges: 0m,
                GrandTotal: 2400m,
                Notes: null,
                PurchaseOrderId: null,
                Items: [
                    new ReceiveGoodsItemDto(
                        IngredientId: chicken.Id,
                        PurchaseOrderItemId: null,
                        PackUnit: "10 kg crate",
                        ReceivedPackQuantity: 1m,
                        ConversionFactor: 10000m,
                        PackPrice: 2400m,
                        LineDiscount: 0m,
                        LineTax: 0m,
                        TotalLineCost: 2400m
                    )
                ]
            ));

            var lotId = invoice.Items[0].StockLotId!.Value;
            var lot = (await inv.GetLotsAsync(s.Restaurant)).Single(l => l.Id == lotId);
            Assert.Equal(10000m, lot.AvailableQuantity);

            // Attempt to return 15,000 g (exceeds invoice qty) -> must throw ArgumentException
            await Assert.ThrowsAsync<ArgumentException>(() => inv.CreatePurchaseReturnAsync(s.Restaurant, s.Actor, new CreatePurchaseReturnDto(
                OperationId: Guid.NewGuid(),
                PurchaseInvoiceId: invoice.Id,
                CreditNoteReference: "CN-TEST",
                Reason: "Quality Issue",
                Notes: null,
                Items: [new CreatePurchaseReturnItemDto(invoice.Items[0].Id, 15000m)]
            )));

            // Return 2,000 g with credit note "CN-001"
            var returnDto = await inv.CreatePurchaseReturnAsync(s.Restaurant, s.Actor, new CreatePurchaseReturnDto(
                OperationId: Guid.NewGuid(),
                PurchaseInvoiceId: invoice.Id,
                CreditNoteReference: "CN-001",
                Reason: "Damaged packaging",
                Notes: null,
                Items: [new CreatePurchaseReturnItemDto(invoice.Items[0].Id, 2000m)]
            ));

            Assert.Equal(480m, returnDto.TotalCreditAmount); // 2000 * 0.24 = 480

            // Verify Lot available quantity is now 8,000 g
            var updatedLot = (await inv.GetLotsAsync(s.Restaurant)).Single(l => l.Id == lotId);
            Assert.Equal(8000m, updatedLot.AvailableQuantity);

            // Verify Stock on hand is now 8,000 g
            var ing = (await inv.GetIngredientsAsync(s.Restaurant)).Single(i => i.Id == chicken.Id);
            Assert.Equal(8000m, ing.OnHandQuantity);

            // Reversing movement posted
            var movements = await inv.GetMovementsAsync(s.Restaurant, chicken.Id);
            Assert.Contains(movements, m => m.Reason == "PurchaseReturn" && m.QuantityChange == -2000m);
        });
    }

    [Fact]
    public async Task Area4_PreparedIngredients_RejectsCircular_AndDeductsOnceOnDishSale()
    {
        await WithDatabase(async (conn, s) =>
        {
            await using var db = Context(conn);
            var inv = new InventoryService(db);

            // Raw spices
            var cardamom = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto("Cardamom", "g", InitialStock: 500m, InitialCost: 1500m)); // 3 INR/g
            var clove = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto("Clove", "g", InitialStock: 500m, InitialCost: 500m)); // 1 INR/g

            // Prepared item
            var biryaniMasala = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto("Biryani Masala", "g"));

            // 1. Attempt circular definition: Biryani Masala consuming itself
            await Assert.ThrowsAsync<ArgumentException>(() => inv.SavePreparationRecipeAsync(s.Restaurant, new SavePreparationRecipeDto(
                OutputIngredientId: biryaniMasala.Id,
                ExpectedYieldQuantity: 500m,
                Notes: "Circular test",
                Inputs: [new SavePreparationRecipeInputDto(biryaniMasala.Id, 50m, 100m)]
            )));

            // 2. Save valid preparation recipe: 500 g output requires 100g Cardamom and 100g Clove
            var prepRecipe = await inv.SavePreparationRecipeAsync(s.Restaurant, new SavePreparationRecipeDto(
                OutputIngredientId: biryaniMasala.Id,
                ExpectedYieldQuantity: 500m,
                Notes: "Signature Biryani Spice Blend",
                Inputs: [
                    new SavePreparationRecipeInputDto(cardamom.Id, 100m, 100m),
                    new SavePreparationRecipeInputDto(clove.Id, 100m, 100m)
                ]
            ));

            Assert.NotNull(prepRecipe);
            Assert.Equal(2, prepRecipe.Inputs.Count);

            // 3. Execute Batch: 1x multiplier, produce 480 g (20 g waste in grinding)
            // Raw inputs consumed: 100g Cardamom (@ ₹3 = ₹300) + 100g Clove (@ ₹1 = ₹100) = ₹400 total input cost
            // Output unit cost = 400 / 480 = 0.833333 INR/g
            var batch = await inv.RecordPreparationBatchAsync(s.Restaurant, s.Actor, new RecordPreparationBatchDto(
                OperationId: Guid.NewGuid(),
                PreparationRecipeId: prepRecipe.Id,
                Multiplier: 1m,
                ActualYieldQuantity: 480m,
                WasteQuantity: 20m,
                WasteReason: "Grinding loss",
                ExpiryDate: DateTime.UtcNow.AddMonths(1)
            ));

            Assert.Equal(480m, batch.ActualYieldQuantity);
            Assert.Equal(400m, batch.TotalInputCost);
            Assert.Equal(Math.Round(400m / 480m, 6), batch.CostPerBaseUnit);

            // Verify raw ingredients decreased
            var cardOnHand = (await inv.GetIngredientsAsync(s.Restaurant)).Single(i => i.Id == cardamom.Id).OnHandQuantity;
            var cloveOnHand = (await inv.GetIngredientsAsync(s.Restaurant)).Single(i => i.Id == clove.Id).OnHandQuantity;
            Assert.Equal(400m, cardOnHand); // 500 - 100
            Assert.Equal(400m, cloveOnHand); // 500 - 100

            // Verify prepared ingredient increased
            var masalaOnHand = (await inv.GetIngredientsAsync(s.Restaurant)).Single(i => i.Id == biryaniMasala.Id).OnHandQuantity;
            Assert.Equal(480m, masalaOnHand);

            // 4. Configure Dish Recipe to use 20 g of Biryani Masala
            await inv.SetRecipeAsync(s.Restaurant, s.Actor, s.Item, new SetRecipeDto(
                Items: [new SetRecipeItemDto(biryaniMasala.Id, 20m, 100m)]
            ));

            // Place order for 2 portions
            var orderService = Orders(db);
            var order = await orderService.CreateOrderAsync(new CreateOrderRequestDto
            {
                QRToken = (await db.RestaurantTables.SingleAsync(t => t.Id == s.Table)).QRToken,
                Items = [new OrderItemRequestDto { MenuItemId = s.Item, Quantity = 2 }]
            }, isStaff: true);

            // Settle bill deduction
            var bill = await db.TableBills.Include(b => b.Orders).ThenInclude(o => o.OrderItems)
                .SingleAsync(b => b.TableId == s.Table && b.SettledAtUtc == null);
            await inv.DeductForSettlementAsync(db, s.Restaurant, bill.Id, s.Actor, Guid.NewGuid());

            // Invariant check: Selling 2 portions consumes 40 g of Biryani Masala (480 - 40 = 440 g)
            var masalaAfterSale = (await inv.GetIngredientsAsync(s.Restaurant)).Single(i => i.Id == biryaniMasala.Id).OnHandQuantity;
            Assert.Equal(440m, masalaAfterSale);

            // Raw Cardamom and Clove MUST NOT have been deducted again!
            var cardAfterSale = (await inv.GetIngredientsAsync(s.Restaurant)).Single(i => i.Id == cardamom.Id).OnHandQuantity;
            var cloveAfterSale = (await inv.GetIngredientsAsync(s.Restaurant)).Single(i => i.Id == clove.Id).OnHandQuantity;
            Assert.Equal(400m, cardAfterSale);
            Assert.Equal(400m, cloveAfterSale);
        });
    }

    [Fact]
    public async Task Area5_LotsAndExpiry_QuarantineAndWriteOff()
    {
        await WithDatabase(async (conn, s) =>
        {
            await using var db = Context(conn);
            var inv = new InventoryService(db);

            var milk = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto("Fresh Milk", "ml"));
            var supplier = await inv.CreateSupplierAsync(s.Restaurant, new CreateSupplierDto(Name: "Dairy Farm"));

            // Receive milk with past expiry date
            var pastDate = DateTime.UtcNow.AddDays(-2);
            var invoice = await inv.ReceiveGoodsAsync(s.Restaurant, s.Actor, new ReceiveGoodsDto(
                OperationId: Guid.NewGuid(),
                SupplierId: supplier.Id,
                InvoiceNumber: "INV-MILK-001",
                InvoiceDate: DateTime.UtcNow.AddDays(-3),
                Subtotal: 500m,
                DiscountAmount: 0m,
                TaxAmount: 0m,
                ShippingCharges: 0m,
                GrandTotal: 500m,
                Notes: null,
                PurchaseOrderId: null,
                Items: [
                    new ReceiveGoodsItemDto(
                        IngredientId: milk.Id,
                        PurchaseOrderItemId: null,
                        PackUnit: "10 L can",
                        ReceivedPackQuantity: 1m,
                        ConversionFactor: 10000m,
                        PackPrice: 500m,
                        LineDiscount: 0m,
                        LineTax: 0m,
                        TotalLineCost: 500m,
                        ExpiryDate: pastDate
                    )
                ]
            ));

            var lotId = invoice.Items[0].StockLotId!.Value;

            // Check lot surfaces as expired
            var expiredLots = await inv.GetLotsAsync(s.Restaurant);
            var expiredLot = expiredLots.Single(l => l.Id == lotId);
            Assert.True(expiredLot.IsExpired);

            // Quarantine lot
            var quarantined = await inv.QuarantineLotAsync(s.Restaurant, s.Actor, lotId, new QuarantineLotDto(Guid.NewGuid(), "Sour smell"));
            Assert.Equal("Quarantined", quarantined.Status);

            // Write off entire 10,000 ml
            var writtenOff = await inv.WriteOffLotAsync(s.Restaurant, s.Actor, lotId, new WriteOffLotDto(Guid.NewGuid(), 10000m, "Expired and curdled"));
            Assert.Equal("WrittenOff", writtenOff.Status);
            Assert.Equal(0m, writtenOff.AvailableQuantity);

            // Verify stock movement for write off
            var movements = await inv.GetMovementsAsync(s.Restaurant, milk.Id);
            Assert.Contains(movements, m => m.Reason == "WriteOff" && m.QuantityChange == -10000m);
        });
    }

    [Fact]
    public async Task Area6_CostingMethods_FIFO_DepletesOldestLayersFirst()
    {
        await WithDatabase(async (conn, s) =>
        {
            await using var db = Context(conn);
            var inv = new InventoryService(db);

            // Set costing method to FIFO
            await inv.UpdateInventorySettingsAsync(s.Restaurant, new UpdateInventorySettingsDto("FIFO", false));

            var ingredient = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto("Biryani Ghee", "g"));
            var supplier = await inv.CreateSupplierAsync(s.Restaurant, new CreateSupplierDto(Name: "Pure Ghee Co"));

            // Lot 1: 100 g @ ₹1.00/g = ₹100
            await inv.ReceiveGoodsAsync(s.Restaurant, s.Actor, new ReceiveGoodsDto(
                OperationId: Guid.NewGuid(),
                SupplierId: supplier.Id,
                InvoiceNumber: "INV-GHEE-001",
                InvoiceDate: DateTime.UtcNow.AddDays(-2),
                Subtotal: 100m,
                DiscountAmount: 0m,
                TaxAmount: 0m,
                ShippingCharges: 0m,
                GrandTotal: 100m,
                Notes: null,
                PurchaseOrderId: null,
                Items: [new ReceiveGoodsItemDto(ingredient.Id, null, "100 g pack", 1m, 100m, 100m, 0m, 0m, 100m, LotCode: "LOT-FIFO-01")]
            ));

            // Lot 2: 100 g @ ₹2.00/g = ₹200
            await inv.ReceiveGoodsAsync(s.Restaurant, s.Actor, new ReceiveGoodsDto(
                OperationId: Guid.NewGuid(),
                SupplierId: supplier.Id,
                InvoiceNumber: "INV-GHEE-002",
                InvoiceDate: DateTime.UtcNow.AddDays(-1),
                Subtotal: 200m,
                DiscountAmount: 0m,
                TaxAmount: 0m,
                ShippingCharges: 0m,
                GrandTotal: 200m,
                Notes: null,
                PurchaseOrderId: null,
                Items: [new ReceiveGoodsItemDto(ingredient.Id, null, "100 g pack", 1m, 100m, 200m, 0m, 0m, 200m, LotCode: "LOT-FIFO-02")]
            ));

            // Set dish recipe requiring 150 g Ghee per serving
            await inv.SetRecipeAsync(s.Restaurant, s.Actor, s.Item, new SetRecipeDto(
                Items: [new SetRecipeItemDto(ingredient.Id, 150m, 100m)]
            ));

            // Place order for 1 serving (needs 150 g)
            var orderService = Orders(db);
            await orderService.CreateOrderAsync(new CreateOrderRequestDto
            {
                QRToken = (await db.RestaurantTables.SingleAsync(t => t.Id == s.Table)).QRToken,
                Items = [new OrderItemRequestDto { MenuItemId = s.Item, Quantity = 1 }]
            }, isStaff: true);

            // Settle bill deduction
            var bill = await db.TableBills.Include(b => b.Orders).ThenInclude(o => o.OrderItems)
                .SingleAsync(b => b.TableId == s.Table && b.SettledAtUtc == null);
            await inv.DeductForSettlementAsync(db, s.Restaurant, bill.Id, s.Actor, Guid.NewGuid());

            // In FIFO:
            // Depletes all 100 g of LOT-FIFO-01 (@ ₹1.00 = ₹100)
            // Depletes 50 g of LOT-FIFO-02 (@ ₹2.00 = ₹100)
            // Total COGS = ₹200.00
            var settledCost = await db.SettledBillItemCosts.Include(c => c.IngredientCosts)
                .SingleAsync(c => c.TableBillId == bill.Id);

            Assert.Equal(200m, settledCost.TotalIngredientCost);

            // Verify Lot 1 is exhausted, Lot 2 has 50 g remaining
            var lot1 = await db.StockLots.SingleAsync(l => l.LotCode == "LOT-FIFO-01");
            var lot2 = await db.StockLots.SingleAsync(l => l.LotCode == "LOT-FIFO-02");
            Assert.Equal(0m, lot1.AvailableQuantity);
            Assert.Equal("Exhausted", lot1.Status);
            Assert.Equal(50m, lot2.AvailableQuantity);
            Assert.Equal("Active", lot2.Status);
        });
    }

    [Fact]
    public async Task Area7_CentralKitchenAndTransfers_DualMovementLedger()
    {
        await WithDatabase(async (conn, s) =>
        {
            await using var db = Context(conn);
            var inv = new InventoryService(db);

            var centralKitchen = await inv.CreateOutletAsync(s.Restaurant, new CreateOutletDto("Central Kitchen", "CK", true));
            var outlet1 = await inv.CreateOutletAsync(s.Restaurant, new CreateOutletDto("Retail Branch 1", "RB1", false));

            var mutton = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto(
                Name: "Tender Mutton",
                BaseUnit: "g",
                InitialStock: 20000m,
                InitialCost: 16000m // 0.8 INR/g
            ));

            // Create transfer request: RB1 requests 10,000 g from Central Kitchen
            var transfer = await inv.CreateTransferAsync(s.Restaurant, s.Actor, new CreateStockTransferDto(
                SourceOutletId: centralKitchen.Id,
                DestinationOutletId: outlet1.Id,
                Notes: "Daily meat supply",
                Items: [new CreateStockTransferItemDto(mutton.Id, 10000m)]
            ));

            Assert.Equal("Requested", transfer.Status);

            // Central kitchen dispatches 10,000 g
            var dispatched = await inv.DispatchTransferAsync(s.Restaurant, s.Actor, transfer.Id, new DispatchStockTransferDto(
                OperationId: Guid.NewGuid(),
                Items: [new DispatchTransferItemDto(transfer.Items[0].Id, 10000m)]
            ));
            Assert.Equal("Dispatched", dispatched.Status);

            // Verify source outlet movement posted (-10,000 g)
            var movementsAfterDispatch = await inv.GetMovementsAsync(s.Restaurant, mutton.Id);
            Assert.Contains(movementsAfterDispatch, m => m.Reason == "TransferOut" && m.QuantityChange == -10000m);

            // Destination receives 9,500 g (500 g missing / damaged in transit)
            var received = await inv.ReceiveTransferAsync(s.Restaurant, s.Actor, transfer.Id, new ReceiveStockTransferDto(
                OperationId: Guid.NewGuid(),
                Items: [new ReceiveTransferItemDto(transfer.Items[0].Id, 9500m, "500 g pack torn")]
            ));

            Assert.Equal("Received", received.Status);
            Assert.Equal(9500m, received.Items[0].ReceivedQuantity);
            Assert.Equal(500m, received.Items[0].DiscrepancyQuantity);

            // Verify destination outlet movement (+9,500 g) and restaurant-wide net loss of exactly 500 g (no double deduction)
            var allMovements = await inv.GetMovementsAsync(s.Restaurant, mutton.Id);
            Assert.Contains(allMovements, m => m.Reason == "TransferIn" && m.QuantityChange == 9500m);
            Assert.Contains(allMovements, m => m.Reason == "TransferOut" && m.QuantityChange == -10000m);
            Assert.DoesNotContain(allMovements, m => m.Reason == "TransferDiscrepancyLoss");
            Assert.Equal(-500m, allMovements.Sum(m => m.QuantityChange));

            // Test repeated receipt idempotency
            var repeatReceived = await inv.ReceiveTransferAsync(s.Restaurant, s.Actor, transfer.Id, new ReceiveStockTransferDto(
                OperationId: Guid.NewGuid(),
                Items: [new ReceiveTransferItemDto(transfer.Items[0].Id, 9500m, "500 g pack torn")]
            ));
            Assert.Equal("Received", repeatReceived.Status);
            var movementsAfterRepeat = await inv.GetMovementsAsync(s.Restaurant, mutton.Id);
            Assert.Equal(-500m, movementsAfterRepeat.Sum(m => m.QuantityChange));
        });
    }

    [Fact]
    public async Task Area8_MenuAvailabilityAndReservations_PreventsOverselling()
    {
        await WithDatabase(async (conn, s) =>
        {
            await using var db = Context(conn);
            var inv = new InventoryService(db);

            // Enable AutoManageAvailability
            await inv.UpdateInventorySettingsAsync(s.Restaurant, new UpdateInventorySettingsDto("WeightedAverage", true));

            // Stock exactly 200 g of Chicken (Recipe requires 100 g per serving -> exactly 2 portions available)
            var chicken = await inv.CreateIngredientAsync(s.Restaurant, s.Actor, new CreateIngredientDto(
                Name: "Biryani Chicken Pieces",
                BaseUnit: "g",
                InitialStock: 200m,
                InitialCost: 60m
            ));

            await inv.SetRecipeAsync(s.Restaurant, s.Actor, s.Item, new SetRecipeDto(
                Items: [new SetRecipeItemDto(chicken.Id, 100m, 100m)]
            ));

            // Check menu availability initially
            var availabilities = await inv.GetMenuAvailabilityAsync(s.Restaurant);
            var itemAvail = availabilities.Single(a => a.MenuItemId == s.Item);
            Assert.Equal(2, itemAvail.MaxPortionsAvailable);
            Assert.True(itemAvail.IsAvailable);

            var orderService = Orders(db);
            var qrToken = (await db.RestaurantTables.SingleAsync(t => t.Id == s.Table)).QRToken;

            // Order 2 portions (takes all available stock)
            var order1 = await orderService.CreateOrderAsync(new CreateOrderRequestDto
            {
                QRToken = qrToken,
                Items = [new OrderItemRequestDto { MenuItemId = s.Item, Quantity = 2 }]
            }, isStaff: true);

            // Active reservation should exist for 200 g
            var reservations = await db.StockReservations.Where(r => r.OrderId == order1.OrderId).ToListAsync();
            Assert.Single(reservations);
            Assert.Equal("Active", reservations[0].Status);
            Assert.Equal(200m, reservations[0].ReservedQuantity);

            // Availability now shows 0 portions
            var afterOrderAvail = (await inv.GetMenuAvailabilityAsync(s.Restaurant)).Single(a => a.MenuItemId == s.Item);
            Assert.Equal(0, afterOrderAvail.MaxPortionsAvailable);

            // Attempting to place an order for 1 more portion MUST fail due to insufficient stock
            await Assert.ThrowsAsync<ArgumentException>(() => orderService.CreateOrderAsync(new CreateOrderRequestDto
            {
                QRToken = qrToken,
                Items = [new OrderItemRequestDto { MenuItemId = s.Item, Quantity = 1 }]
            }, isStaff: false));

            // Cancel the order -> releases reservation
            await orderService.UpdateOrderStatusAsync(s.Restaurant, order1.OrderId, new UpdateOrderStatusRequestDto
            {
                Status = "Cancelled",
                ExceptionReason = "Customer changed mind"
            });

            var releasedRes = await db.StockReservations.SingleAsync(r => r.OrderId == order1.OrderId);
            Assert.Equal("Released", releasedRes.Status);

            // Availability restored to 2 portions
            var restoredAvail = (await inv.GetMenuAvailabilityAsync(s.Restaurant)).Single(a => a.MenuItemId == s.Item);
            Assert.Equal(2, restoredAvail.MaxPortionsAvailable);

            // Re-order 2 portions and settle bill -> reservation committed
            var order2 = await orderService.CreateOrderAsync(new CreateOrderRequestDto
            {
                QRToken = qrToken,
                Items = [new OrderItemRequestDto { MenuItemId = s.Item, Quantity = 2 }]
            }, isStaff: true);

            var bill = await db.TableBills.Include(b => b.Orders).ThenInclude(o => o.OrderItems)
                .SingleAsync(b => b.TableId == s.Table && b.SettledAtUtc == null);
            await inv.DeductForSettlementAsync(db, s.Restaurant, bill.Id, s.Actor, Guid.NewGuid());

            var committedRes = await db.StockReservations.SingleAsync(r => r.OrderId == order2.OrderId);
            Assert.Equal("Committed", committedRes.Status);

            // On-hand stock is now 0 g
            var finalOnHand = (await inv.GetIngredientsAsync(s.Restaurant)).Single(i => i.Id == chicken.Id).OnHandQuantity;
            Assert.Equal(0m, finalOnHand);
        });
    }
}
