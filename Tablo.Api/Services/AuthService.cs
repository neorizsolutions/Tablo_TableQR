using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Hubs;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly ISupportConnectionRevoker? _revoker;

        public AuthService(
            ApplicationDbContext context,
            IConfiguration configuration,
            IEmailService emailService,
            ISupportConnectionRevoker? revoker = null)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
            _revoker = revoker;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
        {
            var employee = await _context.Employees
                .Include(e => e.Role)
                .Include(e => e.Restaurant)
                .FirstOrDefaultAsync(e => e.Email == request.Email && !e.IsDeleted);

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
                new Claim(JwtRegisteredClaimNames.Name, employee.FullName),
                new Claim("RestaurantId", employee.RestaurantId.ToString()),
                new Claim("RestaurantName", employee.Restaurant?.Name ?? "Tablo Restaurant"),
                new Claim(ClaimTypes.Role, employee.Role.Name)
            };

            if (!string.IsNullOrEmpty(employee.SecurityStamp))
            {
                claims.Add(new Claim("SecurityStamp", employee.SecurityStamp));
            }

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
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && !e.IsDeleted && e.IsActive);
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
            employee.SecurityStamp = Guid.NewGuid().ToString("N");
            await _context.SaveChangesAsync();
            if (_revoker != null)
            {
                await _revoker.RevokeEmployeeConnectionsAsync(employee.Id, "Session invalidated due to password change.");
            }
        }

        public async Task ForgotPasswordAsync(string email)
        {
            var employee = await _context.Employees
                .Include(e => e.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Email == email && !e.IsDeleted && e.IsActive);
            // Self-service password reset is strictly restricted to Restaurant Admin
            if (employee == null || employee.Role == null || employee.Role.Name != "Admin") return;

            var otp = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            var otpHash = BCrypt.Net.BCrypt.HashPassword(otp);
            var now = DateTime.UtcNow;
            var expiresAt = now.AddMinutes(10);
            var cooldownCutoff = expiresAt.AddMinutes(-1);
            var updated = await _context.Employees
                .Where(e => e.Id == employee.Id && !e.IsDeleted && e.IsActive &&
                    (e.ResetOtpExpiration == null || e.ResetOtpExpiration <= cooldownCutoff))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.ResetOtpHash, otpHash)
                    .SetProperty(e => e.ResetOtpExpiration, expiresAt)
                    .SetProperty(e => e.ResetOtpAttempts, 0)
                    .SetProperty(e => e.ResetTokenHash, (string?)null));
            if (updated == 0)
                return;

            string subject = "Tablo Password Reset Code";
            string body = $@"Hello,

We received a request to reset your Tablo account password.

Your verification code is:

{otp}

This code expires in 10 minutes.

If you did not request this, you can safely ignore this email.

Regards,
Tablo";

            try
            {
                await _emailService.SendEmailAsync(employee.Email, subject, body);
            }
            catch
            {
                await _context.Employees.Where(e => e.Id == employee.Id && e.ResetOtpHash == otpHash)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(e => e.ResetOtpHash, (string?)null)
                        .SetProperty(e => e.ResetOtpExpiration, (DateTime?)null));
                throw;
            }
        }

        public async Task<VerifyOtpResponseDto> VerifyOtpAsync(string email, string otp)
        {
            var employee = await _context.Employees
                .Include(e => e.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Email == email && !e.IsDeleted && e.IsActive);
            if (employee == null || employee.Role == null || employee.Role.Name != "Admin" || employee.ResetOtpHash == null)
            {
                throw new Exception("Invalid OTP.");
            }

            if (employee.ResetOtpExpiration is null || employee.ResetOtpExpiration < DateTime.UtcNow)
            {
                throw new Exception("OTP has expired.");
            }

            if (employee.ResetOtpAttempts >= 5)
            {
                throw new Exception("Too many failed attempts. Please request a new OTP.");
            }

            var now = DateTime.UtcNow;
            var challenge = _context.Employees.Where(e =>
                e.Id == employee.Id && !e.IsDeleted && e.IsActive && e.ResetOtpHash == employee.ResetOtpHash &&
                e.ResetOtpExpiration > now && e.ResetOtpAttempts < 5);
            if (!BCrypt.Net.BCrypt.Verify(otp, employee.ResetOtpHash))
            {
                await challenge.ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.ResetOtpAttempts, e => e.ResetOtpAttempts + 1));
                throw new Exception("Invalid OTP.");
            }

            // Generate reset token
            var resetToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            
            var resetTokenHash = BCrypt.Net.BCrypt.HashPassword(resetToken);
            var updated = await challenge.ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.ResetOtpHash, (string?)null)
                .SetProperty(e => e.ResetOtpExpiration, now.AddMinutes(10))
                .SetProperty(e => e.ResetOtpAttempts, 0)
                .SetProperty(e => e.ResetTokenHash, resetTokenHash));
            if (updated == 0)
                throw new Exception("Invalid OTP.");

            return new VerifyOtpResponseDto { ResetToken = resetToken };
        }

        public async Task ResetPasswordAsync(string email, string resetToken, string newPassword)
        {
            var employee = await _context.Employees
                .Include(e => e.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Email == email && !e.IsDeleted && e.IsActive);
            if (employee == null || employee.Role == null || employee.Role.Name != "Admin" || employee.ResetTokenHash == null ||
                employee.ResetOtpExpiration is null || employee.ResetOtpExpiration < DateTime.UtcNow)
            {
                throw new Exception("Invalid or expired reset token.");
            }

            if (!BCrypt.Net.BCrypt.Verify(resetToken, employee.ResetTokenHash))
            {
                throw new Exception("Invalid or expired reset token.");
            }

            var tokenHash = employee.ResetTokenHash;
            var now = DateTime.UtcNow;
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            var newSecurityStamp = Guid.NewGuid().ToString("N");
            var updated = await _context.Employees
                .Where(e => e.Id == employee.Id && !e.IsDeleted && e.IsActive &&
                    e.ResetTokenHash == tokenHash && e.ResetOtpExpiration > now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.PasswordHash, passwordHash)
                    .SetProperty(e => e.SecurityStamp, newSecurityStamp)
                    .SetProperty(e => e.ResetTokenHash, (string?)null)
                    .SetProperty(e => e.ResetOtpExpiration, (DateTime?)null));
            if (updated == 0)
                throw new Exception("Invalid or expired reset token.");

            if (_revoker != null)
            {
                await _revoker.RevokeEmployeeConnectionsAsync(employee.Id, "Session invalidated due to password reset.");
            }
        }
    }
}
