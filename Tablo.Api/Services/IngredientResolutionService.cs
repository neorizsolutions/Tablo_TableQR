using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;

namespace Tablo.Api.Services;

public class ResolvedIngredientRequirement
{
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string BaseUnit { get; set; } = string.Empty;
    public decimal RawQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal ExtendedCost => Math.Round(RawQuantity * UnitCost, 4);
}

public class ItemResolvedIngredients
{
    public Guid MenuItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool IsRefill { get; set; }
    public List<ResolvedIngredientRequirement> Ingredients { get; set; } = new();
    public decimal TotalCost => Ingredients.Sum(i => i.ExtendedCost);
}

public class IngredientResolutionService
{
    private readonly ApplicationDbContext _db;

    public IngredientResolutionService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Resolves ingredients on a per-item basis, taking into account base recipes
    /// and modifier adjustments (Add, Substitute, Remove).
    /// </summary>
    public async Task<List<ItemResolvedIngredients>> ResolveItemIngredientsAsync(
        Guid restaurantId,
        IEnumerable<(Guid MenuItemId, string ItemName, int Quantity, string? ModifiersJson, bool IsRefill)> items)
    {
        var itemList = items.Where(x => x.Quantity > 0).ToList();
        if (itemList.Count == 0) return new List<ItemResolvedIngredients>();

        var menuItemIds = itemList.Select(x => x.MenuItemId).Distinct().ToList();

        // 1. Fetch base recipe items
        var recipeItems = await _db.RecipeItems
            .Include(r => r.Ingredient)
            .Where(r => r.RestaurantId == restaurantId && menuItemIds.Contains(r.MenuItemId))
            .ToListAsync();

        // 2. Fetch modifier ingredients
        var modifierIngredientIds = new HashSet<Guid>();
        foreach (var item in itemList)
        {
            if (!string.IsNullOrWhiteSpace(item.ModifiersJson))
            {
                try
                {
                    var snapshots = JsonSerializer.Deserialize<List<SelectedModifierSnapshotDto>>(item.ModifiersJson);
                    if (snapshots != null)
                    {
                        foreach (var s in snapshots)
                        {
                            if (s.IngredientId.HasValue) modifierIngredientIds.Add(s.IngredientId.Value);
                            if (s.OriginalIngredientId.HasValue) modifierIngredientIds.Add(s.OriginalIngredientId.Value);
                        }
                    }
                }
                catch { }
            }
        }

        var allIngredientIds = recipeItems.Select(r => r.IngredientId).Concat(modifierIngredientIds).Distinct().ToList();
        var ingredientsMap = await _db.Ingredients
            .Where(i => i.RestaurantId == restaurantId && allIngredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id);

        // 3. Resolve item by item
        var result = new List<ItemResolvedIngredients>();

        foreach (var (menuItemId, itemName, quantity, modifiersJson, isRefill) in itemList)
        {
            var itemRecipes = recipeItems.Where(r => r.MenuItemId == menuItemId).ToList();

            var itemRawMap = new Dictionary<Guid, decimal>();
            foreach (var r in itemRecipes)
            {
                decimal rawPerPortion = UnitConverter.CalculateRawQuantity(r.UsableQuantity, r.YieldPercent);
                itemRawMap[r.IngredientId] = rawPerPortion * quantity;
            }

            // Modifiers
            if (!string.IsNullOrWhiteSpace(modifiersJson))
            {
                try
                {
                    var snapshots = JsonSerializer.Deserialize<List<SelectedModifierSnapshotDto>>(modifiersJson);
                    if (snapshots != null)
                    {
                        foreach (var snap in snapshots)
                        {
                            if (snap.IngredientAdjustmentType == "Add" && snap.IngredientId.HasValue && snap.IngredientQuantity > 0)
                            {
                                var addQty = snap.IngredientQuantity * quantity;
                                itemRawMap[snap.IngredientId.Value] = itemRawMap.GetValueOrDefault(snap.IngredientId.Value, 0m) + addQty;
                            }
                            else if (snap.IngredientAdjustmentType == "Substitute" && snap.IngredientId.HasValue)
                            {
                                decimal originalQty = 0m;
                                if (snap.OriginalIngredientId.HasValue && itemRawMap.TryGetValue(snap.OriginalIngredientId.Value, out var existingOriginal))
                                {
                                    originalQty = existingOriginal;
                                    itemRawMap.Remove(snap.OriginalIngredientId.Value);
                                }

                                decimal subPerPortion = snap.IngredientQuantity > 0
                                    ? snap.IngredientQuantity
                                    : (originalQty > 0 ? (originalQty / quantity) : 1m);

                                decimal subQty = subPerPortion * quantity;
                                itemRawMap[snap.IngredientId.Value] = itemRawMap.GetValueOrDefault(snap.IngredientId.Value, 0m) + subQty;
                            }
                            else if (snap.IngredientAdjustmentType == "Remove" && snap.OriginalIngredientId.HasValue)
                            {
                                itemRawMap.Remove(snap.OriginalIngredientId.Value);
                            }
                        }
                    }
                }
                catch { }
            }

            var itemResolved = new ItemResolvedIngredients
            {
                MenuItemId = menuItemId,
                ItemName = itemName,
                Quantity = quantity,
                IsRefill = isRefill,
                Ingredients = new List<ResolvedIngredientRequirement>()
            };

            foreach (var (ingId, rawQty) in itemRawMap)
            {
                if (rawQty > 0 && ingredientsMap.TryGetValue(ingId, out var ing))
                {
                    itemResolved.Ingredients.Add(new ResolvedIngredientRequirement
                    {
                        IngredientId = ingId,
                        IngredientName = ing.Name,
                        BaseUnit = ing.BaseUnit,
                        RawQuantity = rawQty,
                        UnitCost = ing.CurrentAverageCost
                    });
                }
            }

            result.Add(itemResolved);
        }

        return result;
    }

    /// <summary>
    /// Authoritative resolution of ingredients required for a collection of order items,
    /// taking into account base recipes and modifier adjustments (Add, Substitute, Remove).
    /// </summary>
    public async Task<List<ResolvedIngredientRequirement>> ResolveIngredientsAsync(
        Guid restaurantId,
        IEnumerable<(Guid MenuItemId, int Quantity, string? ModifiersJson, bool IsRefill)> items)
    {
        var itemResults = await ResolveItemIngredientsAsync(
            restaurantId,
            items.Select(x => (x.MenuItemId, string.Empty, x.Quantity, x.ModifiersJson, x.IsRefill)));

        var aggregated = new Dictionary<Guid, (string Name, string Unit, decimal Qty, decimal UnitCost)>();
        foreach (var item in itemResults)
        {
            foreach (var ing in item.Ingredients)
            {
                if (aggregated.TryGetValue(ing.IngredientId, out var existing))
                {
                    aggregated[ing.IngredientId] = (ing.IngredientName, ing.BaseUnit, existing.Qty + ing.RawQuantity, ing.UnitCost);
                }
                else
                {
                    aggregated[ing.IngredientId] = (ing.IngredientName, ing.BaseUnit, ing.RawQuantity, ing.UnitCost);
                }
            }
        }

        return aggregated.Select(kv => new ResolvedIngredientRequirement
        {
            IngredientId = kv.Key,
            IngredientName = kv.Value.Name,
            BaseUnit = kv.Value.Unit,
            RawQuantity = kv.Value.Qty,
            UnitCost = kv.Value.UnitCost
        }).ToList();
    }
}
