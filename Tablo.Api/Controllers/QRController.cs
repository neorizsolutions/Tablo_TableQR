using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QRController : ControllerBase
    {
        private readonly IQRService _qrService;

        public QRController(IQRService qrService)
        {
            _qrService = qrService;
        }

        [HttpGet("{qrToken}")]
        public async Task<ActionResult<QRResolutionDto>> ResolveQRToken(string qrToken)
        {
            var result = await _qrService.ResolveQRTokenAsync(qrToken);
            if (result == null)
            {
                return NotFound(new { message = "Invalid or inactive QR token." });
            }

            return Ok(result);
        }
    }
}
