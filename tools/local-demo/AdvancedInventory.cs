using Tablo.Api.DTOs;
using Tablo.Api.Services;

internal static class AdvancedInventory
{
    internal static async Task<object> SeedAsync(InventoryService inventory, Guid tenant, Guid actor)
    {
        var supplier = await inventory.CreateSupplierAsync(tenant,
            new("Kadal Produce Cooperative", "Meera", "orders@kadal.example.test"));
        var secondSupplier = await inventory.CreateSupplierAsync(tenant, new("West Coast Growers"));
        var limes = await inventory.CreateIngredientAsync(tenant, actor, new("Fresh limes", "g"));
        var cordial = await inventory.CreateIngredientAsync(tenant, actor, new("Lime cordial", "ml"));
        await inventory.SetSupplierIngredientAsync(tenant, new(supplier.Id, limes.Id, "1 kg crate", 1, "kg", 1000, 80));
        await inventory.SetSupplierIngredientAsync(tenant, new(secondSupplier.Id, limes.Id, "1 kg crate", 1, "kg", 1000, 85));

        var purchase = new CreatePurchaseOrderDto(supplier.Id, null, "Weekly produce delivery",
            [new(limes.Id, "1 kg crate", 4, 80, 1000)]);
        var pending = await inventory.CreatePurchaseOrderAsync(tenant, actor, purchase);
        var po = await inventory.CreatePurchaseOrderAsync(tenant, actor, purchase);
        await inventory.UpdatePurchaseOrderStatusAsync(tenant, actor, po.Id, "Approved");
        var now = DateTime.UtcNow;
        var receipt = new ReceiveGoodsDto(Guid.NewGuid(), supplier.Id, "KADAL-DEMO-001", now,
            160, 0, 0, 20, 180, "Two of four crates", po.Id,
            [new(limes.Id, po.Items.Single().Id, "1 kg crate", 2, 1000, 80, 0, 0, 180,
                now.AddDays(3), "KADAL-FRESH")]);
        var invoice = await inventory.ReceiveGoodsAsync(tenant, actor, receipt);
        var replay = await inventory.ReceiveGoodsAsync(tenant, actor, receipt);
        Require(replay.Id == invoice.Id, "Purchase receipt replay");
        var invoiceLine = invoice.Items.Single();
        Require(invoiceLine.BaseQuantity == 2000 && invoiceLine.UnitCostPerBaseUnit == .09m, "Landed lime cost");
        var returned = await inventory.CreatePurchaseReturnAsync(tenant, actor,
            new(Guid.NewGuid(), invoice.Id, "KADAL-CREDIT-001", "Damaged produce", null, [new(invoiceLine.Id, 100)]));

        var direct = await inventory.ReceiveGoodsAsync(tenant, actor,
            new(Guid.NewGuid(), secondSupplier.Id, "KADAL-DEMO-001", now, 85, 0, 0, 0, 85, null, null,
                [new(limes.Id, null, "1 kg crate", 1, 1000, 85, 0, 0, 85, now.AddDays(1), "WEST-HOLD")]));
        Require(direct.Id != invoice.Id, "Independent suppliers can share an invoice number");
        var heldLot = direct.Items.Single().StockLotId ?? throw new InvalidOperationException("Direct purchase produced no lot");
        await inventory.QuarantineLotAsync(tenant, actor, heldLot, new(Guid.NewGuid(), "Quality inspection"));

        var prep = await inventory.SavePreparationRecipeAsync(tenant, new(cordial.Id, 500,
            "Press and strain fresh limes", [new(limes.Id, 900, 100)]));
        var batch = await inventory.RecordPreparationBatchAsync(tenant, actor,
            new(Guid.NewGuid(), prep.Id, 1, 450, 50, "Straining loss", now.AddDays(2)));
        Require(batch.ActualYieldQuantity == 450 && batch.OutputStockLotId.HasValue, "Prepared cordial yield");

        var central = await inventory.CreateOutletAsync(tenant, new("Heritage Central Kitchen", "HCK", true));
        var counter = await inventory.CreateOutletAsync(tenant, new("MG Road Counter", "MGRC"));
        var transferRequest = new CreateStockTransferDto(central.Id, counter.Id, "Counter replenishment",
            [new(cordial.Id, 200)]);
        var requested = await inventory.CreateTransferAsync(tenant, actor, transferRequest);
        var transfer = await inventory.CreateTransferAsync(tenant, actor, transferRequest);
        await inventory.DispatchTransferAsync(tenant, actor, transfer.Id, new(Guid.NewGuid(),
            [new(transfer.Items.Single().Id, 200)]));
        var received = await inventory.ReceiveTransferAsync(tenant, actor, transfer.Id, new(Guid.NewGuid(),
            [new(transfer.Items.Single().Id, 190, "10 ml spilled")]));
        var receivedLine = received.Items.Single();
        Require(receivedLine.DispatchedQuantity == 200 && receivedLine.ReceivedQuantity == 190
            && receivedLine.DiscrepancyQuantity == 10, "Outlet transfer quantities");

        var count = await inventory.StartStocktakeAsync(tenant, actor, Guid.NewGuid(), "Closing count in progress");
        return new
        {
            pendingPurchaseOrder = pending.Id, partialPurchaseOrder = po.Id, invoice = invoice.Id,
            purchaseReturn = returned.Id, directInvoice = direct.Id, quarantinedLot = heldLot,
            preparationBatch = batch.Id, requestedTransfer = requested.Id, receivedTransfer = received.Id,
            stocktake = count.Id
        };
    }

    private static void Require(bool condition, string scenario)
    {
        if (!condition)
            throw new InvalidOperationException($"Demo verification failed: {scenario}");
    }
}
