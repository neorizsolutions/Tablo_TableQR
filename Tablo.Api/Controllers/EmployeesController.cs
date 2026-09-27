using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Extensions;
using System.Text.RegularExpressions;

namespace Tablo.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class EmployeesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EmployeesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployees()
        {
            var restaurantId = User.GetRestaurantId();

            var employees = await _context.Employees
                .Include(e => e.Role)
                .Where(e => e.RestaurantId == restaurantId)
                .Select(e => new EmployeeDto
                {
                    Id = e.Id,
                    RestaurantId = e.RestaurantId,
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                    Email = e.Email,
                    Role = e.Role.Name,
                    IsActive = e.IsActive
                })
                .ToListAsync();

            return Ok(employees);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetEmployee(Guid id)
        {
            var restaurantId = User.GetRestaurantId();

            var employee = await _context.Employees
                .Include(e => e.Role)
                .FirstOrDefaultAsync(e => e.Id == id && e.RestaurantId == restaurantId);

            if (employee == null)
            {
                return NotFound();
            }

            return Ok(new EmployeeDto
            {
                Id = employee.Id,
                RestaurantId = employee.RestaurantId,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                Email = employee.Email,
                Role = employee.Role.Name,
                IsActive = employee.IsActive
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeDto request)
        {
            var restaurantId = User.GetRestaurantId();

            var allowedRoles = new[] { "Admin", "Kitchen", "Waiter" };
            if (!allowedRoles.Contains(request.RoleName))
            {
                return BadRequest("Invalid role. Allowed roles are: Admin, Kitchen, Waiter.");
            }

            // Check global email uniqueness
            if (await _context.Employees.AnyAsync(e => e.Email == request.Email))
            {
                return Conflict("Email is already in use.");
            }

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == request.RoleName);
            if (role == null)
            {
                return BadRequest("Role does not exist in the database.");
            }

            var newEmployee = new Employee
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurantId,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                RoleId = role.Id,
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            };

            _context.Employees.Add(newEmployee);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetEmployee), new { id = newEmployee.Id }, new EmployeeDto
            {
                Id = newEmployee.Id,
                RestaurantId = newEmployee.RestaurantId,
                FirstName = newEmployee.FirstName,
                LastName = newEmployee.LastName,
                Email = newEmployee.Email,
                Role = role.Name,
                IsActive = newEmployee.IsActive
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEmployee(Guid id, [FromBody] UpdateEmployeeDto request)
        {
            var restaurantId = User.GetRestaurantId();
            var userId = User.GetUserId();

            var employee = await _context.Employees
                .Include(e => e.Role)
                .FirstOrDefaultAsync(e => e.Id == id && e.RestaurantId == restaurantId);

            if (employee == null)
            {
                return NotFound();
            }

            var allowedRoles = new[] { "Admin", "Kitchen", "Waiter" };
            if (!allowedRoles.Contains(request.RoleName))
            {
                return BadRequest("Invalid role. Allowed roles are: Admin, Kitchen, Waiter.");
            }

            var newRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == request.RoleName);
            if (newRole == null)
            {
                return BadRequest("Role does not exist in the database.");
            }

            // Self-lockout protection
            if (id == userId && request.RoleName != "Admin" && employee.Role.Name == "Admin")
            {
                return BadRequest("You cannot demote yourself from Admin.");
            }

            // Last admin protection
            if (id != userId && employee.Role.Name == "Admin" && request.RoleName != "Admin")
            {
                var activeAdmins = await _context.Employees
                    .CountAsync(e => e.RestaurantId == restaurantId && e.IsActive && e.Role.Name == "Admin");

                if (activeAdmins <= 1)
                {
                    return BadRequest("You cannot demote the only remaining active Admin.");
                }
            }

            if (employee.Email != request.Email)
            {
                if (await _context.Employees.AnyAsync(e => e.Email == request.Email))
                {
                    return Conflict("Email is already in use.");
                }
                employee.Email = request.Email;
            }

            employee.FirstName = request.FirstName;
            employee.LastName = request.LastName;
            employee.RoleId = newRole.Id;

            await _context.SaveChangesAsync();

            return Ok(new EmployeeDto
            {
                Id = employee.Id,
                RestaurantId = employee.RestaurantId,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                Email = employee.Email,
                Role = newRole.Name,
                IsActive = employee.IsActive
            });
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateEmployeeStatusDto request)
        {
            var restaurantId = User.GetRestaurantId();
            var userId = User.GetUserId();

            var employee = await _context.Employees
                .Include(e => e.Role)
                .FirstOrDefaultAsync(e => e.Id == id && e.RestaurantId == restaurantId);

            if (employee == null)
            {
                return NotFound();
            }

            // Self-lockout protection
            if (id == userId && !request.IsActive)
            {
                return BadRequest("You cannot deactivate your own account.");
            }

            // Last admin protection
            if (!request.IsActive && employee.Role.Name == "Admin" && employee.IsActive)
            {
                var activeAdmins = await _context.Employees
                    .CountAsync(e => e.RestaurantId == restaurantId && e.IsActive && e.Role.Name == "Admin");

                if (activeAdmins <= 1)
                {
                    return BadRequest("You cannot deactivate the only remaining active Admin.");
                }
            }

            employee.IsActive = request.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new { message = request.IsActive ? "Employee activated." : "Employee deactivated." });
        }

        [HttpPatch("{id}/password")]
        public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordDto request)
        {
            var restaurantId = User.GetRestaurantId();

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == id && e.RestaurantId == restaurantId);

            if (employee == null)
            {
                return NotFound();
            }

            employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Password reset successfully." });
        }
    }
}
