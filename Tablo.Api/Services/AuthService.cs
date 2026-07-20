using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
        {
            var employee = await _context.Employees
                .Include(e => e.Role)
                .FirstOrDefaultAsync(e => e.Email == request.Email);

            if (employee == null)
            {
                throw new Exception("Invalid email or password.");
            }

            if (!employee.IsActive)
            {
                throw new Exception("Invalid email or password.");
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, employee.PasswordHash);
            
            if (!isPasswordValid)
            {
                throw new Exception("Invalid email or password.");
            }

            var token = GenerateJwtToken(employee);

            return new AuthResponseDto
            {
                Token = token,
                User = new EmployeeDto
                {
                    Id = employee.Id,
                    RestaurantId = employee.RestaurantId,
                    FirstName = employee.FirstName,
                    LastName = employee.LastName,
                    Email = employee.Email,
                    Role = employee.Role.Name,
                    IsActive = employee.IsActive
                }
            };
        }

        private string GenerateJwtToken(Entities.Employee employee)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]!);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, employee.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, employee.Email),
                new Claim("RestaurantId", employee.RestaurantId.ToString()),
                new Claim(ClaimTypes.Role, employee.Role.Name)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(24),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
    }
}
