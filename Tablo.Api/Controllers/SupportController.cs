using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tablo.Api.DTOs;
using Tablo.Api.Extensions;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Controllers
{
    [ApiController]
    [Route("api/support")]
    [Authorize(Roles = "Admin,Owner")]
    public class SupportController(ISupportService supportService) : ControllerBase
    {
        private bool TryGetActor(out Guid restaurantId, out Guid employeeId)
        {
            restaurantId = User.GetRestaurantId();
            employeeId = User.GetUserId();
            return restaurantId != Guid.Empty && employeeId != Guid.Empty;
        }

        private string GetActorDisplayName()
        {
            return User.FindFirst("name")?.Value
                ?? User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst(ClaimTypes.Email)?.Value
                ?? "Restaurant Admin";
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations([FromQuery] DateTime? sinceUtc = null, CancellationToken cancellationToken = default)
        {
            if (!TryGetActor(out var restaurantId, out _))
                return Forbid();

            var result = await supportService.GetRestaurantConversationsAsync(restaurantId, sinceUtc, cancellationToken);
            return Ok(result);
        }

        [HttpGet("conversations/{id:guid}")]
        public async Task<IActionResult> GetConversation(Guid id, [FromQuery] long? afterSequence = null, CancellationToken cancellationToken = default)
        {
            if (!TryGetActor(out var restaurantId, out _))
                return Forbid();

            var detail = await supportService.GetConversationDetailAsync(id, restaurantId, afterSequence, cancellationToken);
            if (detail == null)
                return NotFound(new { message = "Support conversation not found." });

            return Ok(detail);
        }

        [HttpPost("conversations")]
        public async Task<IActionResult> CreateConversation([FromBody] CreateSupportConversationDto request, CancellationToken cancellationToken = default)
        {
            if (!TryGetActor(out var restaurantId, out var employeeId))
                return Forbid();

            try
            {
                var result = await supportService.CreateConversationAsync(restaurantId, employeeId, request, cancellationToken);
                if (result.Deduplicated)
                    return Ok(result);

                return CreatedAtAction(nameof(GetConversation), new { id = result.Conversation.Id }, result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
        }

        [HttpPost("conversations/{id:guid}/messages")]
        public async Task<IActionResult> Reply(Guid id, [FromBody] CreateSupportMessageDto request, CancellationToken cancellationToken = default)
        {
            if (!TryGetActor(out var restaurantId, out var employeeId))
                return Forbid();

            try
            {
                var result = await supportService.AddRestaurantReplyAsync(id, restaurantId, employeeId, request, cancellationToken);
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
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
        }

        [HttpPost("conversations/{id:guid}/reopen")]
        public async Task<IActionResult> Reopen(Guid id, [FromBody] ReopenSupportConversationDto? request, CancellationToken cancellationToken = default)
        {
            if (!TryGetActor(out var restaurantId, out var employeeId))
                return Forbid();

            try
            {
                var result = await supportService.ReopenConversationAsync(
                    id,
                    restaurantId,
                    employeeId,
                    request ?? new ReopenSupportConversationDto(),
                    cancellationToken);
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
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
        }

        [HttpPost("conversations/{id:guid}/resolve")]
        public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveSupportConversationDto? request, CancellationToken cancellationToken = default)
        {
            if (!TryGetActor(out var restaurantId, out _))
                return Forbid();

            var summary = await supportService.ResolveConversationAsync(
                id,
                restaurantId,
                "Restaurant",
                GetActorDisplayName(),
                request?.Note,
                cancellationToken);

            if (summary == null)
                return NotFound(new { message = "Support conversation not found." });

            return Ok(summary);
        }

        [HttpPost("conversations/{id:guid}/read")]
        public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken = default)
        {
            if (!TryGetActor(out var restaurantId, out _))
                return Forbid();

            var summary = await supportService.MarkRestaurantReadAsync(
                id,
                restaurantId,
                GetActorDisplayName(),
                cancellationToken);

            if (summary == null)
                return NotFound(new { message = "Support conversation not found." });

            return Ok(summary);
        }
    }
}
