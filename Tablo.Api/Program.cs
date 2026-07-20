using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register DbContext with PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSettings["Key"] ?? "SuperSecretKeyThatIsAtLeast32BytesLong!");

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
builder.Services.AddScoped<Tablo.Api.Interfaces.IAuthService, Tablo.Api.Services.AuthService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// Add health endpoint
app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "Healthy",
        service = "Tablo API"
    });
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
        var context = services.GetRequiredService<Tablo.Api.Data.ApplicationDbContext>();
        context.Database.Migrate(); // Ensures db is created and migrated
        Tablo.Api.Data.DbSeeder.Seed(context);

        // Quick fix to ensure Table 11-20 exist for testing
        var restaurant = context.Restaurants.FirstOrDefault();
        if (restaurant != null)
        {
            var existingTables = context.RestaurantTables.Where(t => t.RestaurantId == restaurant.Id).Select(t => t.NameOrNumber).ToList();
            for (int i = 11; i <= 20; i++)
            {
                string tableName = $"Table {i}";
                if (!existingTables.Contains(tableName))
                {
                    context.RestaurantTables.Add(new Tablo.Api.Entities.RestaurantTable
                    {
                        Id = Guid.NewGuid(),
                        RestaurantId = restaurant.Id,
                        NameOrNumber = tableName,
                        QRToken = Guid.NewGuid().ToString("N")
                    });
                }
            }
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

app.Run();
