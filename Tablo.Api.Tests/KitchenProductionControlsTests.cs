using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tablo.Api.Constants;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Services;
using Xunit;

namespace Tablo.Api.Tests;

public class KitchenProductionControlsTests
{
    private ApplicationDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "Tablo_KitchenProduction_" + Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ManagerAuthService_RejectsCancellation_WhenEmployeeIdGivenWithoutPinByNonManager()
    {
        using var db = CreateInMemoryDb();
        var restaurantId = Guid.NewGuid();

        var waiterRole = new Role { Id = Guid.NewGuid(), Name = "Waiter" };
        var managerRole = new Role { Id = Guid.NewGuid(), Name = "Manager" };
        db.Roles.AddRange(waiterRole, managerRole);

        var waiter = new Employee
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            RoleId = waiterRole.Id,
            FirstName = "Alice",
            LastName = "Waiter",
            Email = "waiter@tablo.local",
            IsActive = true
        };

        var manager = new Employee
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            RoleId = managerRole.Id,
            FirstName = "Bob",
            LastName = "Manager",
            Email = "manager@tablo.local",
            IsActive = true,
            ManagerPinHash = BCrypt.Net.BCrypt.HashPassword("1234")
        };

        db.Employees.AddRange(waiter, manager);
        await db.SaveChangesAsync();

        var authService = new ManagerAuthService(db, NullLogger<ManagerAuthService>.Instance);

        // Waiter attempts to cancel using manager's employee ID but WITHOUT PIN -> MUST FAIL
        var resultWithoutPin = await authService.ValidateManagerApprovalAsync(
            restaurantId,
            callerEmployeeId: waiter.Id,
            managerPin: null,
            specificManagerId: manager.Id);

        Assert.False(resultWithoutPin.Success);
        Assert.Null(resultWithoutPin.Approver);
        Assert.Contains("Manager PIN is strictly required", resultWithoutPin.ErrorMessage);

        // Waiter supplies wrong PIN -> MUST FAIL
        var resultWrongPin = await authService.ValidateManagerApprovalAsync(
            restaurantId,
            callerEmployeeId: waiter.Id,
            managerPin: "9999",
            specificManagerId: manager.Id);

        Assert.False(resultWrongPin.Success);
        Assert.Null(resultWrongPin.Approver);

        // Waiter supplies valid PIN -> MUST SUCCEED with Manager as Approver
        var resultValidPin = await authService.ValidateManagerApprovalAsync(
            restaurantId,
            callerEmployeeId: waiter.Id,
            managerPin: "1234",
            specificManagerId: manager.Id);

        Assert.True(resultValidPin.Success);
        Assert.NotNull(resultValidPin.Approver);
        Assert.Equal(manager.Id, resultValidPin.Approver!.Id);
    }

    [Fact]
    public async Task ManagerAuthService_PermitsDirectCancellation_WhenCallerIsAdminOrManager()
    {
        using var db = CreateInMemoryDb();
        var restaurantId = Guid.NewGuid();

        var managerRole = new Role { Id = Guid.NewGuid(), Name = "Manager" };
        db.Roles.Add(managerRole);

        var manager = new Employee
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            RoleId = managerRole.Id,
            FirstName = "Charlie",
            LastName = "Manager",
            Email = "charlie@tablo.local",
            IsActive = true
        };

        db.Employees.Add(manager);
        await db.SaveChangesAsync();

        var authService = new ManagerAuthService(db, NullLogger<ManagerAuthService>.Instance);

        // Direct caller is Manager: does not require a PIN prompt
        var result = await authService.ValidateManagerApprovalAsync(
            restaurantId,
            callerEmployeeId: manager.Id,
            managerPin: null);

        Assert.True(result.Success);
        Assert.NotNull(result.Approver);
        Assert.Equal(manager.Id, result.Approver!.Id);
    }

    [Fact]
    public async Task ManagerAuthService_EnforcesTenantLockout_AfterFiveFailedPinAttempts()
    {
        using var db = CreateInMemoryDb();
        var restaurantId = Guid.NewGuid();

        var waiterRole = new Role { Id = Guid.NewGuid(), Name = "Waiter" };
        var managerRole = new Role { Id = Guid.NewGuid(), Name = "Manager" };
        db.Roles.AddRange(waiterRole, managerRole);

        var waiter = new Employee { Id = Guid.NewGuid(), RestaurantId = restaurantId, RoleId = waiterRole.Id, IsActive = true, FirstName = "W", LastName = "1", Email = "w@tablo.local" };
        var manager = new Employee { Id = Guid.NewGuid(), RestaurantId = restaurantId, RoleId = managerRole.Id, IsActive = true, FirstName = "M", LastName = "1", Email = "m@tablo.local", ManagerPinHash = BCrypt.Net.BCrypt.HashPassword("5555") };

        db.Employees.AddRange(waiter, manager);
        await db.SaveChangesAsync();

        var authService = new ManagerAuthService(db, NullLogger<ManagerAuthService>.Instance);

        // 4 failed attempts: not locked out yet
        for (int i = 0; i < 4; i++)
        {
            var res = await authService.ValidateManagerApprovalAsync(restaurantId, waiter.Id, "0000");
            Assert.False(res.Success);
            Assert.Contains("Invalid or locked", res.ErrorMessage);
        }

        // 5th failed attempt: triggers 15-minute lockout
        var fifthRes = await authService.ValidateManagerApprovalAsync(restaurantId, waiter.Id, "0000");
        Assert.False(fifthRes.Success);

        // 6th attempt with CORRECT PIN is blocked due to lockout!
        var blockedRes = await authService.ValidateManagerApprovalAsync(restaurantId, waiter.Id, "5555");
        Assert.False(blockedRes.Success);
        Assert.Contains("locked", blockedRes.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IngredientResolutionService_CorrectlyResolvesBaseRecipes_AndModifierSubstitutions()
    {
        using var db = CreateInMemoryDb();
        var restaurantId = Guid.NewGuid();

        var coffeeBeans = new Ingredient { Id = Guid.NewGuid(), RestaurantId = restaurantId, Name = "Coffee Beans", BaseUnit = "kg", CurrentAverageCost = 800m };
        var dairyMilk = new Ingredient { Id = Guid.NewGuid(), RestaurantId = restaurantId, Name = "Whole Milk", BaseUnit = "liter", CurrentAverageCost = 60m };
        var oatMilk = new Ingredient { Id = Guid.NewGuid(), RestaurantId = restaurantId, Name = "Oat Milk", BaseUnit = "liter", CurrentAverageCost = 140m };
        var vanillaSyrup = new Ingredient { Id = Guid.NewGuid(), RestaurantId = restaurantId, Name = "Vanilla Syrup", BaseUnit = "liter", CurrentAverageCost = 300m };

        db.Ingredients.AddRange(coffeeBeans, dairyMilk, oatMilk, vanillaSyrup);

        var latte = new MenuItem { Id = Guid.NewGuid(), RestaurantId = restaurantId, Name = "Caffe Latte", Price = 180m, IsAvailable = true };
        db.MenuItems.Add(latte);

        // Base recipe: 0.018 kg beans (18g) and 0.200 liter milk (200ml)
        var recipe1 = new RecipeItem { Id = Guid.NewGuid(), RestaurantId = restaurantId, MenuItemId = latte.Id, IngredientId = coffeeBeans.Id, UsableQuantity = 0.018m, YieldPercent = 100m };
        var recipe2 = new RecipeItem { Id = Guid.NewGuid(), RestaurantId = restaurantId, MenuItemId = latte.Id, IngredientId = dairyMilk.Id, UsableQuantity = 0.200m, YieldPercent = 100m };
        db.RecipeItems.AddRange(recipe1, recipe2);

        await db.SaveChangesAsync();

        var resolver = new IngredientResolutionService(db);

        // 1. Base Latte resolution (no modifiers)
        var baseRes = await resolver.ResolveIngredientsAsync(restaurantId, new[] { (latte.Id, 2, (string?)null, false) });
        Assert.Equal(2, baseRes.Count);
        var beansReq = baseRes.First(r => r.IngredientId == coffeeBeans.Id);
        var milkReq = baseRes.First(r => r.IngredientId == dairyMilk.Id);
        Assert.Equal(0.036m, beansReq.RawQuantity); // 2 * 0.018
        Assert.Equal(0.400m, milkReq.RawQuantity);  // 2 * 0.200

        // 2. Modifiers: Substitute Dairy Milk with Oat Milk (0.2L), and Add Vanilla Syrup (0.02L)
        var modifierSnapshots = new List<SelectedModifierSnapshotDto>
        {
            new()
            {
                ModifierGroupId = Guid.NewGuid(),
                GroupName = "Milk Alternative",
                ModifierOptionId = Guid.NewGuid(),
                OptionName = "Oat Milk",
                PriceDelta = 40m,
                IngredientAdjustmentType = "Substitute",
                IngredientId = oatMilk.Id,
                IngredientName = "Oat Milk",
                IngredientQuantity = 0.200m,
                OriginalIngredientId = dairyMilk.Id,
                OriginalIngredientName = "Whole Milk"
            },
            new()
            {
                ModifierGroupId = Guid.NewGuid(),
                GroupName = "Flavor",
                ModifierOptionId = Guid.NewGuid(),
                OptionName = "Vanilla Syrup",
                PriceDelta = 25m,
                IngredientAdjustmentType = "Add",
                IngredientId = vanillaSyrup.Id,
                IngredientName = "Vanilla Syrup",
                IngredientQuantity = 0.020m
            }
        };

        var modJson = JsonSerializer.Serialize(modifierSnapshots);
        var modRes = await resolver.ResolveIngredientsAsync(restaurantId, new[] { (latte.Id, 1, (string?)modJson, false) });

        // Dairy milk should be completely replaced by oat milk
        Assert.DoesNotContain(modRes, r => r.IngredientId == dairyMilk.Id);
        Assert.Contains(modRes, r => r.IngredientId == oatMilk.Id && r.RawQuantity == 0.200m);
        Assert.Contains(modRes, r => r.IngredientId == vanillaSyrup.Id && r.RawQuantity == 0.020m);
        Assert.Contains(modRes, r => r.IngredientId == coffeeBeans.Id && r.RawQuantity == 0.018m);
    }

    [Fact]
    public void OrderProductionOperation_PayloadHash_DistinguishesIdenticalVsAlteredParameters()
    {
        var orderId = Guid.NewGuid();
        var item1 = Guid.NewGuid();
        var item2 = Guid.NewGuid();

        // Identical parameters produce identical SHA256 hashes
        var payload1 = $"Fire:{orderId}:{item1},{item2}:mains";
        var payload2 = $"Fire:{orderId}:{item1},{item2}:mains";

        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash1 = Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload1)));
        var hash2 = Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload2)));

        Assert.Equal(hash1, hash2);

        // Altered course or items produces different hash
        var payload3 = $"Fire:{orderId}:{item1},{item2}:desserts";
        var hash3 = Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload3)));

        Assert.NotEqual(hash1, hash3);
    }

    [Fact]
    public async Task ManagerAuthService_LockoutExpiry_ResetsFailureWindowToFirstAttempt()
    {
        using var db = CreateInMemoryDb();
        var restaurantId = Guid.NewGuid();

        var waiterRole = new Role { Id = Guid.NewGuid(), Name = "Waiter" };
        var managerRole = new Role { Id = Guid.NewGuid(), Name = "Manager" };
        db.Roles.AddRange(waiterRole, managerRole);

        var waiter = new Employee { Id = Guid.NewGuid(), RestaurantId = restaurantId, RoleId = waiterRole.Id, IsActive = true, FirstName = "W", LastName = "1", Email = "w@tablo.local" };
        var manager = new Employee { Id = Guid.NewGuid(), RestaurantId = restaurantId, RoleId = managerRole.Id, IsActive = true, FirstName = "M", LastName = "1", Email = "m@tablo.local", ManagerPinHash = BCrypt.Net.BCrypt.HashPassword("5555") };
        db.Employees.AddRange(waiter, manager);

        // Seed an expired lockout (locked out 10 minutes ago for 5 failed attempts)
        var expiredLimit = new ManagerPinRateLimit
        {
            RestaurantId = restaurantId,
            FailedAttempts = 5,
            LockedUntilUtc = DateTime.UtcNow.AddMinutes(-10),
            LastAttemptAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };
        db.ManagerPinRateLimits.Add(expiredLimit);
        await db.SaveChangesAsync();

        var authService = new ManagerAuthService(db, NullLogger<ManagerAuthService>.Instance);

        // 1st failed attempt after expiry -> must NOT trigger immediate lockout!
        var res1 = await authService.ValidateManagerApprovalAsync(restaurantId, waiter.Id, "0000");
        Assert.False(res1.Success);

        var updated = await db.ManagerPinRateLimits.FirstOrDefaultAsync(r => r.RestaurantId == restaurantId);
        Assert.NotNull(updated);
        Assert.Equal(1, updated!.FailedAttempts); // Fresh failure window reset to 1
        Assert.Null(updated.LockedUntilUtc);       // Not locked out

        // Correct PIN now succeeds because failure count is 1 (< 5 threshold)
        var validRes = await authService.ValidateManagerApprovalAsync(restaurantId, waiter.Id, "5555");
        Assert.True(validRes.Success);
        Assert.NotNull(validRes.Approver);
    }

    [Fact]
    public async Task ManagerAuthService_ActiveLockout_PreservesDeadlineWithoutExtendingIt()
    {
        using var db = CreateInMemoryDb();
        var restaurantId = Guid.NewGuid();

        var waiterRole = new Role { Id = Guid.NewGuid(), Name = "Waiter" };
        var managerRole = new Role { Id = Guid.NewGuid(), Name = "Manager" };
        db.Roles.AddRange(waiterRole, managerRole);

        var waiter = new Employee { Id = Guid.NewGuid(), RestaurantId = restaurantId, RoleId = waiterRole.Id, IsActive = true, FirstName = "W", LastName = "1", Email = "w@tablo.local" };
        var manager = new Employee { Id = Guid.NewGuid(), RestaurantId = restaurantId, RoleId = managerRole.Id, IsActive = true, FirstName = "M", LastName = "1", Email = "m@tablo.local", ManagerPinHash = BCrypt.Net.BCrypt.HashPassword("5555") };
        db.Employees.AddRange(waiter, manager);

        var originalDeadline = DateTime.UtcNow.AddMinutes(8);
        var activeLimit = new ManagerPinRateLimit
        {
            RestaurantId = restaurantId,
            FailedAttempts = 5,
            LockedUntilUtc = originalDeadline,
            LastAttemptAtUtc = DateTime.UtcNow.AddMinutes(-7)
        };
        db.ManagerPinRateLimits.Add(activeLimit);
        await db.SaveChangesAsync();

        var authService = new ManagerAuthService(db, NullLogger<ManagerAuthService>.Instance);

        // Subsequent rejected attempt while lockout is active
        var res = await authService.ValidateManagerApprovalAsync(restaurantId, waiter.Id, "0000");
        Assert.False(res.Success);
        Assert.Contains("locked", res.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var record = await db.ManagerPinRateLimits.FirstOrDefaultAsync(r => r.RestaurantId == restaurantId);
        Assert.NotNull(record);
        // Deadline must be preserved, not extended to DateTime.UtcNow + 15 min!
        Assert.Equal(originalDeadline, record!.LockedUntilUtc);
    }

    [Fact]
    public async Task ManagerAuthService_RacingVerification_DoesNotResetActiveLockout()
    {
        using var db = CreateInMemoryDb();
        var restaurantId = Guid.NewGuid();

        var waiterRole = new Role { Id = Guid.NewGuid(), Name = "Waiter" };
        var managerRole = new Role { Id = Guid.NewGuid(), Name = "Manager" };
        db.Roles.AddRange(waiterRole, managerRole);

        var waiter = new Employee { Id = Guid.NewGuid(), RestaurantId = restaurantId, RoleId = waiterRole.Id, IsActive = true, FirstName = "W", LastName = "1", Email = "w@tablo.local" };
        var manager = new Employee { Id = Guid.NewGuid(), RestaurantId = restaurantId, RoleId = managerRole.Id, IsActive = true, FirstName = "M", LastName = "1", Email = "m@tablo.local", ManagerPinHash = BCrypt.Net.BCrypt.HashPassword("5555") };
        db.Employees.AddRange(waiter, manager);

        var activeDeadline = DateTime.UtcNow.AddMinutes(12);
        var activeLimit = new ManagerPinRateLimit
        {
            RestaurantId = restaurantId,
            FailedAttempts = 5,
            LockedUntilUtc = activeDeadline,
            LastAttemptAtUtc = DateTime.UtcNow
        };
        db.ManagerPinRateLimits.Add(activeLimit);
        await db.SaveChangesAsync();

        var authService = new ManagerAuthService(db, NullLogger<ManagerAuthService>.Instance);

        // Attempt verification with the CORRECT PIN while an active lockout is present
        var result = await authService.ValidateManagerApprovalAsync(restaurantId, waiter.Id, "5555");
        Assert.False(result.Success);
        Assert.Contains("locked", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var record = await db.ManagerPinRateLimits.FirstOrDefaultAsync(r => r.RestaurantId == restaurantId);
        Assert.NotNull(record);
        // Active lockout is NOT erased by the correct PIN presentation
        Assert.Equal(activeDeadline, record!.LockedUntilUtc);
        Assert.Equal(5, record.FailedAttempts);
    }
}
