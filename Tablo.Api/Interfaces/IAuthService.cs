using Tablo.Api.DTOs;

namespace Tablo.Api.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
        Task ChangePasswordAsync(Guid employeeId, string oldPassword, string newPassword);
        string GenerateSuperAdminToken();
        Task ForgotPasswordAsync(string email);
        Task<VerifyOtpResponseDto> VerifyOtpAsync(string email, string otp);
        Task ResetPasswordAsync(string email, string resetToken, string newPassword);
    }
}
