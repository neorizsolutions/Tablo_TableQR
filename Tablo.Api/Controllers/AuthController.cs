using Microsoft.AspNetCore.Mvc;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // In a real app we might return 401 specifically for auth failures
                return Unauthorized(new { message = ex.Message });
            }
        }
    }
}
