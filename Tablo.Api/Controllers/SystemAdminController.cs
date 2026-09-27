using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Controllers
{
    [ApiController]
    [Route("api/sysadmin")]
    public class SystemAdminController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public SystemAdminController(IAuthService authService, IConfiguration configuration, ApplicationDbContext context)
        {
            _authService = authService;
            _configuration = configuration;
            _context = context;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] SysAdminLoginDto request)
        {
            var configUsername = _configuration["SuperAdmin:Username"];
            var configPassword = _configuration["SuperAdmin:Password"];

            if (string.IsNullOrEmpty(configUsername) || string.IsNullOrEmpty(configPassword))
            {
                return StatusCode(500, new { message = "SuperAdmin credentials are not configured on the server." });
            }

            if (request.Username == configUsername && request.Password == configPassword)
            {
                var token = _authService.GenerateSuperAdminToken();
                return Ok(new
                {
                    Token = token,
                    User = new
                    {
                        Id = Guid.Empty,
                        FirstName = "Super",
                        LastName = "Admin",
                        Email = "superadmin",
                        Role = "SuperAdmin",
                        RestaurantId = Guid.Empty
                    }
                });
            }

            return Unauthorized(new { message = "Invalid Super Admin credentials." });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("onboard")]
        public async Task<IActionResult> Onboard([FromBody] SysAdminOnboardDto request)
        {
            // 1. Check if email already exists
            if (await _context.Employees.AnyAsync(e => e.Email == request.AdminEmail))
            {
                return BadRequest(new { message = "An account with this email already exists." });
            }

            // 2. Fetch Admin Role
            var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
            if (adminRole == null)
            {
                return StatusCode(500, new { message = "System Error: Admin role not found." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 3. Create Restaurant
                var restaurant = new Restaurant
                {
                    Id = Guid.NewGuid(),
                    Name = request.RestaurantName,
                    Description = "New Restaurant",
                    Address = "Not Provided",
                    ContactEmail = request.AdminEmail,
                    ContactPhone = "Not Provided"
                };
                _context.Restaurants.Add(restaurant);

                // 4. Create Admin Employee
                var adminEmployee = new Employee
                {
                    Id = Guid.NewGuid(),
                    RestaurantId = restaurant.Id,
                    RoleId = adminRole.Id,
                    FirstName = request.AdminFirstName,
                    LastName = request.AdminLastName,
                    Email = request.AdminEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
                    IsActive = true
                };
                _context.Employees.Add(adminEmployee);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Restaurant successfully created.",
                    restaurantId = restaurant.Id,
                    restaurantName = restaurant.Name
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "An error occurred while provisioning the tenant.", details = ex.Message });
            }
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("tenants")]
        public async Task<IActionResult> GetTenants()
        {
            var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
            if (adminRole == null) return StatusCode(500, new { message = "Admin role not found" });

            var tenants = await _context.Restaurants
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.ContactEmail,
                    CreatedAt = r.CreatedAt,
                    IsActive = _context.Employees
                        .Where(e => e.RestaurantId == r.Id && e.RoleId == adminRole.Id)
                        .Select(e => (bool?)e.IsActive)
                        .FirstOrDefault() ?? false,
                    ActiveUsers = _context.Employees
                        .Where(e => e.RestaurantId == r.Id && e.IsActive)
                        .Select(e => new {
                            e.FirstName,
                            e.LastName,
                            e.Email,
                            Role = e.Role.Name
                        }).ToList()
                })
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return Ok(tenants);
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPut("tenants/{restaurantId}/toggle-status")]
        public async Task<IActionResult> ToggleTenantStatus(Guid restaurantId)
        {
            var allEmployees = await _context.Employees.Where(e => e.RestaurantId == restaurantId).ToListAsync();
            
            if (!allEmployees.Any())
            {
                return NotFound(new { message = "No employees found for this tenant." });
            }

            var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
            var adminEmployee = allEmployees.FirstOrDefault(e => e.RoleId == adminRole?.Id) ?? allEmployees.First();
            
            bool newState = !adminEmployee.IsActive;
            
            foreach (var emp in allEmployees)
            {
                emp.IsActive = newState;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = $"Tenant status updated to {(newState ? "Active" : "Disabled")}.", isActive = newState });
        }
    }
}
