using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;

namespace Tablo.Api.Services;

public partial class InventoryService(ApplicationDbContext db)
{
    private static readonly TimeZoneInfo India = GetIndiaTimeZone();

    private static TimeZoneInfo GetIndiaTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
            catch { return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "India Standard Time", "IST"); }
        }
    }

    public static DateTime ToIst(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, India);

    public async Task<List<IngredientDto>> GetIngredientsAsync(Guid restaurantId)
    {
        var ingredients = await db.Ingredients.AsNoTracking()
            .Where(i => i.RestaurantId == restaurantId)
            .OrderBy(i => i.Name)
            .ToListAsync();

        var balances = await db.StockMovements.AsNoTracking()
            .Where(m => m.RestaurantId == restaurantId)
            .GroupBy(m => m.IngredientId)
            .Select(g => new
            {
                IngredientId = g.Key,
                OnHand = g.Sum(m => m.QuantityChange),
                LastMovement = g.Max(m => m.TimestampUtc)
            })
            .ToDictionaryAsync(x => x.IngredientId);

        return ingredients.Select(i =>
        {
            balances.TryGetValue(i.Id, out var bal);
            var onHand = bal?.OnHand ?? 0m;
            var isLow = i.LowStockThreshold.HasValue && onHand <= i.LowStockThreshold.Value;
            return new IngredientDto(
                i.Id,
                i.Name,
                i.BaseUnit,
                i.LowStockThreshold,
                i.CurrentAverageCost,
                i.IsActive,
                onHand,
                isLow,
                bal?.LastMovement
            );
        }).ToList();
    }

    public async Task<IngredientDto> CreateIngredientAsync(Guid restaurantId, Guid actorId, CreateIngredientDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Ingredient name is required.");

        var baseUnit = UnitConverter.CanonicalizeBaseUnit(dto.BaseUnit);

        if (await db.Ingredients.AnyAsync(i => i.RestaurantId == restaurantId && i.Name.ToLower() == dto.Name.Trim().ToLower()))
            throw new ServiceConflictException($"An ingredient named '{dto.Name.Trim()}' already exists.");

        decimal initialUnitCost = 0m;
        decimal? initialTotalCost = null;
        decimal initialQuantity = 0m;

        if (dto.InitialStock.HasValue && dto.InitialStock.Value > 0)
        {
            initialQuantity = UnitConverter.NormalizeToBase(dto.InitialStock.Value, dto.InitialStockUnit ?? baseUnit, baseUnit);
            if (dto.InitialCost.HasValue && dto.InitialCost.Value > 0)
            {
                initialTotalCost = dto.InitialCost.Value;
                initialUnitCost = initialQuantity > 0 ? Math.Round(dto.InitialCost.Value / initialQuantity, 6) : 0m;
            }
        }

        var ingredient = new Ingredient
        {
            RestaurantId = restaurantId,
            Name = dto.Name.Trim(),
            BaseUnit = baseUnit,
            LowStockThreshold = dto.LowStockThreshold,
            CurrentAverageCost = initialUnitCost
        };

        db.Ingredients.Add(ingredient);

        if (initialQuantity > 0)
        {
            var movement = new StockMovement
            {
                RestaurantId = restaurantId,
                IngredientId = ingredient.Id,
                ActorEmployeeId = actorId != Guid.Empty ? actorId : null,
                TimestampUtc = DateTime.UtcNow,
                QuantityChange = initialQuantity,
                UnitSnapshot = baseUnit,
                Reason = "OpeningStock",
                UnitCostSnapshot = initialUnitCost > 0 ? initialUnitCost : null,
                TotalCost = initialTotalCost,
                OperationId = dto.OperationId,
                Notes = "Opening stock at ingredient creation"
            };
            db.StockMovements.Add(movement);
        }

        await db.SaveChangesAsync();

        var isLow = ingredient.LowStockThreshold.HasValue && initialQuantity <= ingredient.LowStockThreshold.Value;

        return new IngredientDto(
            ingredient.Id,
            ingredient.Name,
            ingredient.BaseUnit,
            ingredient.LowStockThreshold,
            ingredient.CurrentAverageCost,
            ingredient.IsActive,
            initialQuantity,
            isLow,
            DateTime.UtcNow
        );
    }

    public async Task<IngredientDto> UpdateIngredientAsync(Guid restaurantId, Guid ingredientId, UpdateIngredientDto dto)
    {
        var ingredient = await db.Ingredients.SingleOrDefaultAsync(i => i.Id == ingredientId && i.RestaurantId == restaurantId)
            ?? throw new KeyNotFoundException("Ingredient not found.");

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            var trimmed = dto.Name.Trim();
            if (trimmed != ingredient.Name && await db.Ingredients.AnyAsync(i => i.RestaurantId == restaurantId && i.Id != ingredientId && i.Name.ToLower() == trimmed.ToLower()))
                throw new ServiceConflictException($"Another ingredient named '{trimmed}' already exists.");
            ingredient.Name = trimmed;
        }

        if (dto.LowStockThreshold.HasValue)
            ingredient.LowStockThreshold = dto.LowStockThreshold.Value;

        if (dto.CurrentAverageCost.HasValue && dto.CurrentAverageCost.Value >= 0)
            ingredient.CurrentAverageCost = dto.CurrentAverageCost.Value;

        if (dto.IsActive.HasValue)
            ingredient.IsActive = dto.IsActive.Value;

        ingredient.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var onHand = await db.StockMovements.AsNoTracking()
            .Where(m => m.RestaurantId == restaurantId && m.IngredientId == ingredientId)
            .SumAsync(m => (decimal?)m.QuantityChange) ?? 0m;

        var isLow = ingredient.LowStockThreshold.HasValue && onHand <= ingredient.LowStockThreshold.Value;

        return new IngredientDto(
            ingredient.Id,
            ingredient.Name,
            ingredient.BaseUnit,
            ingredient.LowStockThreshold,
            ingredient.CurrentAverageCost,
            ingredient.IsActive,
            onHand,
            isLow,
            ingredient.UpdatedAtUtc
        );
    }

    public async Task<StockMovementDto> RecordMovementAsync(Guid restaurantId, Guid actorId, Guid ingredientId, RecordStockMovementDto dto)
    {
        if (dto.OperationId != Guid.Empty)
        {
            var existing = await db.StockMovements.Include(m => m.Ingredient).Include(m => m.ActorEmployee)
                .SingleOrDefaultAsync(m => m.RestaurantId == restaurantId && m.OperationId == dto.OperationId);
            if (existing != null)
            {
                return new StockMovementDto(
                    existing.Id,
                    existing.IngredientId,
                    existing.Ingredient.Name,
                    existing.TimestampUtc,
                    existing.QuantityChange,
                    existing.UnitSnapshot,
                    existing.Reason,
                    existing.UnitCostSnapshot,
                    existing.TotalCost,
                    existing.ReferenceType,
                    existing.ReferenceId,
                    existing.Notes,
                    existing.ActorEmployee != null ? $"{existing.ActorEmployee.FirstName} {existing.ActorEmployee.LastName}" : "System",
                    existing.OperationId
                );
            }
        }

        var ingredient = await db.Ingredients.SingleOrDefaultAsync(i => i.Id == ingredientId && i.RestaurantId == restaurantId)
            ?? throw new KeyNotFoundException("Ingredient not found.");

        var allowedReasons = new[] { "Restock", "Waste", "ManualCorrection", "OpeningStock" };
        if (!allowedReasons.Contains(dto.Reason))
            throw new ArgumentException($"Invalid movement reason '{dto.Reason}'. Allowed reasons are: {string.Join(", ", allowedReasons)}.");

        var normalizedQty = UnitConverter.NormalizeToBase(Math.Abs(dto.Quantity), dto.Unit ?? ingredient.BaseUnit, ingredient.BaseUnit);

        decimal quantityChange;
        decimal? unitCostSnapshot = null;
        decimal? totalCost = null;

        var currentOnHand = await db.StockMovements.AsNoTracking()
            .Where(m => m.RestaurantId == restaurantId && m.IngredientId == ingredientId)
            .SumAsync(m => (decimal?)m.QuantityChange) ?? 0m;

        if (dto.Reason is "Restock" or "OpeningStock")
        {
            quantityChange = normalizedQty;

            decimal inflowUnitCost;
            if (dto.TotalCost.HasValue && dto.TotalCost.Value > 0)
            {
                totalCost = dto.TotalCost.Value;
                inflowUnitCost = normalizedQty > 0 ? Math.Round(dto.TotalCost.Value / normalizedQty, 6) : 0m;
            }
            else if (dto.UnitCost.HasValue && dto.UnitCost.Value > 0)
            {
                var factor = UnitConverter.NormalizeToBase(1m, dto.Unit ?? ingredient.BaseUnit, ingredient.BaseUnit);
                inflowUnitCost = factor > 0 ? Math.Round(dto.UnitCost.Value / factor, 6) : dto.UnitCost.Value;
                totalCost = Math.Round(normalizedQty * inflowUnitCost, 4);
            }
            else
            {
                inflowUnitCost = ingredient.CurrentAverageCost;
                totalCost = Math.Round(normalizedQty * inflowUnitCost, 4);
            }

            unitCostSnapshot = inflowUnitCost;

            // Moving weighted-average formula:
            // New Average Cost = (Current Stock * Current Avg Cost + Inflow Qty * Inflow Unit Cost) / (Current Stock + Inflow Qty)
            if (inflowUnitCost > 0)
            {
                if (currentOnHand <= 0)
                {
                    ingredient.CurrentAverageCost = inflowUnitCost;
                }
                else
                {
                    var newAvg = ((currentOnHand * ingredient.CurrentAverageCost) + (normalizedQty * inflowUnitCost)) / (currentOnHand + normalizedQty);
                    ingredient.CurrentAverageCost = Math.Round(newAvg, 6);
                }
                ingredient.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
        else if (dto.Reason is "Waste")
        {
            quantityChange = -normalizedQty;
            unitCostSnapshot = ingredient.CurrentAverageCost;
            totalCost = Math.Round(normalizedQty * ingredient.CurrentAverageCost, 4);
        }
        else // ManualCorrection
        {
            quantityChange = dto.Quantity < 0 ? -normalizedQty : normalizedQty;
            unitCostSnapshot = ingredient.CurrentAverageCost;
            totalCost = Math.Round(Math.Abs(quantityChange) * ingredient.CurrentAverageCost, 4);
        }

        var movement = new StockMovement
        {
            RestaurantId = restaurantId,
            IngredientId = ingredientId,
            ActorEmployeeId = actorId != Guid.Empty ? actorId : null,
            TimestampUtc = DateTime.UtcNow,
            QuantityChange = quantityChange,
            UnitSnapshot = ingredient.BaseUnit,
            Reason = dto.Reason,
            UnitCostSnapshot = unitCostSnapshot,
            TotalCost = totalCost,
            OperationId = dto.OperationId != Guid.Empty ? dto.OperationId : null,
            Notes = dto.Notes
        };

        db.StockMovements.Add(movement);
        await db.SaveChangesAsync();

        var actor = actorId != Guid.Empty
            ? await db.Employees.AsNoTracking().Where(e => e.Id == actorId).Select(e => $"{e.FirstName} {e.LastName}").SingleOrDefaultAsync()
            : "System";

        return new StockMovementDto(
            movement.Id,
            ingredient.Id,
            ingredient.Name,
            movement.TimestampUtc,
            movement.QuantityChange,
            movement.UnitSnapshot,
            movement.Reason,
            movement.UnitCostSnapshot,
            movement.TotalCost,
            movement.ReferenceType,
            movement.ReferenceId,
            movement.Notes,
            actor,
            movement.OperationId
        );
    }

    public async Task<List<StockMovementDto>> GetMovementsAsync(Guid restaurantId, Guid? ingredientId = null, int limit = 200)
    {
        var query = db.StockMovements.AsNoTracking()
            .Include(m => m.Ingredient)
            .Include(m => m.ActorEmployee)
            .Where(m => m.RestaurantId == restaurantId);

        if (ingredientId.HasValue && ingredientId.Value != Guid.Empty)
            query = query.Where(m => m.IngredientId == ingredientId.Value);

        var movements = await query
            .OrderByDescending(m => m.TimestampUtc)
            .Take(limit)
            .ToListAsync();

        return movements.Select(m => new StockMovementDto(
            m.Id,
            m.IngredientId,
            m.Ingredient.Name,
            m.TimestampUtc,
            m.QuantityChange,
            m.UnitSnapshot,
            m.Reason,
            m.UnitCostSnapshot,
            m.TotalCost,
            m.ReferenceType,
            m.ReferenceId,
            m.Notes,
            m.ActorEmployee != null ? $"{m.ActorEmployee.FirstName} {m.ActorEmployee.LastName}" : "System",
            m.OperationId
        )).ToList();
    }

    public async Task<List<MenuItemRecipeDto>> GetRecipesAsync(Guid restaurantId)
    {
        var menuItems = await db.MenuItems.AsNoTracking()
            .Include(m => m.Category)
            .Where(m => m.RestaurantId == restaurantId && !m.IsDeleted)
            .OrderBy(m => m.Category.Name)
            .ThenBy(m => m.Name)
            .ToListAsync();

        var recipes = await db.RecipeItems.AsNoTracking()
            .Include(r => r.Ingredient)
            .Where(r => r.RestaurantId == restaurantId)
            .ToListAsync();

        var groupedRecipes = recipes.GroupBy(r => r.MenuItemId).ToDictionary(g => g.Key, g => g.ToList());

        return menuItems.Select(m =>
        {
            groupedRecipes.TryGetValue(m.Id, out var items);
            items ??= [];

            var recipeDtos = items.Select(r =>
            {
                var raw = UnitConverter.CalculateRawQuantity(r.UsableQuantity, r.YieldPercent);
                var unitCost = r.Ingredient.CurrentAverageCost;
                var cost = Math.Round(raw * unitCost, 4);
                return new RecipeItemDto(
                    r.Id,
                    r.IngredientId,
                    r.Ingredient.Name,
                    r.Ingredient.BaseUnit,
                    r.UsableQuantity,
                    r.YieldPercent,
                    raw,
                    unitCost,
                    cost
                );
            }).ToList();

            var totalCost = recipeDtos.Sum(i => i.CostPerServing);
            var isConfigured = recipeDtos.Count > 0;
            var grossProfit = m.Price - totalCost;
            var marginPercent = m.Price > 0 ? Math.Round((grossProfit / m.Price) * 100m, 2) : 0m;
            var hasIncompleteCost = !isConfigured || recipeDtos.Any(i => i.UnitCost <= 0);

            return new MenuItemRecipeDto(
                m.Id,
                m.Name,
                m.Category?.Name ?? "Uncategorized",
                m.Price,
                isConfigured,
                totalCost,
                grossProfit,
                marginPercent,
                hasIncompleteCost,
                recipeDtos
            );
        }).ToList();
    }

    public async Task<MenuItemRecipeDto> SetRecipeAsync(Guid restaurantId, Guid actorId, Guid menuItemId, SetRecipeDto dto)
    {
        var menuItem = await db.MenuItems.Include(m => m.Category)
            .SingleOrDefaultAsync(m => m.Id == menuItemId && m.RestaurantId == restaurantId && !m.IsDeleted)
            ?? throw new KeyNotFoundException("Menu item not found.");

        var existing = await db.RecipeItems.Where(r => r.RestaurantId == restaurantId && r.MenuItemId == menuItemId).ToListAsync();
        db.RecipeItems.RemoveRange(existing);

        var newItems = new List<RecipeItem>();
        foreach (var itemDto in dto.Items)
        {
            if (itemDto.UsableQuantity <= 0)
                throw new ArgumentException("Usable quantity must be greater than zero.");

            var ingredient = await db.Ingredients.AsNoTracking()
                .SingleOrDefaultAsync(i => i.Id == itemDto.IngredientId && i.RestaurantId == restaurantId && i.IsActive)
                ?? throw new ArgumentException($"Ingredient with ID '{itemDto.IngredientId}' not found or inactive.");

            var recipeItem = new RecipeItem
            {
                RestaurantId = restaurantId,
                MenuItemId = menuItemId,
                IngredientId = itemDto.IngredientId,
                UsableQuantity = itemDto.UsableQuantity,
                YieldPercent = itemDto.YieldPercent > 0 ? itemDto.YieldPercent : 100m
            };

            db.RecipeItems.Add(recipeItem);
            newItems.Add(recipeItem);
        }

        await db.SaveChangesAsync();

        var ingredientIds = newItems.Select(n => n.IngredientId).ToList();
        var ingredientMap = await db.Ingredients.AsNoTracking()
            .Where(i => ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id);

        var recipeDtos = newItems.Select(r =>
        {
            var ing = ingredientMap[r.IngredientId];
            var raw = UnitConverter.CalculateRawQuantity(r.UsableQuantity, r.YieldPercent);
            var unitCost = ing.CurrentAverageCost;
            var cost = Math.Round(raw * unitCost, 4);
            return new RecipeItemDto(
                r.Id,
                r.IngredientId,
                ing.Name,
                ing.BaseUnit,
                r.UsableQuantity,
                r.YieldPercent,
                raw,
                unitCost,
                cost
            );
        }).ToList();

        var totalCost = recipeDtos.Sum(i => i.CostPerServing);
        var isConfigured = recipeDtos.Count > 0;
        var grossProfit = menuItem.Price - totalCost;
        var marginPercent = menuItem.Price > 0 ? Math.Round((grossProfit / menuItem.Price) * 100m, 2) : 0m;
        var hasIncompleteCost = !isConfigured || recipeDtos.Any(i => i.UnitCost <= 0);

        return new MenuItemRecipeDto(
            menuItem.Id,
            menuItem.Name,
            menuItem.Category?.Name ?? "Uncategorized",
            menuItem.Price,
            isConfigured,
            totalCost,
            grossProfit,
            marginPercent,
            hasIncompleteCost,
            recipeDtos
        );
    }

    /// <summary>
    /// Deducts recipe ingredients for chargeable food when its bill is fully settled.
    /// Preserves exact raw quantity, unit cost snapshot, and extended cost.
    /// Also records SettledBillItemCost and SettledBillItemIngredientCost snapshots for immutable reporting.
    /// </summary>
    public async Task DeductForSettlementAsync(ApplicationDbContext dbContext, Guid restaurantId, Guid billId, Guid actorId, Guid operationId)
    {
        var alreadyDeducted = await dbContext.StockMovements.AnyAsync(m =>
            m.RestaurantId == restaurantId &&
            m.ReferenceType == "TableBill" &&
            m.ReferenceId == billId.ToString());

        if (alreadyDeducted)
            return;

        var bill = await dbContext.TableBills.Include(b => b.Orders).ThenInclude(o => o.OrderItems).ThenInclude(oi => oi.MenuItem).ThenInclude(m => m.Category)
            .SingleOrDefaultAsync(b => b.Id == billId && b.RestaurantId == restaurantId);

        if (bill == null)
            return;

        var billOrders = await dbContext.Orders
            .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem).ThenInclude(m => m.Category)
            .Where(o => o.TableBillId == billId && o.RestaurantId == restaurantId)
            .ToListAsync();

        // Line items to settle: MenuItemId, NameSnapshot, UnitPrice, Quantity, ModifiersJson, IsRefill
        var rawLineItems = new List<(Guid MenuItemId, string NameSnapshot, decimal UnitPrice, int Quantity, string? ModifiersJson, bool IsRefill)>();

        if (bill.ParentBillId == null)
        {
            var chargeableOrders = billOrders.Where(BillingService.Chargeable).ToList();
            foreach (var order in chargeableOrders)
            {
                foreach (var item in order.OrderItems)
                {
                    int activeQty = item.Quantity - item.CancelledQuantity;
                    if (!item.IsCancelled && activeQty > 0)
                    {
                        rawLineItems.Add((item.MenuItemId, item.ItemNameSnapshot ?? item.MenuItem?.Name ?? "Item", item.UnitPrice, activeQty, item.ModifiersJson, item.IsRefill));
                    }
                }
            }
        }
        else
        {
            // Split child bill
            if (!string.IsNullOrEmpty(bill.AllocationJson))
            {
                var allocation = JsonSerializer.Deserialize<BillDto>(bill.AllocationJson);
                var hasItemBreakdown = allocation?.Orders != null && allocation.Orders.Any(o => o.Items.Count > 0);

                if (hasItemBreakdown)
                {
                    foreach (var orderDto in allocation!.Orders)
                    {
                        foreach (var itemDto in orderDto.Items)
                        {
                            rawLineItems.Add((itemDto.MenuItemId, itemDto.Name ?? itemDto.ItemNameSnapshot ?? "Item", itemDto.UnitPrice, itemDto.Quantity, null, false));
                        }
                    }
                }
                else
                {
                    // Amount split: check if this settlement makes ALL sibling child bills settled
                    var allSiblings = await dbContext.TableBills
                        .Where(b => b.RestaurantId == restaurantId && b.ParentBillId == bill.ParentBillId)
                        .ToListAsync();

                    var otherUnsettled = allSiblings.Any(s => s.Id != billId && s.SettledAtUtc == null);
                    if (otherUnsettled)
                    {
                        // Not the final share yet
                        return;
                    }

                    var siblingIds = allSiblings.Select(s => s.Id.ToString()).ToList();
                    siblingIds.Add(bill.ParentBillId.Value.ToString());

                    var anySiblingDeducted = await dbContext.StockMovements.AnyAsync(m =>
                        m.RestaurantId == restaurantId &&
                        m.ReferenceType == "TableBill" &&
                        siblingIds.Contains(m.ReferenceId!));

                    if (anySiblingDeducted)
                        return;

                    // Consume original parent bill items
                    var parentBill = await dbContext.TableBills.Include(b => b.Orders).ThenInclude(o => o.OrderItems).ThenInclude(oi => oi.MenuItem).ThenInclude(m => m.Category)
                        .SingleOrDefaultAsync(b => b.Id == bill.ParentBillId.Value && b.RestaurantId == restaurantId);

                    if (parentBill != null)
                    {
                        var parentChargeable = parentBill.Orders.Where(BillingService.Chargeable).ToList();
                        foreach (var order in parentChargeable)
                        {
                            foreach (var item in order.OrderItems)
                            {
                                int activeQty = item.Quantity - item.CancelledQuantity;
                                if (!item.IsCancelled && activeQty > 0)
                                {
                                    rawLineItems.Add((item.MenuItemId, item.ItemNameSnapshot ?? item.MenuItem?.Name ?? "Item", item.UnitPrice, activeQty, item.ModifiersJson, item.IsRefill));
                                }
                            }
                        }
                    }
                }
            }
        }

        if (rawLineItems.Count == 0)
            return;

        var menuItemIds = rawLineItems.Select(g => g.MenuItemId).Distinct().ToList();
        var menuItems = await dbContext.MenuItems.Include(m => m.Category)
            .Where(m => menuItemIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        var resolver = new IngredientResolutionService(dbContext);
        var resolvedItems = await resolver.ResolveItemIngredientsAsync(
            restaurantId,
            rawLineItems.Select(r => (r.MenuItemId, r.NameSnapshot, r.Quantity, r.ModifiersJson, r.IsRefill)));

        var totalGrossRevenue = rawLineItems.Sum(g => g.UnitPrice * g.Quantity);

        var billDiscount = bill.Discount;
        var billTax = bill.Tax;

        decimal accumulatedDiscount = 0m;
        decimal accumulatedTax = 0m;

        // Costing method determination
        var restaurantSettings = await dbContext.RestaurantSettings.AsNoTracking().SingleOrDefaultAsync(s => s.RestaurantId == restaurantId);
        var costingMethod = restaurantSettings?.CostingMethod ?? "WeightedAverage";

        var activeLots = (costingMethod is "FIFO" or "LIFO")
            ? await dbContext.StockLots.Where(l => l.RestaurantId == restaurantId && l.Status == "Active" && l.AvailableQuantity > 0).ToListAsync()
            : new List<StockLot>();

        // Track aggregate ingredient deductions across this bill
        var ingredientDeductions = new Dictionary<Guid, (Ingredient Ingredient, decimal TotalRawQty, decimal ExtendedCost)>();

        var settledAtUtc = DateTime.UtcNow;

        for (int i = 0; i < resolvedItems.Count; i++)
        {
            var itemGroup = resolvedItems[i];
            var rawItem = rawLineItems[i];
            var isLast = i == resolvedItems.Count - 1;

            var menuItem = menuItems.GetValueOrDefault(itemGroup.MenuItemId);
            var unitPrice = rawItem.UnitPrice;
            var gross = unitPrice * itemGroup.Quantity;
            decimal itemDiscount;
            decimal itemTax;

            if (isLast)
            {
                itemDiscount = billDiscount - accumulatedDiscount;
                itemTax = billTax - accumulatedTax;
            }
            else
            {
                itemDiscount = totalGrossRevenue > 0 ? Math.Round(billDiscount * (gross / totalGrossRevenue), 2) : 0m;
                itemTax = totalGrossRevenue > 0 ? Math.Round(billTax * (gross / totalGrossRevenue), 2) : 0m;
                accumulatedDiscount += itemDiscount;
                accumulatedTax += itemTax;
            }

            var netRevenue = gross - itemDiscount;

            bool isComplete = itemGroup.Ingredients.Count > 0;
            string? incompleteReason = itemGroup.Ingredients.Count == 0 ? "No recipe configured" : null;

            decimal itemTotalCogs = 0m;
            var itemIngredientCosts = new List<SettledBillItemIngredientCost>();

            foreach (var ingReq in itemGroup.Ingredients)
            {
                var totalRaw = ingReq.RawQuantity;
                decimal unitCost;
                decimal extendedCost;

                if (costingMethod == "WeightedAverage" || activeLots.Count == 0)
                {
                    unitCost = ingReq.UnitCost;
                    extendedCost = Math.Round(totalRaw * unitCost, 4);
                }
                else
                {
                    var ingLots = activeLots.Where(l => l.IngredientId == ingReq.IngredientId).ToList();
                    ingLots = costingMethod == "FIFO"
                        ? ingLots.OrderBy(l => l.ReceivedOrProducedDateUtc).ThenBy(l => l.LotCode).ToList()
                        : ingLots.OrderByDescending(l => l.ReceivedOrProducedDateUtc).ThenByDescending(l => l.LotCode).ToList();

                    decimal remainingRaw = totalRaw;
                    decimal layerCost = 0m;

                    foreach (var lot in ingLots)
                    {
                        if (remainingRaw <= 0) break;
                        var takeQty = Math.Min(lot.AvailableQuantity, remainingRaw);
                        layerCost += takeQty * lot.UnitCost;
                        lot.AvailableQuantity -= takeQty;
                        if (lot.AvailableQuantity <= 0)
                        {
                            lot.AvailableQuantity = 0;
                            lot.Status = "Exhausted";
                        }
                        lot.UpdatedAtUtc = settledAtUtc;
                        remainingRaw -= takeQty;
                    }

                    if (remainingRaw > 0)
                    {
                        layerCost += remainingRaw * ingReq.UnitCost;
                    }

                    extendedCost = Math.Round(layerCost, 4);
                    unitCost = totalRaw > 0 ? Math.Round(extendedCost / totalRaw, 6) : ingReq.UnitCost;
                }

                if (unitCost <= 0)
                {
                    isComplete = false;
                    incompleteReason = $"Missing cost basis for {ingReq.IngredientName}";
                }

                itemTotalCogs += extendedCost;

                itemIngredientCosts.Add(new SettledBillItemIngredientCost
                {
                    IngredientId = ingReq.IngredientId,
                    IngredientNameSnapshot = ingReq.IngredientName,
                    BaseUnitSnapshot = ingReq.BaseUnit,
                    RawQuantityPerPortion = itemGroup.Quantity > 0 ? totalRaw / itemGroup.Quantity : totalRaw,
                    TotalRawQuantity = totalRaw,
                    UnitCostSnapshot = unitCost,
                    TotalCost = extendedCost
                });

                // Accumulate to total bill deduction
                if (ingredientDeductions.TryGetValue(ingReq.IngredientId, out var existing))
                {
                    ingredientDeductions[ingReq.IngredientId] = (
                        existing.Ingredient,
                        existing.TotalRawQty + totalRaw,
                        existing.ExtendedCost + extendedCost
                    );
                }
                else
                {
                    var ingEntity = await dbContext.Ingredients.FindAsync(ingReq.IngredientId);
                    if (ingEntity != null)
                    {
                        ingredientDeductions[ingReq.IngredientId] = (ingEntity, totalRaw, extendedCost);
                    }
                }
            }

            var grossProfit = netRevenue - itemTotalCogs;
            var marginPercent = netRevenue > 0 ? Math.Round((grossProfit / netRevenue) * 100m, 2) : 0m;

            var settledItemCost = new SettledBillItemCost
            {
                RestaurantId = restaurantId,
                TableBillId = billId,
                MenuItemId = itemGroup.MenuItemId,
                MenuItemNameSnapshot = menuItem?.Name ?? itemGroup.ItemName,
                CategoryNameSnapshot = menuItem?.Category?.Name ?? "Uncategorized",
                Quantity = itemGroup.Quantity,
                UnitPriceSnapshot = unitPrice,
                GrossRevenue = gross,
                AllocatedDiscount = itemDiscount,
                NetRevenue = netRevenue,
                AllocatedTax = itemTax,
                TotalIngredientCost = itemTotalCogs,
                GrossProfit = grossProfit,
                GrossMarginPercent = marginPercent,
                IsCostComplete = isComplete,
                IncompleteReason = incompleteReason,
                SettledAtUtc = settledAtUtc,
                IngredientCosts = itemIngredientCosts
            };

            dbContext.SettledBillItemCosts.Add(settledItemCost);
        }

        // Post stock movement deductions
        foreach (var (ingId, (ingredient, totalRaw, totalCost)) in ingredientDeductions)
        {
            var movement = new StockMovement
            {
                RestaurantId = restaurantId,
                IngredientId = ingId,
                ActorEmployeeId = actorId != Guid.Empty ? actorId : null,
                TimestampUtc = settledAtUtc,
                QuantityChange = -totalRaw,
                UnitSnapshot = ingredient.BaseUnit,
                Reason = "OrderDeduction",
                UnitCostSnapshot = ingredient.CurrentAverageCost,
                TotalCost = totalCost,
                ReferenceType = "TableBill",
                ReferenceId = billId.ToString(),
                OperationId = operationId != Guid.Empty ? operationId : null,
                Notes = $"Settlement deduction for Table {bill.TableNumberSnapshot}"
            };

            dbContext.StockMovements.Add(movement);
        }

        // Commit active reservations for this bill's orders
        var billOrderIds = billOrders.Select(o => o.Id).ToList();
        if (billOrderIds.Count > 0)
        {
            var activeReservations = await dbContext.StockReservations
                .Where(r => r.RestaurantId == restaurantId && billOrderIds.Contains(r.OrderId) && r.Status == "Active")
                .ToListAsync();

            foreach (var res in activeReservations)
            {
                res.Status = "Committed";
                res.CommittedAtUtc = settledAtUtc;
            }
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task<StocktakeDto> StartStocktakeAsync(Guid restaurantId, Guid actorId, Guid operationId, string? notes = null)
    {
        if (operationId != Guid.Empty)
        {
            var existing = await db.Stocktakes.Include(s => s.Items).ThenInclude(i => i.Ingredient).Include(s => s.ActorEmployee)
                .SingleOrDefaultAsync(s => s.RestaurantId == restaurantId && s.OperationId == operationId);
            if (existing != null)
                return ProjectStocktake(existing);
        }

        var istDate = ToIst(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var ingredients = await db.Ingredients.AsNoTracking()
            .Where(i => i.RestaurantId == restaurantId && i.IsActive)
            .OrderBy(i => i.Name)
            .ToListAsync();

        var balances = await db.StockMovements.AsNoTracking()
            .Where(m => m.RestaurantId == restaurantId)
            .GroupBy(m => m.IngredientId)
            .Select(g => new { IngredientId = g.Key, OnHand = g.Sum(m => m.QuantityChange) })
            .ToDictionaryAsync(x => x.IngredientId, x => x.OnHand);

        var stocktake = new Stocktake
        {
            RestaurantId = restaurantId,
            ActorEmployeeId = actorId != Guid.Empty ? actorId : null,
            BusinessDateIst = istDate,
            CountedAtUtc = DateTime.UtcNow,
            Status = "Draft",
            OperationId = operationId != Guid.Empty ? operationId : Guid.NewGuid(),
            Notes = notes
        };

        foreach (var ing in ingredients)
        {
            balances.TryGetValue(ing.Id, out var recorded);
            stocktake.Items.Add(new StocktakeItem
            {
                IngredientId = ing.Id,
                RecordedStockSnapshot = recorded,
                PhysicalCount = recorded,
                Variance = 0m
            });
        }

        db.Stocktakes.Add(stocktake);
        await db.SaveChangesAsync();

        return ProjectStocktake(stocktake);
    }

    public async Task<StocktakeDto> SaveStocktakeDraftAsync(Guid restaurantId, Guid actorId, Guid stocktakeId, SaveStocktakeDraftDto dto)
    {
        var stocktake = await db.Stocktakes.Include(s => s.Items).ThenInclude(i => i.Ingredient).Include(s => s.ActorEmployee)
            .SingleOrDefaultAsync(s => s.Id == stocktakeId && s.RestaurantId == restaurantId)
            ?? throw new KeyNotFoundException("Stocktake not found.");

        if (stocktake.Status == "Posted")
            throw new ServiceConflictException("Cannot edit a posted stocktake.");

        var countMap = dto.Counts.ToDictionary(c => c.IngredientId, c => c.PhysicalCount);

        foreach (var item in stocktake.Items)
        {
            if (countMap.TryGetValue(item.IngredientId, out var count))
            {
                item.PhysicalCount = count;
                item.Variance = count - item.RecordedStockSnapshot;
            }
        }

        if (dto.Notes != null)
            stocktake.Notes = dto.Notes;

        await db.SaveChangesAsync();
        return ProjectStocktake(stocktake);
    }

    public async Task<StocktakeDto> PostStocktakeAsync(Guid restaurantId, Guid actorId, Guid stocktakeId, PostStocktakeRequestDto dto)
    {
        var stocktake = await db.Stocktakes.Include(s => s.Items).ThenInclude(i => i.Ingredient).Include(s => s.ActorEmployee)
            .SingleOrDefaultAsync(s => s.Id == stocktakeId && s.RestaurantId == restaurantId)
            ?? throw new KeyNotFoundException("Stocktake not found.");

        if (stocktake.Status is "Completed" or "Posted")
            return ProjectStocktake(stocktake);

        if (dto.Counts != null && dto.Counts.Count > 0)
        {
            var countMap = dto.Counts.ToDictionary(c => c.IngredientId, c => c.PhysicalCount);
            foreach (var item in stocktake.Items)
            {
                if (countMap.TryGetValue(item.IngredientId, out var count))
                {
                    item.PhysicalCount = count;
                    item.Variance = count - item.RecordedStockSnapshot;
                }
            }
        }

        if (dto.Notes != null)
            stocktake.Notes = dto.Notes;

        var currentBalances = await db.StockMovements.AsNoTracking()
            .Where(m => m.RestaurantId == restaurantId)
            .GroupBy(m => m.IngredientId)
            .Select(g => new { IngredientId = g.Key, OnHand = g.Sum(m => m.QuantityChange) })
            .ToDictionaryAsync(x => x.IngredientId, x => x.OnHand);

        bool hasConflict = false;
        foreach (var item in stocktake.Items)
        {
            currentBalances.TryGetValue(item.IngredientId, out var currentStock);
            item.StockAtPosting = currentStock;

            if (currentStock != item.RecordedStockSnapshot)
            {
                hasConflict = true;
            }

            item.FinalAdjustment = item.Variance;
        }

        if (hasConflict && !dto.ConfirmReconciliation)
        {
            await db.SaveChangesAsync();
            throw new ServiceConflictException("Intervening stock movements detected since stocktake snapshot. Please confirm reconciliation to preserve intervening changes.");
        }

        var postedAt = DateTime.UtcNow;
        stocktake.Status = "Completed";
        stocktake.PostedAtUtc = postedAt;

        foreach (var item in stocktake.Items)
        {
            var adj = item.FinalAdjustment ?? item.Variance;
            if (adj != 0m)
            {
                var unitCost = item.Ingredient.CurrentAverageCost;
                var movement = new StockMovement
                {
                    RestaurantId = restaurantId,
                    IngredientId = item.IngredientId,
                    ActorEmployeeId = actorId != Guid.Empty ? actorId : null,
                    TimestampUtc = postedAt,
                    QuantityChange = adj,
                    UnitSnapshot = item.Ingredient.BaseUnit,
                    Reason = "StocktakeAdjustment",
                    UnitCostSnapshot = unitCost,
                    TotalCost = Math.Round(Math.Abs(adj) * unitCost, 4),
                    ReferenceType = "Stocktake",
                    ReferenceId = stocktake.Id.ToString(),
                    OperationId = dto.OperationId != Guid.Empty ? dto.OperationId : null,
                    Notes = $"Stocktake adjustment for IST date {stocktake.BusinessDateIst}"
                };
                db.StockMovements.Add(movement);
            }
        }

        await db.SaveChangesAsync();
        return ProjectStocktake(stocktake);
    }

    public async Task<List<StocktakeDto>> GetStocktakesAsync(Guid restaurantId)
    {
        var stocktakes = await db.Stocktakes.AsNoTracking()
            .Include(s => s.Items).ThenInclude(i => i.Ingredient)
            .Include(s => s.ActorEmployee)
            .Where(s => s.RestaurantId == restaurantId)
            .OrderByDescending(s => s.CountedAtUtc)
            .ToListAsync();

        return stocktakes.Select(ProjectStocktake).ToList();
    }

    private static StocktakeDto ProjectStocktake(Stocktake s)
    {
        var actorName = s.ActorEmployee != null ? $"{s.ActorEmployee.FirstName} {s.ActorEmployee.LastName}" : "System";
        var hasConflict = s.Items.Any(i => i.StockAtPosting.HasValue && i.StockAtPosting.Value != i.RecordedStockSnapshot);

        var itemDtos = s.Items.Select(i => new StocktakeItemDto(
            i.Id,
            i.IngredientId,
            i.Ingredient?.Name ?? "Unknown",
            i.Ingredient?.BaseUnit ?? "g",
            i.RecordedStockSnapshot,
            i.PhysicalCount,
            i.Variance,
            i.StockAtPosting,
            i.FinalAdjustment
        )).ToList();

        return new StocktakeDto(
            s.Id,
            s.BusinessDateIst,
            s.CountedAtUtc,
            s.PostedAtUtc,
            s.Status,
            s.OperationId,
            s.Notes,
            actorName,
            hasConflict,
            itemDtos
        );
    }
}
