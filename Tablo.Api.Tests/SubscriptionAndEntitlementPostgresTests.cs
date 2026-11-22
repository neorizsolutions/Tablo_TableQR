using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tablo.Api.Constants;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Xunit;

namespace Tablo.Api.Tests;

public class SubscriptionAndEntitlementPostgresTests : IAsyncLifetime
{
    private readonly string _adminConnStr;
    private readonly string _dbName;
    private readonly string _connStr;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public SubscriptionAndEntitlementPostgresTests()
    {
        _adminConnStr = Environment.GetEnvironmentVariable("TABLO_BILLING_TEST_CONNECTION")
            ?? "Host=127.0.0.1;Port=55439;Database=postgres;Username=tablo_test;Pooling=false";
        _dbName = $"tablo_sub_test_{Guid.NewGuid():N}";
        var cb = new NpgsqlConnectionStringBuilder(_adminConnStr) { Database = _dbName };
        _connStr = cb.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        await using (var conn = new NpgsqlConnection(_adminConnStr))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE \"{_dbName}\";";
            await cmd.ExecuteNonQueryAsync();
        }

        // Apply migrations and seed the fresh database BEFORE starting the test host
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_connStr).Options;
        await using (var db = new ApplicationDbContext(options))
        {
            await db.Database.MigrateAsync();
            DbSeeder.Seed(db, seedDummyData: true);
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", _connStr);
            builder.UseSetting("Jwt:Key", "service-desk-isolated-test-signing-key-not-for-production-2026");
            builder.UseSetting("Jwt:Issuer", "TabloApi");
            builder.UseSetting("Jwt:Audience", "TabloApp");
            builder.UseSetting("AllowedOrigins:0", "http://localhost");
            builder.UseSetting("SuperAdmin:Username", "superadmin@tablo.local");
            builder.UseSetting("SuperAdmin:Password", "SuperPassword123!");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(opts => opts.UseNpgsql(_connStr));
            });
        });

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory != null) await _factory.DisposeAsync();

        await using var conn = new NpgsqlConnection(_adminConnStr);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS \"{_dbName}\" WITH (FORCE);";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<string> LoginAsync(string email, string password = "password123")
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return doc.GetProperty("token").GetString()!;
    }

    private async Task<string> LoginSuperAdminAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/sysadmin/login", new { Username = "superadmin@tablo.local", Password = "SuperPassword123!" });
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return doc.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task EffectiveAccess_SeededRestaurant_HasActivePremiumSubscription()
    {
        var token = await LoginAsync("admin@tablodemo.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.GetAsync("/api/subscriptions/effective-access");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var access = await resp.Content.ReadFromJsonAsync<EffectiveAccessDto>();
        Assert.NotNull(access);
        Assert.Equal("Admin", access.Role);
        Assert.Equal("premium", access.Plan.Code);
        Assert.Equal("Active", access.Plan.Status);
        Assert.Contains(Capabilities.InventoryCore, access.Capabilities);
        Assert.Contains(Capabilities.StaffOrdering, access.Capabilities);
        Assert.Contains(Capabilities.QrOrdering, access.Capabilities);
        Assert.Contains(Capabilities.Purchasing, access.Capabilities);
    }

    [Fact]
    public async Task EffectiveAccess_Waiter_ExcludesInventoryEvenOnPremium()
    {
        var token = await LoginAsync("waiter@tablodemo.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.GetAsync("/api/subscriptions/effective-access");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var access = await resp.Content.ReadFromJsonAsync<EffectiveAccessDto>();
        Assert.NotNull(access);
        Assert.Equal("Waiter", access.Role);
        Assert.Contains(Capabilities.StaffOrdering, access.Capabilities);
        Assert.Contains(Capabilities.DiningGroups, access.Capabilities);
        Assert.Contains(Capabilities.SplitBilling, access.Capabilities);
        Assert.DoesNotContain(Capabilities.InventoryCore, access.Capabilities);
        Assert.DoesNotContain(Capabilities.Purchasing, access.Capabilities);
    }

    [Fact]
    public async Task EffectiveAccess_Kitchen_OnlyHasKitchenDisplayAndPrep()
    {
        var token = await LoginAsync("kitchen@tablodemo.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.GetAsync("/api/subscriptions/effective-access");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var access = await resp.Content.ReadFromJsonAsync<EffectiveAccessDto>();
        Assert.NotNull(access);
        Assert.Equal("Kitchen", access.Role);
        Assert.Contains(Capabilities.KitchenDisplay, access.Capabilities);
        Assert.Contains(Capabilities.Preparations, access.Capabilities);
        Assert.DoesNotContain(Capabilities.StandardBilling, access.Capabilities);
        Assert.DoesNotContain(Capabilities.StaffOrdering, access.Capabilities);
    }

    [Fact]
    public async Task PlanDowngrade_BasicPlan_BlocksInventoryAndStaffOrdering()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var restaurant = await db.Restaurants.FirstAsync();

        // 1. SuperAdmin downgrades restaurant to Basic
        var superToken = await LoginSuperAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superToken);

        var changeResp = await _client.PostAsJsonAsync($"/api/sysadmin/subscriptions/{restaurant.Id}/change-plan", new ChangeSubscriptionRequestDto
        {
            TargetPlanCode = "basic",
            Reason = "Customer requested downgrade to Basic plan"
        });
        Assert.Equal(HttpStatusCode.OK, changeResp.StatusCode);

        // 2. Admin logs in and checks effective access
        var adminToken = await LoginAsync("admin@tablodemo.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var accessResp = await _client.GetAsync("/api/subscriptions/effective-access");
        Assert.Equal(HttpStatusCode.OK, accessResp.StatusCode);
        var access = await accessResp.Content.ReadFromJsonAsync<EffectiveAccessDto>();
        Assert.NotNull(access);
        Assert.Equal("basic", access.Plan.Code);
        Assert.Contains(Capabilities.QrOrdering, access.Capabilities);
        Assert.Contains(Capabilities.KitchenDisplay, access.Capabilities);
        Assert.DoesNotContain(Capabilities.InventoryCore, access.Capabilities);
        Assert.DoesNotContain(Capabilities.StaffOrdering, access.Capabilities);
        Assert.DoesNotContain(Capabilities.Purchasing, access.Capabilities);

        // 3. Admin calls Inventory endpoint -> Must return 403 Forbidden with FEATURE_NOT_IN_PLAN
        var invResp = await _client.GetAsync($"/api/restaurants/{restaurant.Id}/inventory/ingredients");
        Assert.Equal(HttpStatusCode.Forbidden, invResp.StatusCode);
        var errorBody = await invResp.Content.ReadAsStringAsync();
        Assert.Contains("FEATURE_NOT_IN_PLAN", errorBody);
    }

    [Fact]
    public async Task PlanUpgrade_PreviewShowsGainedAndLostCapabilities()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var restaurant = await db.Restaurants.FirstAsync();

        var superToken = await LoginSuperAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superToken);

        // Set to basic first
        await _client.PostAsJsonAsync($"/api/sysadmin/subscriptions/{restaurant.Id}/change-plan", new ChangeSubscriptionRequestDto
        {
            TargetPlanCode = "basic",
            Reason = "Setup for preview test"
        });

        // Preview upgrade to Plus
        var previewResp = await _client.GetAsync($"/api/sysadmin/subscriptions/{restaurant.Id}/preview?targetPlanCode=plus");
        Assert.Equal(HttpStatusCode.OK, previewResp.StatusCode);

        var preview = await previewResp.Content.ReadFromJsonAsync<PlanChangePreviewDto>();
        Assert.NotNull(preview);
        Assert.Equal("basic", preview.FromPlanCode);
        Assert.Equal("plus", preview.ToPlanCode);
        Assert.Contains(Capabilities.StaffOrdering, preview.CapabilitiesGained);
        Assert.Contains(Capabilities.SplitBilling, preview.CapabilitiesGained);
        Assert.Empty(preview.CapabilitiesLost);
    }

    [Fact]
    public async Task SuperAdmin_AuditLog_RecordsEveryPlanChange()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var restaurant = await db.Restaurants.FirstAsync();

        var superToken = await LoginSuperAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superToken);

        var changeResp = await _client.PostAsJsonAsync($"/api/sysadmin/subscriptions/{restaurant.Id}/change-plan", new ChangeSubscriptionRequestDto
        {
            TargetPlanCode = "plus",
            Reason = "Customer seasonal upgrade"
        });
        Assert.Equal(HttpStatusCode.OK, changeResp.StatusCode);

        var auditResp = await _client.GetAsync($"/api/sysadmin/audit-logs?restaurantId={restaurant.Id}");
        Assert.Equal(HttpStatusCode.OK, auditResp.StatusCode);

        var logs = await auditResp.Content.ReadFromJsonAsync<List<SubscriptionAuditLogDto>>();
        Assert.NotNull(logs);
        Assert.NotEmpty(logs);
        var latest = logs.First();
        Assert.Equal("Customer seasonal upgrade", latest.Reason);
        Assert.Equal("plus", latest.ToPlanCode);
    }

    [Fact]
    public async Task FeatureDependencies_RejectsEnablingPurchasingWithoutInventory()
    {
        var invalidFeatures = new[] { Capabilities.Purchasing, Capabilities.QrOrdering };
        var (isValid, missing) = Capabilities.ValidateDependencies(invalidFeatures);

        Assert.False(isValid);
        Assert.Contains("inventory_core", missing);
    }
}
