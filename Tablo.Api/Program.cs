using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register DbContext with PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
        if (allowedOrigins == null || allowedOrigins.Length == 0) throw new InvalidOperationException("AllowedOrigins configuration is missing or empty.");
        policy.WithOrigins(allowedOrigins)
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials(); // SignalR needs credentials
    });
});

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKeyString = jwtSettings["Key"];
if (string.IsNullOrEmpty(jwtKeyString))
{
    throw new InvalidOperationException("JWT Key is missing from configuration.");
}
var key = Encoding.ASCII.GetBytes(jwtKeyString);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddScoped<Tablo.Api.Interfaces.IMenuRepository, Tablo.Api.Repositories.MenuRepository>();
builder.Services.AddScoped<Tablo.Api.Interfaces.IMenuService, Tablo.Api.Services.MenuService>();
builder.Services.AddScoped<Tablo.Api.Interfaces.ITableRepository, Tablo.Api.Repositories.TableRepository>();
builder.Services.AddScoped<Tablo.Api.Interfaces.ITableService, Tablo.Api.Services.TableService>();
builder.Services.AddScoped<Tablo.Api.Interfaces.IQRService, Tablo.Api.Services.QRService>();
builder.Services.AddScoped<Tablo.Api.Interfaces.IOrderRepository, Tablo.Api.Repositories.OrderRepository>();
builder.Services.AddScoped<Tablo.Api.Interfaces.IOrderService, Tablo.Api.Services.OrderService>();
builder.Services.AddScoped<Tablo.Api.Services.BillingService>();
builder.Services.AddScoped<Tablo.Api.Services.PaymentReportService>();
builder.Services.AddScoped<Tablo.Api.Services.InventoryService>();
builder.Services.AddScoped<Tablo.Api.Services.InventoryReportService>();
builder.Services.AddScoped<Tablo.Api.Services.StaffOrderingService>();
builder.Services.AddScoped<Tablo.Api.Interfaces.IAuthService, Tablo.Api.Services.AuthService>();
builder.Services.AddScoped<Tablo.Api.Interfaces.IEmailService, Tablo.Api.Services.EmailService>();
builder.Services.AddScoped<Tablo.Api.Interfaces.IReportService, Tablo.Api.Services.ReportService>();
builder.Services.AddScoped<Tablo.Api.Interfaces.IServiceRequestService, Tablo.Api.Services.ServiceRequestService>();
builder.Services.AddScoped<Tablo.Api.Interfaces.ISettingsService, Tablo.Api.Services.SettingsService>();
builder.Services.AddScoped<Tablo.Api.Interfaces.ISubscriptionService, Tablo.Api.Services.SubscriptionService>();
builder.Services.AddScoped<Tablo.Api.Services.Printing.IKotRoutingService, Tablo.Api.Services.Printing.KotRoutingService>();
builder.Services.AddScoped<Tablo.Api.Interfaces.IParcelOrderService, Tablo.Api.Services.ParcelOrderService>();
builder.Services.AddScoped<Tablo.Api.Services.ModifierService>();
builder.Services.AddScoped<Tablo.Api.Services.ManagerAuthService>();
builder.Services.AddHostedService<Tablo.Api.Services.ScheduledOrderReleaseWorker>();

var imageStorageOptions = builder.Configuration.GetSection(Tablo.Api.Configurations.ImageStorageOptions.ImageStorage).Get<Tablo.Api.Configurations.ImageStorageOptions>() ?? new Tablo.Api.Configurations.ImageStorageOptions();
builder.Services.Configure<Tablo.Api.Configurations.ImageStorageOptions>(builder.Configuration.GetSection(Tablo.Api.Configurations.ImageStorageOptions.ImageStorage));

if (imageStorageOptions.Provider.Equals("S3", StringComparison.OrdinalIgnoreCase))
{
    var awsOptions = builder.Configuration.GetAWSOptions();
    if (!string.IsNullOrEmpty(imageStorageOptions.S3.Region))
    {
        awsOptions.Region = Amazon.RegionEndpoint.GetBySystemName(imageStorageOptions.S3.Region);
    }
    builder.Services.AddDefaultAWSOptions(awsOptions);
    builder.Services.AddAWSService<Amazon.S3.IAmazonS3>();
    builder.Services.AddScoped<Tablo.Api.Interfaces.IImageStorageService, Tablo.Api.Services.S3ImageStorageService>();
}
else
{
    builder.Services.AddScoped<Tablo.Api.Interfaces.IImageStorageService, Tablo.Api.Services.LocalImageStorageService>();
}
builder.Services.AddRateLimiter(options => {
    options.AddFixedWindowLimiter("PublicApi", opt => {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 30;
        opt.QueueLimit = 0;
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowFrontend");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets();

// Add health endpoint
app.MapGet("/health", async (Tablo.Api.Data.ApplicationDbContext context, ILogger<Program> logger) =>
{
    try
    {
        bool canConnect = await context.Database.CanConnectAsync();
        if (canConnect)
        {
            return Results.Ok(new
            {
                status = "Healthy",
                service = "Tablo API"
            });
        }
        else
        {
            logger.LogWarning("Health check failed: Database is unreachable.");
            return Results.Json(new { status = "Unhealthy", service = "Tablo API" }, statusCode: 503);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Health check failed due to a database connection exception.");
        return Results.Json(new { status = "Unhealthy", service = "Tablo API" }, statusCode: 503);
    }
})
.WithName("GetHealthStatus")
.WithOpenApi();

app.MapControllers();
app.MapHub<OrderHub>("/hubs/order");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var env = services.GetRequiredService<IWebHostEnvironment>();
        
        bool seedDummyData = configuration.GetValue<bool>("SeedDummyData", false);
        if (env.IsProduction())
        {
            seedDummyData = false; // Strictly prevent demo data in production
        }
        
        var context = services.GetRequiredService<Tablo.Api.Data.ApplicationDbContext>();
        bool autoMigrate = configuration.GetValue<bool>("AutoMigrate", true) || env.IsDevelopment();
        if (autoMigrate)
        {
            context.Database.Migrate();
        }
        
        Tablo.Api.Data.DbSeeder.Seed(context, seedDummyData);

        bool seedRealisticDemo = configuration.GetValue<bool>("SeedRealisticDemo", false) || args.Contains("--seed-demo");
        if (seedRealisticDemo && !env.IsProduction())
        {
            Tablo.Api.Data.LocalDemoSeeder.SeedRealisticDemoAsync(services).GetAwaiter().GetResult();
        }


    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "A critical error occurred initializing the database schema or foundational data. The application cannot start.");
        throw; // Fail-fast on startup
    }
}

app.Run();

public partial class Program { }

