using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Tablo.Api.Data;
using Tablo.Api.Entities;

namespace Tablo.Api.Tests;

public sealed class ServiceDeskTestHost : WebApplicationFactory<Program>
{
    private readonly DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase("ServiceDeskAuth_" + Guid.NewGuid()).Options;
    internal const string TestKey = "service-desk-isolated-test-signing-key-not-for-production-2026";
    public Guid Restaurant { get; } = Guid.NewGuid();
    public Guid Actor { get; } = Guid.NewGuid();
    public Guid InactiveActor { get; } = Guid.NewGuid();
    public Guid AdminActor { get; } = Guid.NewGuid();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Program reads the signing key before Build; supply it as host configuration,
        // not process-wide environment variables shared with other test factories.
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Jwt:Key"] = TestKey, ["Jwt:Issuer"] = "billing-tests", ["Jwt:Audience"] = "billing-tests",
            ["AllowedOrigins:0"] = "http://localhost"
        }));
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Jwt:Key"] = TestKey, ["Jwt:Issuer"] = "billing-tests", ["Jwt:Audience"] = "billing-tests",
                ["AllowedOrigins:0"] = "http://localhost", ["SeedDummyData"] = "false",
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Database=unused;Username=unused"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.AddScoped(_ => new ApplicationDbContext(options));
        });
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (await db.Restaurants.AnyAsync(r => r.Id == Restaurant)) return;
        var role = await db.Roles.SingleAsync(r => r.Name == "Waiter");
        var adminRole = await db.Roles.SingleAsync(r => r.Name == "Admin");
        db.Restaurants.Add(new Restaurant { Id = Restaurant, Name = "Auth test", ContactEmail = "auth@example.test" });
        db.RestaurantTables.Add(new RestaurantTable { Id = Guid.NewGuid(), RestaurantId = Restaurant, TableNumber = 1, QRToken = "auth-test" });
        db.Employees.AddRange(new Employee { Id = Actor, RestaurantId = Restaurant, RoleId = role.Id, IsActive = true, Email = "staff@example.test" },
            new Employee { Id = AdminActor, RestaurantId = Restaurant, RoleId = adminRole.Id, IsActive = true, Email = "admin@example.test" },
            new Employee { Id = InactiveActor, RestaurantId = Restaurant, RoleId = role.Id, IsActive = false, Email = "inactive@example.test" });
        await db.SaveChangesAsync();
    }

    public string Token(string role, Guid? tenant, Guid? actor = null)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, (actor ?? Actor).ToString()), new(ClaimTypes.Role, role) };
        if (tenant.HasValue) claims.Add(new Claim("RestaurantId", tenant.Value.ToString()));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("billing-tests", "billing-tests", claims,
            expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)), SecurityAlgorithms.HmacSha256)));
    }
}

public class ServiceDeskAuthorizationTests(ServiceDeskTestHost host) : IClassFixture<ServiceDeskTestHost>
{
    public static IEnumerable<object[]> EndpointAuthorizationCases()
    {
        foreach (var endpoint in new[] { "review", "open", "submit", "settle", "receipt" })
            foreach (var identity in new[] { "anonymous", "Kitchen", "Waiter", "foreign-tenant", "missing-tenant", "empty-tenant", "inactive" })
                yield return [endpoint, identity];
    }

    [Theory]
    [MemberData(nameof(EndpointAuthorizationCases))]
    public async Task EveryBillAndOrderEndpoint_EnforcesStaffTenantAndActor(string endpoint, string identity)
    {
        using var client = host.CreateClient();
        await host.SeedAsync();
        if (identity != "anonymous")
        {
            Guid? tenant = identity switch { "missing-tenant" => null, "empty-tenant" => Guid.Empty, _ => host.Restaurant };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                host.Token(identity == "Kitchen" ? "Kitchen" : "Waiter", tenant, identity == "inactive" ? host.InactiveActor : host.Actor));
        }
        var routeTenant = identity == "foreign-tenant" ? Guid.NewGuid() : host.Restaurant;
        var root = $"/api/restaurants/{routeTenant}/service-desk/";
        var missingResource = Guid.NewGuid();
        var response = endpoint switch
        {
            "review" => await client.GetAsync(root + $"tables/{missingResource}/bill"),
            "receipt" => await client.GetAsync(root + $"bills/{missingResource}/receipt"),
            "open" => await client.PostAsJsonAsync(root + $"tables/{missingResource}/bill/open", new { }),
            "submit" => await client.PostAsJsonAsync(root + "orders", new { operationId = Guid.NewGuid(), tableId = missingResource,
                items = new[] { new { menuItemId = Guid.NewGuid(), quantity = 1 } } }),
            _ => await client.PostAsJsonAsync(root + $"bills/{missingResource}/settle", new { operationId = Guid.NewGuid(),
                expectedRevision = new string('0', 64), expectedTotal = 0, paymentMethod = "Cash" })
        };
        var expected = identity == "anonymous" ? HttpStatusCode.Unauthorized : identity == "Waiter" ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden;
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("anonymous", 401)]
    [InlineData("Kitchen", 403)]
    [InlineData("Waiter", 200)]
    [InlineData("foreign-tenant", 403)]
    [InlineData("missing-tenant", 403)]
    [InlineData("empty-tenant", 403)]
    [InlineData("inactive", 403)]
    public async Task TableList_EnforcesAuthenticationRoleTenantAndActiveActor(string identity, int expected)
    {
        using var client = host.CreateClient();
        await host.SeedAsync();
        var validation = host.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("Bearer").TokenValidationParameters;
        Assert.Equal("billing-tests", validation.ValidIssuer);
        Assert.True(validation.IssuerSigningKey is SymmetricSecurityKey configuredKey && configuredKey.Key.SequenceEqual(Encoding.UTF8.GetBytes(ServiceDeskTestHost.TestKey)), "Test-host JWT signing key was not isolated from application configuration.");
        if (identity != "anonymous")
        {
            var role = identity == "Kitchen" ? "Kitchen" : "Waiter";
            Guid? tenant = identity switch { "missing-tenant" => null, "empty-tenant" => Guid.Empty, _ => host.Restaurant };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(role, tenant, identity == "inactive" ? host.InactiveActor : host.Actor));
        }
        var routeTenant = identity == "foreign-tenant" ? Guid.NewGuid() : host.Restaurant;
        var response = await client.GetAsync($"/api/restaurants/{routeTenant}/service-desk/tables");
        Assert.True(response.StatusCode == (HttpStatusCode)expected, $"Expected {expected}, got {(int)response.StatusCode}; challenge: {response.Headers.WwwAuthenticate}");
    }
}
