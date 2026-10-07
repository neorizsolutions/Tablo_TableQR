using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Tablo.Api.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace Tablo.Api.Tests
{
    public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        public static bool UseBrokenDatabase { get; set; }

        public HealthEndpointTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    // Remove existing DbContext and its options
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    if (descriptor != null) services.Remove(descriptor);
                    
                    var contextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ApplicationDbContext));
                    if (contextDescriptor != null) services.Remove(contextDescriptor);

                    // Register a dynamic factory for ApplicationDbContext
                    services.AddScoped<ApplicationDbContext>(sp =>
                    {
                        if (UseBrokenDatabase)
                        {
                            // Return a real relational context pointing to a black hole to force CanConnectAsync to fail
                            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                                .UseNpgsql("Host=255.255.255.255;Database=invalid;Username=invalid;Password=invalid;Timeout=1")
                                .Options;
                            return new ApplicationDbContext(options);
                        }
                        else
                        {
                            // Return InMemory context for startup seeding and healthy tests
                            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                                .UseInMemoryDatabase("HealthEndpointTestDb")
                                .Options;
                            var ctx = new ApplicationDbContext(options);
                            ctx.Database.EnsureCreated();
                            return ctx;
                        }
                    });
                });
            });
        }

        [Fact]
        public async Task HealthEndpoint_ReturnsOk_WhenDatabaseIsReachable()
        {
            UseBrokenDatabase = false; // Ensure healthy state
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("\"status\":\"Healthy\"", content);
            Assert.Contains("\"service\":\"Tablo API\"", content);
        }

        [Fact]
        public async Task HealthEndpoint_Returns503_WhenDatabaseIsUnreachable()
        {
            // Start the application with a healthy InMemory database so DbSeeder passes
            UseBrokenDatabase = false;
            var client = _factory.CreateClient();
            
            // Switch to a broken relational connection for the /health request
            // This genuinely exercises the Database.CanConnectAsync() failure path on Npgsql
            UseBrokenDatabase = true;

            var response = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("\"status\":\"Unhealthy\"", content);
            
            // Ensure no connection details or exception messages leak
            Assert.DoesNotContain("Exception", content);
            Assert.DoesNotContain("Connection", content);
            Assert.DoesNotContain("Host=", content);
            Assert.DoesNotContain("255.255.255.255", content);
        }
    }
}
