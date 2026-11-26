using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tablo.Api.Attributes;
using Tablo.Api.Constants;
using Tablo.Api.DTOs;
using Tablo.Api.Extensions;
using Tablo.Api.Services;

namespace Tablo.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Owner,Waiter,Kitchen,Cashier")]
[RequireCapability(Capabilities.InventoryCore)]
[Route("api/restaurants/{restaurantId:guid}/inventory")]
public class InventoryController(InventoryService inventory, InventoryReportService reports) : ControllerBase
{
    [HttpGet("ingredients")]
    public Task<IActionResult> GetIngredients(Guid restaurantId) =>
        Execute(restaurantId, async () => (object)await inventory.GetIngredientsAsync(restaurantId));

    [HttpPost("ingredients")]
    public Task<IActionResult> CreateIngredient(Guid restaurantId, CreateIngredientDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.CreateIngredientAsync(restaurantId, User.GetUserId(), dto));

    [HttpPut("ingredients/{id:guid}")]
    public Task<IActionResult> UpdateIngredient(Guid restaurantId, Guid id, UpdateIngredientDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.UpdateIngredientAsync(restaurantId, id, dto));

    [HttpPost("ingredients/{id:guid}/movements")]
    public Task<IActionResult> RecordMovement(Guid restaurantId, Guid id, RecordStockMovementDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.RecordMovementAsync(restaurantId, User.GetUserId(), id, dto));

    [HttpGet("movements")]
    public Task<IActionResult> GetMovements(Guid restaurantId, [FromQuery] Guid? ingredientId = null, [FromQuery] int limit = 200) =>
        Execute(restaurantId, async () => (object)await inventory.GetMovementsAsync(restaurantId, ingredientId, limit));

    [HttpGet("recipes")]
    public Task<IActionResult> GetRecipes(Guid restaurantId) =>
        Execute(restaurantId, async () => (object)await inventory.GetRecipesAsync(restaurantId));

    [HttpPut("recipes/{menuItemId:guid}")]
    public Task<IActionResult> SetRecipe(Guid restaurantId, Guid menuItemId, SetRecipeDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.SetRecipeAsync(restaurantId, User.GetUserId(), menuItemId, dto));

    [HttpGet("stocktakes")]
    public Task<IActionResult> GetStocktakes(Guid restaurantId) =>
        Execute(restaurantId, async () => (object)await inventory.GetStocktakesAsync(restaurantId));

    public record StartStocktakeRequest(Guid OperationId, string? Notes);

    [HttpPost("stocktakes/start")]
    public Task<IActionResult> StartStocktake(Guid restaurantId, StartStocktakeRequest req) =>
        Execute(restaurantId, async () => (object)await inventory.StartStocktakeAsync(restaurantId, User.GetUserId(), req.OperationId, req.Notes));

    [HttpPut("stocktakes/{id:guid}/draft")]
    public Task<IActionResult> SaveDraft(Guid restaurantId, Guid id, SaveStocktakeDraftDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.SaveStocktakeDraftAsync(restaurantId, User.GetUserId(), id, dto));

    [HttpPost("stocktakes/{id:guid}/post")]
    public Task<IActionResult> PostStocktake(Guid restaurantId, Guid id, PostStocktakeRequestDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.PostStocktakeAsync(restaurantId, User.GetUserId(), id, dto));

    // --- REPORTS & EXCEL EXPORTS ---

    [HttpGet("reports/stock")]
    public Task<IActionResult> GetStockReport(Guid restaurantId, [FromQuery] StockReportFilter filter) =>
        Execute(restaurantId, async () => (object)await reports.GetStockReportAsync(restaurantId, filter));

