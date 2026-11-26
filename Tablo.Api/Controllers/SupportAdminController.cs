using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Controllers
{
    [ApiController]
    [Route("api/sysadmin/support")]
    [Authorize(Roles = "SuperAdmin")]
    public class SupportAdminController(ISupportService supportService) : ControllerBase
    {
        private bool IsValidPlatformIdentity()
        {
            var restaurantClaim = User.FindFirst("RestaurantId")?.Value;
            return string.IsNullOrWhiteSpace(restaurantClaim) ||
                   (Guid.TryParse(restaurantClaim, out var parsed) && parsed == Guid.Empty);
        }

        private (string Name, string Email) GetActor()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value ?? "superadmin@tablo.local";
            var name = User.FindFirst("name")?.Value ?? "Tablo Support";
            return (name, email);
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> GetInbox(
            [FromQuery] string? status = null,
            [FromQuery] string? search = null,
            [FromQuery] bool? escalatedOnly = null,
            [FromQuery] DateTime? sinceUtc = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsValidPlatformIdentity())
                return Forbid();

            var result = await supportService.GetSuperAdminInboxAsync(status, search, escalatedOnly, sinceUtc, cancellationToken);
            return Ok(result);
        }

        [HttpGet("conversations/{id:guid}")]
        public async Task<IActionResult> GetConversation(Guid id, [FromQuery] long? afterSequence = null, CancellationToken cancellationToken = default)
        {
            if (!IsValidPlatformIdentity())
                return Forbid();

            var detail = await supportService.GetConversationDetailAsync(id, null, afterSequence, cancellationToken);
            if (detail == null)
                return NotFound(new { message = "Support conversation not found." });

            return Ok(detail);
        }

        [HttpPost("conversations/{id:guid}/messages")]
        public async Task<IActionResult> Reply(Guid id, [FromBody] CreateSupportMessageDto request, CancellationToken cancellationToken = default)
        {
            if (!IsValidPlatformIdentity())
                return Forbid();

            var (name, email) = GetActor();
            try
            {
                var result = await supportService.AddSuperAdminReplyAsync(id, name, email, request, cancellationToken);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("conversations/{id:guid}/resolve")]
        public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveSupportConversationDto? request, CancellationToken cancellationToken = default)
        {
            if (!IsValidPlatformIdentity())
                return Forbid();

            var (name, email) = GetActor();
            var summary = await supportService.ResolveConversationAsync(
                id,
                null,
                "SuperAdmin",
                string.IsNullOrWhiteSpace(email) ? name : email,
                request?.Note,
                cancellationToken);

            if (summary == null)
                return NotFound(new { message = "Support conversation not found." });

            return Ok(summary);
        }

        [HttpPost("conversations/{id:guid}/read")]
        public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken = default)
        {
            if (!IsValidPlatformIdentity())
                return Forbid();

            var (name, email) = GetActor();
            var summary = await supportService.MarkSuperAdminReadAsync(id, email ?? name, cancellationToken);
            if (summary == null)
                return NotFound(new { message = "Support conversation not found." });

            return Ok(summary);
        }
    }
}
