using Microsoft.EntityFrameworkCore;
using Tablo.Api.Constants;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services;

public class ModifierService
{
    private readonly ApplicationDbContext _db;
    private readonly ISubscriptionService _subscriptions;

    public ModifierService(ApplicationDbContext db, ISubscriptionService subscriptions)
    {
        _db = db;
        _subscriptions = subscriptions;
    }

    private async Task RequireCapabilityAsync(Guid restaurantId)
    {
        bool hasCap = await _subscriptions.HasCapabilityAsync(restaurantId, Capabilities.ItemModifiers);
        if (!hasCap)
        {
            throw new InvalidOperationException("Item modifiers capability is not enabled for this restaurant's plan.");
        }
    }

    public async Task<List<ModifierGroupDto>> GetGroupsAsync(Guid restaurantId)
    {
        var groups = await _db.ModifierGroups
            .Include(g => g.Options)
            .Where(g => g.RestaurantId == restaurantId && g.IsActive)
            .OrderBy(g => g.DisplayOrder)
            .ToListAsync();

        return groups.Select(MapGroupDto).ToList();
    }

    public async Task<ModifierGroupDto> CreateGroupAsync(Guid restaurantId, CreateModifierGroupDto dto)
    {
        await RequireCapabilityAsync(restaurantId);

        var group = new ModifierGroup
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            Name = dto.Name,
            MinSelections = dto.MinSelections,
            MaxSelections = dto.MaxSelections,
            DisplayOrder = dto.DisplayOrder,
            IsActive = dto.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (dto.Options != null && dto.Options.Count > 0)
        {
            foreach (var optDto in dto.Options)
            {
                group.Options.Add(new ModifierOption
                {
                    Id = Guid.NewGuid(),
                    ModifierGroupId = group.Id,
                    RestaurantId = restaurantId,
                    Name = optDto.Name,
                    PriceDelta = optDto.PriceDelta,
                    DisplayOrder = optDto.DisplayOrder,
                    IsAvailable = optDto.IsAvailable,
                    IngredientAdjustmentType = optDto.IngredientAdjustmentType ?? "None",
                    IngredientId = optDto.IngredientId,
                    IngredientQuantity = optDto.IngredientQuantity,
                    OriginalIngredientId = optDto.OriginalIngredientId
                });
            }
        }

        _db.ModifierGroups.Add(group);
        await _db.SaveChangesAsync();

        return MapGroupDto(group);
    }

    public async Task<ModifierGroupDto> UpdateGroupAsync(Guid restaurantId, Guid groupId, UpdateModifierGroupDto dto)
    {
        await RequireCapabilityAsync(restaurantId);

        var group = await _db.ModifierGroups
            .Include(g => g.Options)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.RestaurantId == restaurantId);

        if (group == null) throw new KeyNotFoundException("Modifier group not found.");

        group.Name = dto.Name;
        group.MinSelections = dto.MinSelections;
        group.MaxSelections = dto.MaxSelections;
        group.DisplayOrder = dto.DisplayOrder;
        group.IsActive = dto.IsActive;

