using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Hubs;
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
        private readonly ISubscriptionService _subscriptionService;
        private readonly ISupportConnectionRevoker? _revoker;

        public SystemAdminController(
            IAuthService authService,
            IConfiguration configuration,
            ApplicationDbContext context,
            ISubscriptionService subscriptionService,
            ISupportConnectionRevoker? revoker = null)
        {
            _authService = authService;
            _configuration = configuration;
            _context = context;
            _subscriptionService = subscriptionService;
            _revoker = revoker;
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
                        Email = "superadmin@tablo.local",
                        Role = "SuperAdmin",
                        RestaurantId = Guid.Empty
                    }
                });
            }

            return Unauthorized(new { message = "Invalid Super Admin credentials." });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview()
        {
            var stats = await _subscriptionService.GetSuperAdminOverviewAsync();
            return Ok(stats);
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

            var targetPlanCode = !string.IsNullOrEmpty(request.PlanCode) ? request.PlanCode : "premium";
            var plan = await _context.Plans.FirstOrDefaultAsync(p => p.Code == targetPlanCode)
                       ?? await _context.Plans.FirstOrDefaultAsync(p => p.Code == "premium")
                       ?? await _context.Plans.FirstOrDefaultAsync();

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

                // 5. Create Default Subscription
                if (plan != null)
                {
                    var subscription = new Subscription
                    {
                        Id = Guid.NewGuid(),
                        RestaurantId = restaurant.Id,
                        PlanId = plan.Id,
                        Status = SubscriptionStatus.Active,
                        StartDateUtc = DateTime.UtcNow,
                        EndDateUtc = DateTime.UtcNow.AddYears(1),
                        AutoRenew = true,
                        Notes = "Onboarded by Super Admin"
                    };
                    _context.Subscriptions.Add(subscription);

                    _context.SubscriptionAuditLogs.Add(new SubscriptionAuditLog
                    {
                        Id = Guid.NewGuid(),
                        RestaurantId = restaurant.Id,
                        SubscriptionId = subscription.Id,
                        ActorEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "superadmin@tablo.local",
                        Action = "Created",
                        ToPlanId = plan.Id,
                        ToStatus = SubscriptionStatus.Active,
                        Reason = "New tenant onboarding",
                        TimestampUtc = DateTime.UtcNow
                    });
                }

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
                    PlanCode = r.Subscription != null ? r.Subscription.Plan.Code : "none",
                    PlanName = r.Subscription != null ? r.Subscription.Plan.Name : "No Plan",
                    SubscriptionStatus = r.Subscription != null ? r.Subscription.Status.ToString() : "None",
                    OutletsCount = r.Outlets.Count(),
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
                if (!newState)
                {
                    emp.SecurityStamp = Guid.NewGuid().ToString("N");
                }
            }

            await _context.SaveChangesAsync();

            if (!newState && _revoker != null)
            {
                await _revoker.RevokeRestaurantConnectionsAsync(restaurantId, "Tenant access suspended.");
            }

            return Ok(new { message = $"Tenant status updated to {(newState ? "Active" : "Disabled")}.", isActive = newState });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("plans")]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _subscriptionService.GetPlansAsync();
            return Ok(plans);
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPut("plans/{planId}")]
        public async Task<IActionResult> UpdatePlanFeatures(Guid planId, [FromBody] UpdatePlanFeaturesRequestDto request)
        {
            var actor = User.FindFirst(ClaimTypes.Email)?.Value ?? "superadmin@tablo.local";
            try
            {
                var updated = await _subscriptionService.UpdatePlanFeaturesAsync(planId, request, actor);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("subscriptions")]
        public async Task<IActionResult> GetSubscriptions()
        {
            var list = await _context.Subscriptions
                .Include(s => s.Plan)
                .Include(s => s.Restaurant)
                .OrderByDescending(s => s.UpdatedAtUtc)
                .Select(s => new SubscriptionDto
                {
                    Id = s.Id,
                    RestaurantId = s.RestaurantId,
                    RestaurantName = s.Restaurant.Name,
                    PlanId = s.PlanId,
                    PlanCode = s.Plan.Code,
                    PlanName = s.Plan.Name,
                    Status = s.Status.ToString(),
                    StartDateUtc = s.StartDateUtc,
                    EndDateUtc = s.EndDateUtc,
                    AutoRenew = s.AutoRenew,
                    Notes = s.Notes
                })
                .ToListAsync();

            return Ok(list);
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("subscriptions/{restaurantId}/change-plan")]
        public async Task<IActionResult> ChangeSubscription(Guid restaurantId, [FromBody] ChangeSubscriptionRequestDto request)
        {
            var actor = User.FindFirst(ClaimTypes.Email)?.Value ?? "superadmin@tablo.local";
            try
            {
                var result = await _subscriptionService.ChangeSubscriptionAsync(restaurantId, request, actor);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("subscriptions/{restaurantId}/preview")]
        public async Task<IActionResult> PreviewPlanChange(Guid restaurantId, [FromQuery] string targetPlanCode)
        {
            try
            {
                var preview = await _subscriptionService.PreviewPlanChangeAsync(restaurantId, targetPlanCode);
                return Ok(preview);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogs([FromQuery] Guid? restaurantId = null, [FromQuery] int limit = 100)
        {
            var logs = await _subscriptionService.GetAuditLogsAsync(restaurantId, limit);
            return Ok(logs);
        }
    }
}
