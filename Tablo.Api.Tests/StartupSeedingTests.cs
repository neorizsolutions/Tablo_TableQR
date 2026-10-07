using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tablo.Api.Data;
using Xunit;
using System.Linq;
using System.Threading.Tasks;

namespace Tablo.Api.Tests
{
    public class StartupSeedingTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        static StartupSeedingTests()
        {
            System.Environment.SetEnvironmentVariable("Jwt__Key", "this_is_a_very_long_test_key_for_jwt_validation_12345!");
            System.Environment.SetEnvironmentVariable("AllowedOrigins__0", "http://localhost");
        }

        public StartupSeedingTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                // Ensure tests run in a non-production environment initially unless overridden
                builder.UseEnvironment("Development");

                builder.UseSetting("Jwt:Key", "this_is_a_very_long_test_key_for_jwt_validation_12345!");
                builder.UseSetting("AllowedOrigins:0", "http://localhost");

                var dbName = System.Guid.NewGuid().ToString();
                // Replace PostgreSQL with InMemory Database for isolated tests
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseInMemoryDatabase(dbName);
                    });
                    
                    // Create schema immediately so DbSeeder in Program.cs doesn't fail
                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    db.Database.EnsureCreated();
                });
            });
        }

        [Fact]
        public void Default_SeedDummyData_Is_False()
        {
            // Missing SeedDummyData config resolves to false
            var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Remove SeedDummyData explicitly
                });
            });

            // Run Startup which invokes seeding internally
            var client = factory.CreateClient();

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Assert demo seeding did not occur
            Assert.False(db.Restaurants.Any(), "Missing SeedDummyData should resolve to false, avoiding demo data creation.");
        }

        [Fact]
        public void Explicit_SeedDummyData_False_Disables_Demo_Seeding()
        {
            var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.Sources.Clear();
                    config.AddInMemoryCollection(new[] { 
                        new System.Collections.Generic.KeyValuePair<string, string?>("SeedDummyData", "false"),
                        new System.Collections.Generic.KeyValuePair<string, string?>("Jwt:Key", "this_is_a_very_long_test_key_for_jwt_validation_12345!"),
                        new System.Collections.Generic.KeyValuePair<string, string?>("AllowedOrigins:0", "http://localhost")
                    });
                });
            });

            var client = factory.CreateClient();

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.False(db.Restaurants.Any(), "Explicit SeedDummyData=false should disable demo seeding.");
        }

        [Fact]
        public void Production_Blocks_Demo_Seeding_Even_If_Explicitly_True()
        {
            var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.Sources.Clear();
                    config.AddInMemoryCollection(new[] { 
                        new System.Collections.Generic.KeyValuePair<string, string?>("SeedDummyData", "true"),
                        new System.Collections.Generic.KeyValuePair<string, string?>("Jwt:Key", "this_is_a_very_long_test_key_for_jwt_validation_12345!"),
                        new System.Collections.Generic.KeyValuePair<string, string?>("AllowedOrigins:0", "http://localhost")
                    });
                });
            });

            var client = factory.CreateClient();

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.False(db.Restaurants.Any(), "Production must block demo seeding unconditionally.");
        }

        [Fact]
        public void Migrated_Database_Initializes_Foundational_Roles()
        {
            // The default factory setup calls EnsureCreated() to simulate a migrated DB
            var client = _factory.CreateClient();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Assert foundational roles are seeded
            Assert.True(db.Roles.Any(r => r.Name == "Admin"), "Admin role should be seeded.");
            Assert.True(db.Roles.Any(r => r.Name == "Kitchen"), "Kitchen role should be seeded.");
            Assert.True(db.Roles.Any(r => r.Name == "Waiter"), "Waiter role should be seeded.");
        }

        [Fact]
        public void Initialization_Failure_Prevents_Startup()
        {
            var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    // Configure a deliberately broken connection to simulate initialization failure
                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseNpgsql("Host=255.255.255.255;Database=invalid;Username=invalid;Password=invalid;Timeout=1"));
                });
            });

            // The application should fail to start because DbSeeder throws on schema/connection failure,
            // and Program.cs logs and rethrows, crashing the process.
            Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        }
    }
}