        await _db.SaveChangesAsync();
        return MapGroupDto(group);
    }

    public async Task DeleteGroupAsync(Guid restaurantId, Guid groupId)
    {
        await RequireCapabilityAsync(restaurantId);

        var group = await _db.ModifierGroups.FirstOrDefaultAsync(g => g.Id == groupId && g.RestaurantId == restaurantId);
        if (group == null) return;

        group.IsActive = false; // Soft-delete to preserve historical orders
        await _db.SaveChangesAsync();
    }

    public async Task<ModifierOptionDto> AddOptionAsync(Guid restaurantId, Guid groupId, CreateModifierOptionDto dto)
    {
        await RequireCapabilityAsync(restaurantId);

        var group = await _db.ModifierGroups.FirstOrDefaultAsync(g => g.Id == groupId && g.RestaurantId == restaurantId);
        if (group == null) throw new KeyNotFoundException("Modifier group not found.");

        var option = new ModifierOption
        {
            Id = Guid.NewGuid(),
            ModifierGroupId = groupId,
            RestaurantId = restaurantId,
            Name = dto.Name,
            PriceDelta = dto.PriceDelta,
            DisplayOrder = dto.DisplayOrder,
            IsAvailable = dto.IsAvailable,
            IngredientAdjustmentType = dto.IngredientAdjustmentType ?? "None",
            IngredientId = dto.IngredientId,
            IngredientQuantity = dto.IngredientQuantity,
            OriginalIngredientId = dto.OriginalIngredientId
        };

        _db.ModifierOptions.Add(option);
        await _db.SaveChangesAsync();

        return MapOptionDto(option);
    }

    public async Task<ModifierOptionDto> UpdateOptionAsync(Guid restaurantId, Guid optionId, UpdateModifierOptionDto dto)
    {
        await RequireCapabilityAsync(restaurantId);

        var option = await _db.ModifierOptions.FirstOrDefaultAsync(o => o.Id == optionId && o.RestaurantId == restaurantId);
        if (option == null) throw new KeyNotFoundException("Modifier option not found.");

        option.Name = dto.Name;
        option.PriceDelta = dto.PriceDelta;
        option.DisplayOrder = dto.DisplayOrder;
        option.IsAvailable = dto.IsAvailable;
        option.IngredientAdjustmentType = dto.IngredientAdjustmentType ?? "None";
        option.IngredientId = dto.IngredientId;
        option.IngredientQuantity = dto.IngredientQuantity;
        option.OriginalIngredientId = dto.OriginalIngredientId;

        await _db.SaveChangesAsync();
        return MapOptionDto(option);
    }

    public async Task DeleteOptionAsync(Guid restaurantId, Guid optionId)
    {
        await RequireCapabilityAsync(restaurantId);

        var option = await _db.ModifierOptions.FirstOrDefaultAsync(o => o.Id == optionId && o.RestaurantId == restaurantId);
        if (option == null) return;

        option.IsAvailable = false;
        await _db.SaveChangesAsync();
    }

    public async Task AssignGroupsToMenuItemAsync(Guid restaurantId, Guid menuItemId, List<Guid> modifierGroupIds)
    {
        await RequireCapabilityAsync(restaurantId);

        var menuItem = await _db.MenuItems.FirstOrDefaultAsync(m => m.Id == menuItemId && m.RestaurantId == restaurantId);
        if (menuItem == null) throw new KeyNotFoundException("Menu item not found.");

        // Prevent cross-tenant option group references
        var validGroups = await _db.ModifierGroups
            .Where(g => g.RestaurantId == restaurantId && modifierGroupIds.Contains(g.Id) && g.IsActive)
            .ToListAsync();

        var existingLinks = await _db.MenuItemModifierGroups
            .Where(mg => mg.MenuItemId == menuItemId)
            .ToListAsync();

        _db.MenuItemModifierGroups.RemoveRange(existingLinks);

        int order = 0;
        foreach (var group in validGroups)
        {
            _db.MenuItemModifierGroups.Add(new MenuItemModifierGroup
            {
                MenuItemId = menuItemId,
                ModifierGroupId = group.Id,
                DisplayOrder = order++
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<ModifierGroupDto>> GetMenuItemGroupsAsync(Guid restaurantId, Guid menuItemId)
    {
        var links = await _db.MenuItemModifierGroups
            .Include(mg => mg.ModifierGroup)
                .ThenInclude(g => g.Options)
            .Where(mg => mg.MenuItem.RestaurantId == restaurantId && mg.MenuItemId == menuItemId && mg.ModifierGroup.IsActive)
            .OrderBy(mg => mg.DisplayOrder)
            .ToListAsync();

        return links.Select(l => MapGroupDto(l.ModifierGroup)).ToList();
    }

    private static ModifierGroupDto MapGroupDto(ModifierGroup g) => new()
    {
        Id = g.Id,
        Name = g.Name,
        MinSelections = g.MinSelections,
        MaxSelections = g.MaxSelections,
        DisplayOrder = g.DisplayOrder,
        IsActive = g.IsActive,
        Options = g.Options?.Where(o => o.IsAvailable).OrderBy(o => o.DisplayOrder).Select(MapOptionDto).ToList() ?? new()
    };

    private static ModifierOptionDto MapOptionDto(ModifierOption o) => new()
    {
        Id = o.Id,
        ModifierGroupId = o.ModifierGroupId,
        Name = o.Name,
        PriceDelta = o.PriceDelta,
        DisplayOrder = o.DisplayOrder,
        IsAvailable = o.IsAvailable,
        IngredientAdjustmentType = o.IngredientAdjustmentType,
        IngredientId = o.IngredientId,
        IngredientName = o.Ingredient?.Name,
        IngredientQuantity = o.IngredientQuantity,
        OriginalIngredientId = o.OriginalIngredientId,
        OriginalIngredientName = o.OriginalIngredient?.Name
    };
}
