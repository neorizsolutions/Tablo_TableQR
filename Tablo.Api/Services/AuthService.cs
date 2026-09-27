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
        private readonly IEmailService _emailService;

        public AuthService(ApplicationDbContext context, IConfiguration configuration, IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
        {
            var employee = await _context.Employees
                .Include(e => e.Role)
                .Include(e => e.Restaurant)
                .FirstOrDefaultAsync(e => e.Email == request.Email);

            if (employee == null)
            {
                throw new Exception("Invalid email or password.");
            }

            if (!employee.IsActive)
            {
                throw new Exception("Your account has been deactivated. Please contact your manager.");
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
                new Claim("RestaurantName", employee.Restaurant?.Name ?? "Tablo Restaurant"),
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

        public string GenerateSuperAdminToken()
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]!);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, "superadmin"),
                new Claim(JwtRegisteredClaimNames.Email, "superadmin"),
                // Notice: No RestaurantId claim is added for SuperAdmin
                new Claim(ClaimTypes.Role, "SuperAdmin")
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

        public async Task ChangePasswordAsync(Guid employeeId, string oldPassword, string newPassword)
        {
            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null)
            {
                throw new Exception("User not found.");
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(oldPassword, employee.PasswordHash);
            if (!isPasswordValid)
            {
                throw new Exception("Incorrect old password.");
            }

            employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();
        }

        public async Task ForgotPasswordAsync(string email)
        {
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == email);
            if (employee == null) return;

            // Generate 6-digit OTP
            var random = new Random();
            var otp = random.Next(100000, 999999).ToString();

            employee.ResetOtpHash = BCrypt.Net.BCrypt.HashPassword(otp);
            employee.ResetOtpExpiration = DateTime.UtcNow.AddMinutes(10);
            employee.ResetOtpAttempts = 0;
            employee.ResetTokenHash = null;

            await _context.SaveChangesAsync();

            string subject = "Tablo Password Reset Code";
            string body = $@"Hello,

We received a request to reset your Tablo account password.

Your verification code is:

{otp}

This code expires in 10 minutes.

If you did not request this, you can safely ignore this email.

Regards,
Tablo";

            await _emailService.SendEmailAsync(employee.Email, subject, body);
        }

        public async Task<VerifyOtpResponseDto> VerifyOtpAsync(string email, string otp)
        {
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == email);
            if (employee == null || employee.ResetOtpHash == null)
            {
                throw new Exception("Invalid OTP.");
            }

            if (employee.ResetOtpExpiration < DateTime.UtcNow)
            {
                throw new Exception("OTP has expired.");
            }

            if (employee.ResetOtpAttempts >= 5)
            {
                throw new Exception("Too many failed attempts. Please request a new OTP.");
            }

            if (!BCrypt.Net.BCrypt.Verify(otp, employee.ResetOtpHash))
            {
                employee.ResetOtpAttempts++;
                await _context.SaveChangesAsync();
                throw new Exception("Invalid OTP.");
            }

            // Generate reset token
            var resetToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            
            // Single use OTP - invalidate it now
            employee.ResetOtpHash = null;
            employee.ResetOtpExpiration = null;
            employee.ResetOtpAttempts = 0;
            
            // Set reset token hash
            employee.ResetTokenHash = BCrypt.Net.BCrypt.HashPassword(resetToken);
            await _context.SaveChangesAsync();

            return new VerifyOtpResponseDto { ResetToken = resetToken };
        }

        public async Task ResetPasswordAsync(string email, string resetToken, string newPassword)
        {
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == email);
            if (employee == null || employee.ResetTokenHash == null)
            {
                throw new Exception("Invalid or expired reset token.");
            }

            if (!BCrypt.Net.BCrypt.Verify(resetToken, employee.ResetTokenHash))
            {
                throw new Exception("Invalid or expired reset token.");
            }

            employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            employee.ResetTokenHash = null; // Invalidate reset token after use

            await _context.SaveChangesAsync();
        }
    }
}
