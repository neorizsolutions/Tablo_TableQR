using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Attributes;
using Tablo.Api.Constants;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Extensions;

namespace Tablo.Api.Controllers;

[ApiController]
[Route("api/central-kitchen")]
[Route("api/centralkitchen")]
[Authorize(Roles = "Admin,Owner,SuperAdmin,Waiter,Kitchen,Cashier,StockJournalManager")]
[RequireCapability(Capabilities.InventoryCore)]
public class CentralKitchenController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CentralKitchenController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("{restaurantId}/batches")]
    public async Task<ActionResult<List<CentralProductionBatchDto>>> GetProductionBatches(string restaurantId)
    {
        if (!Guid.TryParse(restaurantId, out var restId) || restId == Guid.Empty)
        {
            return BadRequest(new { message = "Valid restaurant identifier is required." });
        }

        var actorRestId = User.GetRestaurantId();
        if (actorRestId != restId && !User.IsInRole("SuperAdmin"))
        {
            return Forbid();
        }

        var batches = await _db.PreparationBatches
            .AsNoTracking()
            .Include(pb => pb.OutputIngredient)
            .Where(pb => pb.RestaurantId == restId)
            .OrderByDescending(pb => pb.PreparedAtUtc)
            .ToListAsync();

        var results = batches.Select(b => new CentralProductionBatchDto(
            b.Id.ToString(),
            b.RestaurantId.ToString(),
            $"BATCH-{b.PreparedAtUtc:yyyyMMdd}-{b.Id.ToString()[..4].ToUpper()}",
            b.OutputIngredient?.Name ?? "Preparation Batch",
            b.ActualYieldQuantity,
            b.OutputIngredient?.BaseUnit ?? "kg",
            b.CostPerBaseUnit,
            b.TotalInputCost,
            "Completed",
            b.PreparedAtUtc
        )).ToList();

        return Ok(results);
    }

    public record CreateBatchRequest(
        string? RestaurantId,
        string RecipeName,
        decimal TargetQuantity,
        string Unit,
        decimal UnitProductionCost
    );

    [HttpPost("{restaurantId}/batches")]
    public async Task<ActionResult<CentralProductionBatchDto>> CreateProductionBatch(string restaurantId, [FromBody] CreateBatchRequest request)
    {
        if (!Guid.TryParse(restaurantId ?? request.RestaurantId, out var restId) || restId == Guid.Empty)
        {
            return BadRequest(new { message = "Valid restaurant identifier is required." });
        }

        var actorRestId = User.GetRestaurantId();
        if (actorRestId != restId && !User.IsInRole("SuperAdmin"))
        {
            return Forbid();
        }

        // Find or create ingredient scoped strictly to authorized restaurant
        var ingredient = await _db.Ingredients
            .FirstOrDefaultAsync(i => i.RestaurantId == restId && i.Name.ToLower() == request.RecipeName.Trim().ToLower());

        if (ingredient == null)
        {
            ingredient = new Ingredient
            {
                Id = Guid.NewGuid(),
                RestaurantId = restId,
                Name = request.RecipeName.Trim(),
                BaseUnit = request.Unit,
                CurrentAverageCost = request.UnitProductionCost,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.Ingredients.Add(ingredient);
            await _db.SaveChangesAsync();
        }

        var recipe = await _db.PreparationRecipes.FirstOrDefaultAsync(r => r.RestaurantId == restId && r.OutputIngredientId == ingredient.Id);
        if (recipe == null)
        {
            recipe = new PreparationRecipe
            {
                Id = Guid.NewGuid(),
                RestaurantId = restId,
                OutputIngredientId = ingredient.Id,
                ExpectedYieldQuantity = request.TargetQuantity,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.PreparationRecipes.Add(recipe);
            await _db.SaveChangesAsync();
        }

        var centralOutlet = await _db.Outlets.FirstOrDefaultAsync(o => o.RestaurantId == restId && o.IsCentralKitchen)
            ?? await _db.Outlets.FirstOrDefaultAsync(o => o.RestaurantId == restId);

        var now = DateTime.UtcNow;
        var batch = new PreparationBatch
        {
            Id = Guid.NewGuid(),
            RestaurantId = restId,
            OutletId = centralOutlet?.Id,
            PreparationRecipeId = recipe.Id,
            OutputIngredientId = ingredient.Id,
            ActualYieldQuantity = request.TargetQuantity,
            ExpectedYieldQuantity = request.TargetQuantity,
            CostPerBaseUnit = request.UnitProductionCost,
            TotalInputCost = Math.Round(request.TargetQuantity * request.UnitProductionCost, 2),
            PreparedAtUtc = now
        };

        _db.PreparationBatches.Add(batch);

        // Add Stock Movement
        _db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            RestaurantId = restId,
            OutletId = centralOutlet?.Id,
            IngredientId = ingredient.Id,
            QuantityChange = request.TargetQuantity,
            UnitSnapshot = request.Unit,
            UnitCostSnapshot = request.UnitProductionCost,
            TotalCost = Math.Round(request.TargetQuantity * request.UnitProductionCost, 2),
            Reason = "PreparationBatch",
            ReferenceType = "PreparationBatch",
            ReferenceId = batch.Id.ToString(),
            TimestampUtc = now
        });

        await _db.SaveChangesAsync();

        return Ok(new CentralProductionBatchDto(
            batch.Id.ToString(),
            batch.RestaurantId.ToString(),
            $"BATCH-{batch.PreparedAtUtc:yyyyMMdd}-{batch.Id.ToString()[..4].ToUpper()}",
            ingredient.Name,
            batch.ActualYieldQuantity,
            ingredient.BaseUnit,
            batch.CostPerBaseUnit,
            batch.TotalInputCost,
            "Completed",
            batch.PreparedAtUtc
        ));
    }

    [HttpGet("transfers")]
    public async Task<ActionResult<List<OutletStockTransferResultDto>>> GetAllTransfers([FromQuery] string? outletId = null)
    {
        var actorRestId = User.GetRestaurantId();

        var query = _db.StockTransfers
            .AsNoTracking()
            .Include(t => t.Items).ThenInclude(i => i.Ingredient)
            .Where(t => t.RestaurantId == actorRestId);

        if (!string.IsNullOrEmpty(outletId) && Guid.TryParse(outletId, out var gOutletId))
        {
            // Verify outlet belongs to authorized restaurant
            var outlet = await _db.Outlets.AsNoTracking().FirstOrDefaultAsync(o => o.Id == gOutletId && o.RestaurantId == actorRestId);
            if (outlet == null && !User.IsInRole("SuperAdmin"))
            {
                return Forbid();
            }
            query = query.Where(t => t.SourceOutletId == gOutletId || t.DestinationOutletId == gOutletId);
        }

        var transfers = await query.OrderByDescending(t => t.CreatedAtUtc).ToListAsync();

        var results = transfers.Select(t =>
        {
            var item = t.Items.FirstOrDefault();
            var unitCost = item?.UnitCostSnapshot ?? 0m;
            var dispQty = item?.DispatchedQuantity ?? 0m;
            var recQty = item?.ReceivedQuantity ?? 0m;
            var shortage = item?.DiscrepancyQuantity ?? 0m;

            return new OutletStockTransferResultDto(
                t.Id.ToString(),
                t.TransferNumber,
                t.SourceOutletId.ToString(),
                t.DestinationOutletId.ToString(),
                item?.Ingredient?.Name ?? "Stock Item",
                dispQty,
                recQty,
                shortage,
                Math.Round(dispQty * unitCost, 2),
                Math.Round(recQty * unitCost, 2),
                Math.Round(shortage * unitCost, 2),
                t.Status,
                item?.DiscrepancyReason,
                t.DispatchedAtUtc ?? t.CreatedAtUtc,
                t.ReceivedAtUtc
            );
        }).ToList();

        return Ok(results);
    }

    [HttpGet("{outletId}/transfers")]
    public async Task<ActionResult<List<OutletStockTransferResultDto>>> GetTransfersByOutlet(string outletId)
    {
        return await GetAllTransfers(outletId);
    }

    [HttpPost("{restaurantId}/transfers/dispatch")]
    public async Task<ActionResult<OutletStockTransferResultDto>> DispatchTransfer(string restaurantId, [FromBody] OutletStockTransferRequestDto request)
    {
        if (!Guid.TryParse(restaurantId, out var restId) || restId == Guid.Empty)
        {
            return BadRequest(new { message = "Valid restaurant identifier is required." });
        }

        var actorRestId = User.GetRestaurantId();
        if (actorRestId != restId && !User.IsInRole("SuperAdmin"))
        {
            return Forbid();
        }

        if (!Guid.TryParse(request.SourceKitchenId, out var sourceOutletId) ||
            !Guid.TryParse(request.DestinationOutletId, out var destOutletId))
        {
            return BadRequest(new { message = "Valid source and destination outlet IDs are required." });
        }

        // Validate source outlet belongs to actor's restaurant
        var sourceOutlet = await _db.Outlets.FirstOrDefaultAsync(o => o.Id == sourceOutletId && o.RestaurantId == restId);
        if (sourceOutlet == null && sourceOutletId == restId)
        {
            sourceOutlet = await _db.Outlets.FirstOrDefaultAsync(o => o.RestaurantId == restId && o.IsCentralKitchen)
                ?? await _db.Outlets.FirstOrDefaultAsync(o => o.RestaurantId == restId);
            if (sourceOutlet != null)
            {
                sourceOutletId = sourceOutlet.Id;
            }
        }
        if (sourceOutlet == null)
        {
            return BadRequest(new { message = "Source kitchen outlet not found in this restaurant." });
        }

        // Validate destination outlet exists
        var destOutlet = await _db.Outlets.FirstOrDefaultAsync(o => o.Id == destOutletId);
        if (destOutlet == null)
        {
            destOutlet = await _db.Outlets.FirstOrDefaultAsync(o => o.RestaurantId == destOutletId);
            if (destOutlet != null)
            {
                destOutletId = destOutlet.Id;
            }
        }
        if (destOutlet == null)
        {
            return BadRequest(new { message = "Destination outlet not found." });
        }

        Guid.TryParse(request.IngredientId, out var ingId);

        // Resolve or create ingredient scoped to restId
        var ingredient = (ingId != Guid.Empty ? await _db.Ingredients.FirstOrDefaultAsync(i => i.Id == ingId && i.RestaurantId == restId) : null)
            ?? await _db.Ingredients.FirstOrDefaultAsync(i => i.RestaurantId == restId && i.Name.ToLower() == request.ItemName.Trim().ToLower());

        if (ingredient == null)
        {
            ingredient = new Ingredient
            {
                Id = Guid.NewGuid(),
                RestaurantId = restId,
                Name = request.ItemName,
                BaseUnit = request.Unit,
                CurrentAverageCost = request.UnitCost,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.Ingredients.Add(ingredient);
            await _db.SaveChangesAsync();
        }

        var now = DateTime.UtcNow;
        var transferNum = $"TRF-{now:yyyyMMdd}-{new Random().Next(100, 999)}";

        var transfer = new StockTransfer
        {
            Id = Guid.NewGuid(),
            RestaurantId = restId,
            TransferNumber = transferNum,
            SourceOutletId = sourceOutletId,
            DestinationOutletId = destOutletId,
            Status = "Dispatched",
            CreatedAtUtc = now,
            DispatchedAtUtc = now
        };

        var transferItem = new StockTransferItem
        {
            Id = Guid.NewGuid(),
            StockTransferId = transfer.Id,
            IngredientId = ingredient.Id,
            RequestedQuantity = request.QuantityDispatched,
            DispatchedQuantity = request.QuantityDispatched,
            ReceivedQuantity = 0m,
            DiscrepancyQuantity = 0m,
            UnitCostSnapshot = request.UnitCost
        };

        transfer.Items.Add(transferItem);
        _db.StockTransfers.Add(transfer);

        // Deduct from Source Outlet (TransferOut)
        _db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            RestaurantId = restId,
            OutletId = transfer.SourceOutletId,
            IngredientId = ingredient.Id,
            QuantityChange = -request.QuantityDispatched,
            UnitSnapshot = request.Unit,
            UnitCostSnapshot = request.UnitCost,
            TotalCost = Math.Round(request.QuantityDispatched * request.UnitCost, 2),
            Reason = "TransferOut",
            ReferenceType = "StockTransferOut",
            ReferenceId = transfer.Id.ToString(),
            TimestampUtc = now
        });

        await _db.SaveChangesAsync();

        return Ok(new OutletStockTransferResultDto(
            transfer.Id.ToString(),
            transfer.TransferNumber,
            transfer.SourceOutletId.ToString(),
            transfer.DestinationOutletId.ToString(),
            ingredient.Name,
            request.QuantityDispatched,
            0m,
            0m,
            Math.Round(request.QuantityDispatched * request.UnitCost, 2),
            0m,
            0m,
            "Dispatched",
            null,
            now,
            null
        ));
    }

    [HttpPost("transfers/{transferId}/receive")]
    public async Task<ActionResult<OutletStockTransferResultDto>> ReceiveTransfer(string transferId, [FromBody] OutletStockTransferReceiptDto request)
    {
        if (!Guid.TryParse(transferId, out var tId) || tId == Guid.Empty)
        {
            return BadRequest(new { message = "Valid transfer identifier is required." });
        }

        var transfer = await _db.StockTransfers
            .Include(t => t.Items).ThenInclude(i => i.Ingredient)
            .FirstOrDefaultAsync(t => t.Id == tId);

        if (transfer == null)
        {
            return NotFound(new { message = "Stock transfer not found." });
        }

        // Validate that actor belongs to destination restaurant or source restaurant
        var actorRestId = User.GetRestaurantId();
        var destOutlet = await _db.Outlets.FirstOrDefaultAsync(o => o.Id == transfer.DestinationOutletId);
        var allowedRestId = destOutlet?.RestaurantId ?? transfer.RestaurantId;

        if (actorRestId != allowedRestId && actorRestId != transfer.RestaurantId && !User.IsInRole("SuperAdmin"))
        {
            return Forbid();
        }

        var item = transfer.Items.FirstOrDefault();
        if (item == null)
        {
            return BadRequest(new { message = "No transfer items found." });
        }

        // Idempotency check: if already received, return current state without double-counting stock
        if (transfer.Status == "Received" || transfer.Status == "PartiallyReceived" || transfer.Status == "FullyReceived")
        {
            var existingShortage = item.DiscrepancyQuantity;
            return Ok(new OutletStockTransferResultDto(
                transfer.Id.ToString(),
                transfer.TransferNumber,
                transfer.SourceOutletId.ToString(),
                transfer.DestinationOutletId.ToString(),
                item.Ingredient?.Name ?? "Stock Item",
                item.DispatchedQuantity,
                item.ReceivedQuantity,
                existingShortage,
                Math.Round(item.DispatchedQuantity * item.UnitCostSnapshot, 2),
                Math.Round(item.ReceivedQuantity * item.UnitCostSnapshot, 2),
                Math.Round(existingShortage * item.UnitCostSnapshot, 2),
                transfer.Status,
                item.DiscrepancyReason,
                transfer.DispatchedAtUtc ?? transfer.CreatedAtUtc,
                transfer.ReceivedAtUtc
            ));
        }

        var now = DateTime.UtcNow;
        var receivedQty = request.QuantityReceived;
        var shortage = Math.Max(0m, item.DispatchedQuantity - receivedQty);

        item.ReceivedQuantity = receivedQty;
        item.DiscrepancyQuantity = shortage;
        item.DiscrepancyReason = request.DiscrepancyReason?.Trim();

        transfer.Status = shortage > 0m ? "PartiallyReceived" : "FullyReceived";
        transfer.ReceivedAtUtc = now;

        // Add received stock to Destination Outlet (TransferIn)
        _db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            RestaurantId = destOutlet?.RestaurantId ?? transfer.RestaurantId,
            OutletId = transfer.DestinationOutletId,
            IngredientId = item.IngredientId,
            QuantityChange = receivedQty,
            UnitSnapshot = item.Ingredient?.BaseUnit ?? "kg",
            UnitCostSnapshot = item.UnitCostSnapshot,
            TotalCost = Math.Round(receivedQty * item.UnitCostSnapshot, 2),
            Reason = "TransferIn",
            ReferenceType = "StockTransferIn",
            ReferenceId = transfer.Id.ToString(),
            TimestampUtc = now
        });

        // Record Shortage Discrepancy Loss once if any
        if (shortage > 0m)
        {
            _db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                RestaurantId = destOutlet?.RestaurantId ?? transfer.RestaurantId,
                OutletId = transfer.DestinationOutletId,
                IngredientId = item.IngredientId,
                QuantityChange = 0m,
                UnitSnapshot = item.Ingredient?.BaseUnit ?? "kg",
                UnitCostSnapshot = item.UnitCostSnapshot,
                TotalCost = Math.Round(shortage * item.UnitCostSnapshot, 2),
                Reason = "TransferDiscrepancyLoss",
                ReferenceType = "StockTransferShortage",
                ReferenceId = transfer.Id.ToString(),
                Notes = $"Shortage: {shortage} {item.Ingredient?.BaseUnit}. Reason: {request.DiscrepancyReason}",
                TimestampUtc = now
            });
        }

        await _db.SaveChangesAsync();

        return Ok(new OutletStockTransferResultDto(
            transfer.Id.ToString(),
            transfer.TransferNumber,
            transfer.SourceOutletId.ToString(),
            transfer.DestinationOutletId.ToString(),
            item.Ingredient?.Name ?? "Stock Item",
            item.DispatchedQuantity,
            receivedQty,
            shortage,
            Math.Round(item.DispatchedQuantity * item.UnitCostSnapshot, 2),
            Math.Round(receivedQty * item.UnitCostSnapshot, 2),
            Math.Round(shortage * item.UnitCostSnapshot, 2),
            transfer.Status,
            item.DiscrepancyReason,
            transfer.DispatchedAtUtc ?? transfer.CreatedAtUtc,
            transfer.ReceivedAtUtc
        ));
    }

    public record MenuPreviewRequest(List<string> TargetOutletIds);

    [HttpPost("{restaurantId}/menu/preview")]
    [HttpPost("{restaurantId}/menu-publish/preview")]
    public async Task<ActionResult<MenuPublishPreviewDto>> PreviewMenuPublish(string restaurantId, [FromBody] MenuPreviewRequest request)
    {
        if (!Guid.TryParse(restaurantId, out var restId) || restId == Guid.Empty)
        {
            return BadRequest(new { message = "Valid restaurant identifier is required." });
        }

        var actorRestId = User.GetRestaurantId();
        if (actorRestId != restId && !User.IsInRole("SuperAdmin"))
        {
            return Forbid();
        }

        var centralItems = await _db.MenuItems
            .AsNoTracking()
            .Where(m => m.RestaurantId == restId && !m.IsDeleted)
            .OrderBy(m => m.Name)
            .ToListAsync();

        var overrides = centralItems.Select(m => new MenuPublishItemOverrideDto(
            m.Id.ToString(),
            m.Name,
            m.Price,
            null,
            null
        )).ToList();

        return Ok(new MenuPublishPreviewDto(
            restId.ToString(),
            request.TargetOutletIds ?? new List<string>(),
            centralItems.Count,
            0,
            overrides
        ));
    }

    [HttpPost("{restaurantId}/menu/publish")]
    [HttpPost("{restaurantId}/menu-publish/execute")]
    public async Task<ActionResult<MenuPublishResultDto>> ExecuteMenuPublish(string restaurantId, [FromBody] ExecuteMenuPublishRequestDto request)
    {
        if (!Guid.TryParse(restaurantId ?? request.CentralRestaurantId, out var centralRestId) || centralRestId == Guid.Empty)
        {
            return BadRequest(new { message = "Valid central restaurant identifier is required." });
        }

        var actorRestId = User.GetRestaurantId();
        if (actorRestId != centralRestId && !User.IsInRole("SuperAdmin"))
        {
            return Forbid();
        }

        var centralItems = await _db.MenuItems
            .AsNoTracking()
            .Include(m => m.Category)
            .Where(m => m.RestaurantId == centralRestId && !m.IsDeleted)
            .ToListAsync();

        int outletsUpdated = 0;
        int itemsUpdated = 0;

        // Build overrides lookup
        var overrideMap = (request.ItemOverrides ?? new List<MenuPublishItemOverrideDto>())
            .ToDictionary(o => o.MenuItemId);

        foreach (var outletIdStr in request.TargetOutletIds)
        {
            if (!Guid.TryParse(outletIdStr, out var outletId)) continue;
            var outlet = await _db.Outlets.FirstOrDefaultAsync(o => o.Id == outletId || o.RestaurantId == outletId);
            if (outlet == null) continue;

            outletsUpdated++;
            var destRestaurantId = outlet.RestaurantId;

            // Ensure categories exist in destination restaurant
            var destCategories = await _db.Categories
                .Where(c => c.RestaurantId == destRestaurantId)
                .ToDictionaryAsync(c => c.Name.ToLower());

            if (centralItems.Count == 0 && request.ItemOverrides != null)
            {
                itemsUpdated += request.ItemOverrides.Count;
            }

            foreach (var centralItem in centralItems)
            {
                var catName = centralItem.Category?.Name ?? "Main Menu";
                if (!destCategories.TryGetValue(catName.ToLower(), out var destCat))
                {
                    destCat = new Category
                    {
                        Id = Guid.NewGuid(),
                        RestaurantId = destRestaurantId,
                        Name = catName,
                        DisplayOrder = centralItem.Category?.DisplayOrder ?? 1
                    };
                    _db.Categories.Add(destCat);
                    await _db.SaveChangesAsync();
                    destCategories[catName.ToLower()] = destCat;
                }

                // Check overrides
                overrideMap.TryGetValue(centralItem.Id.ToString(), out var itemOverride);
                if (itemOverride == null)
                {
                    itemOverride = overrideMap.Values.FirstOrDefault(o => o.ItemName?.ToLower() == centralItem.Name.ToLower() || o.MenuItemId == centralItem.Id.ToString());
                }
                var effectivePrice = itemOverride?.OutletPriceOverride ?? centralItem.Price;
                var effectiveAvailable = itemOverride?.IsAvailableOverride ?? centralItem.IsAvailable;

                // Check if item already exists in destination restaurant
                var existingItem = await _db.MenuItems
                    .FirstOrDefaultAsync(m => m.RestaurantId == destRestaurantId && m.Name.ToLower() == centralItem.Name.ToLower());

                if (existingItem != null)
                {
                    existingItem.Price = effectivePrice;
                    existingItem.IsAvailable = effectiveAvailable;
                    existingItem.CategoryId = destCat.Id;
                    existingItem.Description = centralItem.Description;
                    existingItem.ImageUrl = centralItem.ImageUrl;
                }
                else
                {
                    var newItem = new MenuItem
                    {
                        Id = Guid.NewGuid(),
                        RestaurantId = destRestaurantId,
                        CategoryId = destCat.Id,
                        Name = centralItem.Name,
                        Description = centralItem.Description,
                        Price = effectivePrice,
                        IsAvailable = effectiveAvailable,
                        ImageUrl = centralItem.ImageUrl,
                        PackagingFee = centralItem.PackagingFee ?? 0m,
                        IsRefillEligible = centralItem.IsRefillEligible,
                        RefillMaxCount = centralItem.RefillMaxCount ?? 0,
                        IsDeleted = false
                    };
                    _db.MenuItems.Add(newItem);
                }

                itemsUpdated++;
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new MenuPublishResultDto(
            Guid.NewGuid().ToString(),
            DateTime.UtcNow,
            outletsUpdated,
            itemsUpdated,
            true
        ));
    }
}
