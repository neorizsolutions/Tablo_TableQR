using Tablo.Api.DTOs;

namespace Tablo.Api.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
    }
}