    [HttpGet("reports/stock/export")]
    public async Task<IActionResult> ExportStockReport(Guid restaurantId, [FromQuery] StockReportFilter filter)
    {
        if (restaurantId == Guid.Empty || User.GetRestaurantId() != restaurantId || User.GetUserId() == Guid.Empty)
            return Forbid();

        try
        {
            var bytes = await reports.ExportStockReportAsync(restaurantId, filter);
            var fileName = $"Tablo_Stock_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("reports/sales-gross-profit")]
    public Task<IActionResult> GetSalesGrossProfitReport(Guid restaurantId, [FromQuery] SalesGrossProfitFilter filter) =>
        Execute(restaurantId, async () => (object)await reports.GetSalesGrossProfitReportAsync(restaurantId, filter));

    [HttpGet("reports/sales-gross-profit/export")]
    public async Task<IActionResult> ExportSalesGrossProfitReport(Guid restaurantId, [FromQuery] SalesGrossProfitFilter filter)
    {
        if (restaurantId == Guid.Empty || User.GetRestaurantId() != restaurantId || User.GetUserId() == Guid.Empty)
            return Forbid();

        try
        {
            var bytes = await reports.ExportSalesGrossProfitReportAsync(restaurantId, filter);
            var fileName = $"Tablo_Sales_Gross_Profit_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // --- 1. SUPPLIERS ---

    [HttpGet("suppliers")]
    public Task<IActionResult> GetSuppliers(Guid restaurantId, [FromQuery] bool includeInactive = true) =>
        Execute(restaurantId, async () => (object)await inventory.GetSuppliersAsync(restaurantId, includeInactive));

    [HttpPost("suppliers")]
    public Task<IActionResult> CreateSupplier(Guid restaurantId, CreateSupplierDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.CreateSupplierAsync(restaurantId, dto));

    [HttpPut("suppliers/{id:guid}")]
    public Task<IActionResult> UpdateSupplier(Guid restaurantId, Guid id, UpdateSupplierDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.UpdateSupplierAsync(restaurantId, id, dto));

    [HttpGet("suppliers/{id:guid}/ingredients")]
    public Task<IActionResult> GetSupplierIngredients(Guid restaurantId, Guid id) =>
        Execute(restaurantId, async () => (object)await inventory.GetSupplierIngredientsAsync(restaurantId, id));

    [HttpPut("suppliers/{id:guid}/ingredients")]
    public Task<IActionResult> SetSupplierIngredient(Guid restaurantId, Guid id, SetSupplierIngredientDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.SetSupplierIngredientAsync(restaurantId, dto with { SupplierId = id }));

    [HttpGet("ingredients/{id:guid}/price-comparison")]
    public Task<IActionResult> CompareIngredientPrices(Guid restaurantId, Guid id) =>
        Execute(restaurantId, async () => (object)await inventory.CompareIngredientPricesAsync(restaurantId, id));

    // --- 2. PURCHASE ORDERS & INVOICES ---

    [HttpGet("purchase-orders")]
    public Task<IActionResult> GetPurchaseOrders(Guid restaurantId, [FromQuery] string? status = null) =>
        Execute(restaurantId, async () => (object)await inventory.GetPurchaseOrdersAsync(restaurantId, status));

    [HttpPost("purchase-orders")]
    public Task<IActionResult> CreatePurchaseOrder(Guid restaurantId, CreatePurchaseOrderDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.CreatePurchaseOrderAsync(restaurantId, User.GetUserId(), dto));

    public record UpdateOrderStatusDto(string Status);

    [HttpPut("purchase-orders/{id:guid}/status")]
    public Task<IActionResult> UpdatePurchaseOrderStatus(Guid restaurantId, Guid id, UpdateOrderStatusDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.UpdatePurchaseOrderStatusAsync(restaurantId, User.GetUserId(), id, dto.Status));

    [HttpPost("goods-receipts")]
    public Task<IActionResult> ReceiveGoods(Guid restaurantId, ReceiveGoodsDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.ReceiveGoodsAsync(restaurantId, User.GetUserId(), dto));

    [HttpGet("invoices")]
    public Task<IActionResult> GetInvoices(Guid restaurantId, [FromQuery] Guid? supplierId = null) =>
        Execute(restaurantId, async () => (object)await inventory.GetInvoicesAsync(restaurantId, supplierId));

    [HttpGet("invoices/{id:guid}")]
    public Task<IActionResult> GetInvoice(Guid restaurantId, Guid id) =>
        Execute(restaurantId, async () => (object)await inventory.GetInvoiceByIdAsync(restaurantId, id));

    // --- 3. PURCHASE RETURNS ---

    [HttpPost("purchase-returns")]
    public Task<IActionResult> CreatePurchaseReturn(Guid restaurantId, CreatePurchaseReturnDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.CreatePurchaseReturnAsync(restaurantId, User.GetUserId(), dto));

    [HttpGet("purchase-returns")]
    public Task<IActionResult> GetPurchaseReturns(Guid restaurantId) =>
        Execute(restaurantId, async () => (object)await inventory.GetPurchaseReturnsAsync(restaurantId));

    [HttpGet("purchase-returns/{id:guid}")]
    public Task<IActionResult> GetPurchaseReturn(Guid restaurantId, Guid id) =>
        Execute(restaurantId, async () => (object)await inventory.GetPurchaseReturnByIdAsync(restaurantId, id));

    // --- 4. PREPARATION RECIPES & BATCHES ---

    [HttpGet("preparation-recipes")]
    public Task<IActionResult> GetPreparationRecipes(Guid restaurantId) =>
        Execute(restaurantId, async () => (object)await inventory.GetPreparationRecipesAsync(restaurantId));

    [HttpPut("preparation-recipes")]
    public Task<IActionResult> SavePreparationRecipe(Guid restaurantId, SavePreparationRecipeDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.SavePreparationRecipeAsync(restaurantId, dto));

    [HttpPost("preparation-batches")]
    public Task<IActionResult> RecordPreparationBatch(Guid restaurantId, RecordPreparationBatchDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.RecordPreparationBatchAsync(restaurantId, User.GetUserId(), dto));

    [HttpGet("preparation-batches")]
    public Task<IActionResult> GetPreparationBatches(Guid restaurantId) =>
        Execute(restaurantId, async () => (object)await inventory.GetPreparationBatchesAsync(restaurantId));

    // --- 5. LOTS & EXPIRY ---

    [HttpGet("lots")]
    public Task<IActionResult> GetLots(Guid restaurantId, [FromQuery] string? status = null, [FromQuery] bool? expiringSoon = null) =>
        Execute(restaurantId, async () => (object)await inventory.GetLotsAsync(restaurantId, status, expiringSoon));

    [HttpPost("lots/{id:guid}/quarantine")]
    public Task<IActionResult> QuarantineLot(Guid restaurantId, Guid id, QuarantineLotDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.QuarantineLotAsync(restaurantId, User.GetUserId(), id, dto));

    [HttpPost("lots/{id:guid}/write-off")]
    public Task<IActionResult> WriteOffLot(Guid restaurantId, Guid id, WriteOffLotDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.WriteOffLotAsync(restaurantId, User.GetUserId(), id, dto));

    // --- 6. COSTING & SETTINGS ---

    [HttpGet("settings")]
    public Task<IActionResult> GetInventorySettings(Guid restaurantId) =>
        Execute(restaurantId, async () => (object)await inventory.GetInventorySettingsAsync(restaurantId));

    [HttpPut("settings")]
    public Task<IActionResult> UpdateInventorySettings(Guid restaurantId, UpdateInventorySettingsDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.UpdateInventorySettingsAsync(restaurantId, dto));

    // --- 7. OUTLETS & TRANSFERS ---

    [HttpGet("outlets")]
    public Task<IActionResult> GetOutlets(Guid restaurantId) =>
        Execute(restaurantId, async () => (object)await inventory.GetOutletsAsync(restaurantId));

    [HttpPost("outlets")]
    public Task<IActionResult> CreateOutlet(Guid restaurantId, CreateOutletDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.CreateOutletAsync(restaurantId, dto));

    [HttpGet("transfers")]
    public Task<IActionResult> GetTransfers(Guid restaurantId) =>
        Execute(restaurantId, async () => (object)await inventory.GetTransfersAsync(restaurantId));

    [HttpPost("transfers")]
    public Task<IActionResult> CreateTransfer(Guid restaurantId, CreateStockTransferDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.CreateTransferAsync(restaurantId, User.GetUserId(), dto));

    [HttpPost("transfers/{id:guid}/dispatch")]
    public Task<IActionResult> DispatchTransfer(Guid restaurantId, Guid id, DispatchStockTransferDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.DispatchTransferAsync(restaurantId, User.GetUserId(), id, dto));

    [HttpPost("transfers/{id:guid}/receive")]
    public Task<IActionResult> ReceiveTransfer(Guid restaurantId, Guid id, ReceiveStockTransferDto dto) =>
        Execute(restaurantId, async () => (object)await inventory.ReceiveTransferAsync(restaurantId, User.GetUserId(), id, dto));

    [HttpGet("transfers/shortage-dry-run")]
    [RequireCapability(Capabilities.OutletTransfers)]
    public Task<IActionResult> GetTransferShortageDryRun(Guid restaurantId) =>
        Execute(restaurantId, async () => (object)await inventory.GetTransferDiscrepancyDryRunAsync(restaurantId));

    [HttpPost("transfers/shortage-correction")]
    [RequireCapability(Capabilities.OutletTransfers)]
    public Task<IActionResult> ApplyTransferShortageCorrection(Guid restaurantId) =>
        Execute(restaurantId, async () => (object)await inventory.ApplyTransferDiscrepancyCorrectionAsync(restaurantId, User.GetUserId()));

    // --- 8. MENU AVAILABILITY ---

    [HttpGet("menu-availability")]
    public Task<IActionResult> GetMenuAvailability(Guid restaurantId) =>
        Execute(restaurantId, async () => (object)await inventory.GetMenuAvailabilityAsync(restaurantId));

    private async Task<IActionResult> Execute(Guid restaurantId, Func<Task<object>> action)
    {
        if (restaurantId == Guid.Empty || User.GetRestaurantId() != restaurantId || User.GetUserId() == Guid.Empty)
            return Forbid();

        try
        {
            var result = await action();
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ServiceConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
