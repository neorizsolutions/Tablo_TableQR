using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Attributes;
using Tablo.Api.Constants;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Extensions;
using Tablo.Api.Interfaces;
using Tablo.Api.Services.Printing;

namespace Tablo.Api.Controllers;

[ApiController]
public class PrintersController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ISubscriptionService _subscriptions;
    private readonly IKotRoutingService _kotRouting;
    private readonly ILogger<PrintersController> _logger;

    public PrintersController(
        ApplicationDbContext db,
        ISubscriptionService subscriptions,
        IKotRoutingService kotRouting,
        ILogger<PrintersController> logger)
    {
        _db = db;
        _subscriptions = subscriptions;
        _kotRouting = kotRouting;
        _logger = logger;
    }

    [HttpGet("api/restaurants/{restaurantId:guid}/printers")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin,Kitchen,Waiter")]
    public async Task<IActionResult> GetPrinters(Guid restaurantId)
    {
        var printers = await _db.ThermalPrinters
            .Include(p => p.Stations)
            .Where(p => p.RestaurantId == restaurantId)
            .OrderBy(p => p.Name)
            .Select(p => new ThermalPrinterDto
            {
                Id = p.Id,
                RestaurantId = p.RestaurantId,
                OutletId = p.OutletId,
                Name = p.Name,
                ConnectionType = p.ConnectionType,
                IpAddress = p.IpAddress,
                Port = p.Port,
                LocalQueueName = p.LocalQueueName,
                PaperWidthMm = p.PaperWidthMm,
                CharactersPerLine = p.CharactersPerLine,
                Status = p.Status,
                LastSeenAtUtc = p.LastSeenAtUtc,
                LastStatusMessage = p.LastStatusMessage,
                IsActive = p.IsActive,
                AssignedStationNames = p.Stations.Select(s => s.Name).ToList(),
                CreatedAtUtc = p.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(printers);
    }

    [HttpPost("api/restaurants/{restaurantId:guid}/printers")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin")]
    [RequireCapability(Capabilities.ThermalPrinting)]
    public async Task<IActionResult> RegisterPrinter(Guid restaurantId, [FromBody] CreateThermalPrinterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new { message = "Printer name is required." });
        }

        int charsPerLine = dto.PaperWidthMm == 58 ? 32 : (dto.CharactersPerLine > 0 ? dto.CharactersPerLine : 48);

        var printer = new ThermalPrinter
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            Name = dto.Name.Trim(),
            ConnectionType = dto.ConnectionType,
            IpAddress = dto.IpAddress?.Trim(),
            Port = dto.Port > 0 ? dto.Port : 9100,
            LocalQueueName = dto.LocalQueueName?.Trim(),
            PaperWidthMm = dto.PaperWidthMm == 58 ? 58 : 80,
            CharactersPerLine = charsPerLine,
            Status = "Unknown",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.ThermalPrinters.Add(printer);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPrinters), new { restaurantId }, new ThermalPrinterDto
        {
            Id = printer.Id,
            RestaurantId = printer.RestaurantId,
            Name = printer.Name,
            ConnectionType = printer.ConnectionType,
            IpAddress = printer.IpAddress,
            Port = printer.Port,
            LocalQueueName = printer.LocalQueueName,
            PaperWidthMm = printer.PaperWidthMm,
            CharactersPerLine = printer.CharactersPerLine,
            Status = printer.Status,
            IsActive = printer.IsActive,
            CreatedAtUtc = printer.CreatedAtUtc
        });
    }

    [HttpPut("api/restaurants/{restaurantId:guid}/printers/{printerId:guid}")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin")]
    [RequireCapability(Capabilities.ThermalPrinting)]
    public async Task<IActionResult> UpdatePrinter(Guid restaurantId, Guid printerId, [FromBody] UpdateThermalPrinterDto dto)
    {
        var printer = await _db.ThermalPrinters.FirstOrDefaultAsync(p => p.Id == printerId && p.RestaurantId == restaurantId);
        if (printer == null)
        {
            return NotFound(new { message = "Printer not found." });
        }

        printer.Name = dto.Name.Trim();
        printer.ConnectionType = dto.ConnectionType;
        printer.IpAddress = dto.IpAddress?.Trim();
        printer.Port = dto.Port > 0 ? dto.Port : 9100;
        printer.LocalQueueName = dto.LocalQueueName?.Trim();
        printer.PaperWidthMm = dto.PaperWidthMm == 58 ? 58 : 80;
        printer.CharactersPerLine = dto.PaperWidthMm == 58 ? 32 : (dto.CharactersPerLine > 0 ? dto.CharactersPerLine : 48);
        printer.IsActive = dto.IsActive;
        printer.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new ThermalPrinterDto
        {
            Id = printer.Id,
            RestaurantId = printer.RestaurantId,
            Name = printer.Name,
            ConnectionType = printer.ConnectionType,
            IpAddress = printer.IpAddress,
            Port = printer.Port,
            LocalQueueName = printer.LocalQueueName,
            PaperWidthMm = printer.PaperWidthMm,
            CharactersPerLine = printer.CharactersPerLine,
            Status = printer.Status,
            IsActive = printer.IsActive,
            CreatedAtUtc = printer.CreatedAtUtc
        });
    }

    [HttpDelete("api/restaurants/{restaurantId:guid}/printers/{printerId:guid}")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin")]
    [RequireCapability(Capabilities.ThermalPrinting)]
    public async Task<IActionResult> DeletePrinter(Guid restaurantId, Guid printerId)
    {
        var printer = await _db.ThermalPrinters.FirstOrDefaultAsync(p => p.Id == printerId && p.RestaurantId == restaurantId);
        if (printer == null)
        {
            return NotFound(new { message = "Printer not found." });
        }

        printer.IsActive = false;
        printer.UpdatedAtUtc = DateTime.UtcNow;

        // Unlink stations that used this printer
        var stations = await _db.KitchenStations.Where(s => s.RestaurantId == restaurantId && s.PrinterId == printerId).ToListAsync();
        foreach (var s in stations) s.PrinterId = null;

        await _db.SaveChangesAsync();

        return Ok(new { message = $"Printer '{printer.Name}' deactivated." });
    }

    [HttpPost("api/restaurants/{restaurantId:guid}/printers/{printerId:guid}/test-print")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin")]
    [RequireCapability(Capabilities.ThermalPrinting)]
    public async Task<IActionResult> DispatchTestPrint(Guid restaurantId, Guid printerId)
    {
        var printer = await _db.ThermalPrinters.FirstOrDefaultAsync(p => p.Id == printerId && p.RestaurantId == restaurantId);
        if (printer == null)
        {
            return NotFound(new { message = "Printer not found." });
        }

        var restaurant = await _db.Restaurants.FindAsync(restaurantId);
        string restName = restaurant?.Name ?? "TABLO Restaurant";

        var (bytes, readableText) = EscPosFormatter.FormatTestPrint(restName, printer.Name, printer.PaperWidthMm);

        var job = new PrintJob
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            KotTicketId = null,
            PrinterId = printer.Id,
            StationId = null,
            JobType = "TestPrint",
            Status = "Pending",
            PayloadEscPosBase64 = Convert.ToBase64String(bytes),
            ReadableText = readableText,
            PaperWidthMm = printer.PaperWidthMm,
            Attempts = 0,
            MaxAttempts = 3,
            CreatedBy = User.GetUserId().ToString(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.PrintJobs.Add(job);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            jobId = job.Id,
            message = $"Test print job queued for {printer.Name}.",
            readablePreview = readableText
        });
    }

    [HttpGet("api/restaurants/{restaurantId:guid}/printers/jobs")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin,Kitchen,Waiter")]
    public async Task<IActionResult> GetPrintJobs(
        Guid restaurantId,
        [FromQuery] Guid? stationId = null,
        [FromQuery] Guid? printerId = null,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50)
    {
        var query = _db.PrintJobs
            .Include(j => j.Printer)
            .Include(j => j.Station)
            .Include(j => j.KotTicket)
            .Where(j => j.RestaurantId == restaurantId)
            .AsQueryable();

        if (stationId.HasValue) query = query.Where(j => j.StationId == stationId.Value);
        if (printerId.HasValue) query = query.Where(j => j.PrinterId == printerId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(j => j.Status == status);

        var jobs = await query
            .OrderByDescending(j => j.CreatedAtUtc)
            .Take(limit)
            .Select(j => new PrintJobDto
            {
                Id = j.Id,
                KotTicketId = j.KotTicketId,
                TicketNumber = j.KotTicket != null ? j.KotTicket.TicketNumber : null,
                PrinterId = j.PrinterId,
                PrinterName = j.Printer.Name,
                StationId = j.StationId,
                StationName = j.Station != null ? j.Station.Name : null,
                JobType = j.JobType,
                Status = j.Status,
                DeliveryState = j.DeliveryState,
                UncertainReason = j.UncertainReason,
                PaperWidthMm = j.PaperWidthMm,
                Attempts = j.Attempts,
                LastAttemptUtc = j.LastAttemptUtc,
                LastError = j.LastError,
                CreatedBy = j.CreatedBy,
                CreatedAtUtc = j.CreatedAtUtc,
                CompletedAtUtc = j.CompletedAtUtc,
                ReadableText = j.ReadableText
            })
            .ToListAsync();

        return Ok(jobs);
    }

    [HttpPost("api/restaurants/{restaurantId:guid}/printers/jobs/{jobId:guid}/retry")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin,Kitchen,Waiter")]
    [RequireCapability(Capabilities.ThermalPrinting)]
    public async Task<IActionResult> RetryJob(Guid restaurantId, Guid jobId)
    {
        var job = await _db.PrintJobs.FirstOrDefaultAsync(j => j.Id == jobId && j.RestaurantId == restaurantId);
        if (job == null)
        {
            return NotFound(new { message = "Print job not found." });
        }

        // 1. Idempotency: if already pending, return ok
        if (job.Status == "Pending")
        {
            return Ok(new { message = "Print job is already pending delivery.", jobId = job.Id });
        }

        // 2. Reject retry if delivery has already completed
        if (job.Status == "Completed" || job.Status == "Printed")
        {
            return Conflict(new { message = "Job has already been printed. Use the reprint endpoint to generate an audited duplicate ticket." });
        }

        // 3. Reject retry if delivery is uncertain (bytes may have reached printer)
        if (job.Status == "UncertainDelivery" || job.DeliveryState == "Uncertain")
        {
            return Conflict(new { message = "Delivery cannot be verified because data transmission was interrupted or lease expired. An authorized audited reprint is required to avoid duplicate tickets." });
        }

        // 4. Prevent retries racing with an active agent lease
        if (job.Status == "Claimed" && job.LeaseExpiresAtUtc.HasValue && job.LeaseExpiresAtUtc.Value > DateTime.UtcNow)
        {
            return Conflict(new { message = "Job is currently claimed by an active print agent. Cannot retry while agent lease is active." });
        }

        // 5. Allowed retry only for known unprinted failures (e.g. Failed before connection/dispatch)
        if (job.Status != "Failed" && job.Status != "Claimed")
        {
            return BadRequest(new { message = $"Cannot retry job in status '{job.Status}'." });
        }

        job.Status = "Pending";
        job.ClaimToken = null;
        job.ClaimedByAgentId = null;
        job.LeaseExpiresAtUtc = null;
        job.LastError = null;
        job.UpdatedAtUtc = DateTime.UtcNow;

        var history = new List<object>();
        if (!string.IsNullOrWhiteSpace(job.AttemptHistoryJson))
        {
            try { history = JsonSerializer.Deserialize<List<object>>(job.AttemptHistoryJson) ?? new(); } catch { }
        }
        history.Add(new
        {
            timestampUtc = DateTime.UtcNow,
            @event = "OperatorRetry",
            operatorName = User.Identity?.Name ?? "Operator",
            previousStatus = job.Status,
            previousDeliveryState = job.DeliveryState
        });
        job.AttemptHistoryJson = JsonSerializer.Serialize(history);

        await _db.SaveChangesAsync();

        return Ok(new { message = "Print job re-queued for delivery.", jobId = job.Id });
    }

    [HttpPost("api/restaurants/{restaurantId:guid}/printers/jobs/{jobId:guid}/cancel")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin")]
    public async Task<IActionResult> CancelJob(Guid restaurantId, Guid jobId)
    {
        var job = await _db.PrintJobs.FirstOrDefaultAsync(j => j.Id == jobId && j.RestaurantId == restaurantId);
        if (job == null)
        {
            return NotFound(new { message = "Print job not found." });
        }

        job.Status = "Cancelled";
        job.LastError = $"Manually cancelled by {User.Identity?.Name ?? "Operator"}";
        job.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Print job cancelled.", jobId = job.Id });
    }

    [HttpPost("api/restaurants/{restaurantId:guid}/printers/tickets/{ticketId:guid}/reprint")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin,Kitchen,Waiter")]
    [RequireCapability(Capabilities.ThermalPrinting)]
    public async Task<IActionResult> ReprintTicket(Guid restaurantId, Guid ticketId, [FromBody] ReprintTicketRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return BadRequest(new { message = "A reason for reprint is required for auditing." });
        }

        string actor = User.Identity?.Name ?? "Staff";
        var reprintTicket = await _kotRouting.ReprintTicketAsync(restaurantId, ticketId, actor, dto.Reason);

        return Ok(new
        {
            message = "Audited reprint ticket generated and queued.",
            reprintTicketId = reprintTicket.Id,
            reprintTicketNumber = reprintTicket.TicketNumber,
            reprintCount = reprintTicket.ReprintCount
        });
    }

    [HttpGet("api/restaurants/{restaurantId:guid}/printers/tickets/{ticketId:guid}")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin,Kitchen,Waiter")]
    public async Task<IActionResult> GetTicket(Guid restaurantId, Guid ticketId)
    {
        var ticket = await _db.KotTickets
            .Include(t => t.KitchenStation)
            .Include(t => t.PrintJobs)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.RestaurantId == restaurantId);

        if (ticket == null)
        {
            return NotFound(new { message = "KOT ticket not found." });
        }

        var items = JsonSerializer.Deserialize<List<KotTicketItemSnapshotDto>>(ticket.ItemsJson) ?? new();

        return Ok(new KotTicketDto
        {
            Id = ticket.Id,
            KotBatchId = ticket.KotBatchId,
            OrderId = ticket.OrderId,
            OrderNumber = ticket.OrderNumber,
            TableNumber = ticket.TableNumber,
            GroupLabel = ticket.GroupLabelSnapshot,
            TicketNumber = ticket.TicketNumber,
            StationId = ticket.KitchenStationId,
            StationName = ticket.StationNameSnapshot,
            TicketType = ticket.TicketType,
            TimestampIst = ticket.TimestampIstFormatted,
            IsReprint = ticket.IsReprint,
            ReprintCount = ticket.ReprintCount,
            Items = items,
            Notes = ticket.Notes,
            CreatedAtUtc = ticket.CreatedAtUtc
        });
    }

    // Agent Registration & Management

    [HttpPost("api/restaurants/{restaurantId:guid}/printers/agents/register")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin")]
    [RequireCapability(Capabilities.ThermalPrinting)]
    public async Task<IActionResult> RegisterAgent(Guid restaurantId, [FromBody] RegisterPrintAgentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.AgentName))
        {
            return BadRequest(new { message = "Agent name is required." });
        }

        // Generate secure 32-byte secret token: "tablo_pa_" + base64url
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
        string plaintextToken = "tablo_pa_" + Convert.ToBase64String(randomBytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        string hash = HashToken(plaintextToken);
        string prefix = plaintextToken.Substring(0, Math.Min(16, plaintextToken.Length));

        var reg = new PrintAgentRegistration
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            AgentName = dto.AgentName.Trim(),
            ApiKeyHash = hash,
            KeyPrefix = prefix,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.PrintAgentRegistrations.Add(reg);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Registered print agent {AgentName} for restaurant {RestaurantId}", dto.AgentName, restaurantId);

        return Ok(new PrintAgentRegisteredResultDto
        {
            AgentId = reg.Id,
            AgentName = reg.AgentName,
            PlaintextApiKey = plaintextToken,
            KeyPrefix = prefix,
            CreatedAtUtc = reg.CreatedAtUtc
        });
    }

    [HttpGet("api/restaurants/{restaurantId:guid}/printers/agents")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin")]
    public async Task<IActionResult> GetAgents(Guid restaurantId)
    {
        var agents = await _db.PrintAgentRegistrations
            .Where(a => a.RestaurantId == restaurantId && a.IsActive)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new PrintAgentDto
            {
                Id = a.Id,
                AgentName = a.AgentName,
                KeyPrefix = a.KeyPrefix,
                IsActive = a.IsActive,
                LastHeartbeatUtc = a.LastHeartbeatUtc,
                Version = a.Version,
                CreatedAtUtc = a.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(agents);
    }

    [HttpDelete("api/restaurants/{restaurantId:guid}/printers/agents/{agentId:guid}")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin")]
    public async Task<IActionResult> RevokeAgent(Guid restaurantId, Guid agentId)
    {
        var agent = await _db.PrintAgentRegistrations.FirstOrDefaultAsync(a => a.Id == agentId && a.RestaurantId == restaurantId);
        if (agent == null)
        {
            return NotFound(new { message = "Agent not found." });
        }

        agent.IsActive = false;
        agent.RevokedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { message = $"Agent '{agent.AgentName}' revoked." });
    }

    // Local Printing Agent Outbound Polling & Ack Endpoints

    [HttpPost("api/printers/agent/poll")]
    [AllowAnonymous]
    public async Task<IActionResult> AgentPoll([FromBody] AgentPollRequestDto? dto)
    {
        var agent = await AuthenticateAgentAsync();
        if (agent == null) return Unauthorized(new { message = "Invalid or missing X-Agent-Key header." });

        agent.LastHeartbeatUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto?.AgentVersion)) agent.Version = dto.AgentVersion;

        await using var tx = _db.Database.IsRelational() && _db.Database.CurrentTransaction == null
            ? await _db.Database.BeginTransactionAsync()
            : null;

        var now = DateTime.UtcNow;

        // Conservative sweep: When an agent claims a job but its lease expires without acknowledgement,
        // bytes may already have been dispatched to the printer before the agent crashed or lost connectivity.
        // To prevent duplicate paper tickets, NEVER automatically re-dispatch expired claims.
        // Instead, mark them as UncertainDelivery with audited attempt history.
        var expiredClaimedJobs = await _db.PrintJobs
            .Where(j => j.RestaurantId == agent.RestaurantId
                        && j.Status == "Claimed"
                        && j.LeaseExpiresAtUtc != null
                        && j.LeaseExpiresAtUtc < now)
            .ToListAsync();

        if (expiredClaimedJobs.Count > 0)
        {
            foreach (var exp in expiredClaimedJobs)
            {
                exp.Status = "UncertainDelivery";
                exp.DeliveryState = "Uncertain";
                exp.UncertainReason = "Claim lease expired without agent acknowledgement (agent may have crashed before, during, or after transmission). Physical printing uncertain; requires operator check or audited reprint.";
                exp.UpdatedAtUtc = now;

                var history = new List<object>();
                if (!string.IsNullOrWhiteSpace(exp.AttemptHistoryJson))
                {
                    try { history = JsonSerializer.Deserialize<List<object>>(exp.AttemptHistoryJson) ?? new(); } catch { }
                }
                history.Add(new
                {
                    timestampUtc = now,
                    agentId = exp.ClaimedByAgentId,
                    claimToken = exp.ClaimToken,
                    @event = "ClaimLeaseExpired",
                    action = "MarkedUncertainDelivery",
                    reason = exp.UncertainReason
                });
                exp.AttemptHistoryJson = JsonSerializer.Serialize(history);
            }
            await _db.SaveChangesAsync();
        }

        List<PrintJob> jobsToClaim = new();

        if (_db.Database.IsRelational())
        {
            // PostgreSQL atomic select using FOR UPDATE OF j SKIP LOCKED
            var eligibleJobIds = await _db.Database.SqlQueryRaw<Guid>(
                @"SELECT j.""Id"" 
                  FROM ""PrintJobs"" j
                  INNER JOIN ""ThermalPrinters"" p ON j.""PrinterId"" = p.""Id""
                  WHERE j.""RestaurantId"" = {0} 
                    AND p.""IsActive"" = TRUE
                    AND j.""Status"" = 'Pending'
                  ORDER BY j.""CreatedAtUtc"" ASC
                  LIMIT 10
                  FOR UPDATE OF j SKIP LOCKED",
                agent.RestaurantId
            ).ToListAsync();

            if (eligibleJobIds.Count > 0)
            {
                jobsToClaim = await _db.PrintJobs
                    .Include(j => j.Printer)
                    .Where(j => eligibleJobIds.Contains(j.Id))
                    .ToListAsync();
            }
        }
        else
        {
            // In-Memory fallback for unit test fixtures
            jobsToClaim = await _db.PrintJobs
                .Include(j => j.Printer)
                .Where(j => j.RestaurantId == agent.RestaurantId 
                            && j.Printer.IsActive 
                            && j.Status == "Pending")
                .OrderBy(j => j.CreatedAtUtc)
                .Take(10)
                .ToListAsync();
        }

        var jobDtos = new List<AgentPollJobDto>();
        foreach (var job in jobsToClaim)
        {
            string claimToken = Guid.NewGuid().ToString("N");
            job.Status = "Claimed";
            job.ClaimToken = claimToken;
            job.ClaimedByAgentId = agent.Id;
            job.ClaimedAtUtc = now;
            job.LeaseExpiresAtUtc = now.AddSeconds(60); // 60-second lease
            job.Attempts++;
            job.LastAttemptUtc = now;
            job.UpdatedAtUtc = now;

            jobDtos.Add(new AgentPollJobDto
            {
                JobId = job.Id,
                ClaimToken = claimToken,
                PrinterId = job.PrinterId,
                PrinterName = job.Printer.Name,
                ConnectionType = job.Printer.ConnectionType,
                IpAddress = job.Printer.IpAddress,
                Port = job.Printer.Port,
                LocalQueueName = job.Printer.LocalQueueName,
                PaperWidthMm = job.PaperWidthMm,
                PayloadEscPosBase64 = job.PayloadEscPosBase64,
                ReadableText = job.ReadableText
            });
        }

        await _db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();

        return Ok(new { jobs = jobDtos, serverTimeUtc = DateTime.UtcNow });
    }

    [HttpPost("api/printers/agent/jobs/{jobId:guid}/ack")]
    [AllowAnonymous]
    public async Task<IActionResult> AgentJobAck(Guid jobId, [FromBody] AgentJobAckDto dto)
    {
        var agent = await AuthenticateAgentAsync();
        if (agent == null) return Unauthorized(new { message = "Invalid or missing X-Agent-Key header." });

        if (string.IsNullOrWhiteSpace(dto.Status))
        {
            return BadRequest(new { message = "Acknowledgement status is required." });
        }

        string rawStatus = dto.Status.Trim();
        string normalizedStatus;

        if (string.Equals(rawStatus, "Printed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawStatus, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            normalizedStatus = "Completed";
        }
        else if (string.Equals(rawStatus, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            normalizedStatus = "Failed";
        }
        else if (string.Equals(rawStatus, "Uncertain", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(rawStatus, "UncertainDelivery", StringComparison.OrdinalIgnoreCase))
        {
            normalizedStatus = "UncertainDelivery";
        }
        else
        {
            return BadRequest(new { message = $"Unknown acknowledgement status '{dto.Status}'. Permitted: Completed, Failed, Uncertain." });
        }

        await using var tx = _db.Database.IsRelational() && _db.Database.CurrentTransaction == null
            ? await _db.Database.BeginTransactionAsync()
            : null;

        var job = await _db.PrintJobs.FirstOrDefaultAsync(j => j.Id == jobId && j.RestaurantId == agent.RestaurantId);
        if (job == null) return NotFound(new { message = "Print job not found." });

        // 1. Validate claiming agent
        if (job.ClaimedByAgentId.HasValue && job.ClaimedByAgentId.Value != agent.Id)
        {
            return StatusCode(403, new { message = "This job was claimed by a different print agent." });
        }

        // 2. Validate claim token & idempotency
        bool isTerminal = job.Status == "Completed" || job.Status == "Cancelled";
        bool tokenMatches = !string.IsNullOrEmpty(job.ClaimToken) && string.Equals(job.ClaimToken, dto.ClaimToken?.Trim(), StringComparison.Ordinal);

        if (isTerminal)
        {
            // Idempotent acknowledgement replay
            if (tokenMatches || string.IsNullOrEmpty(dto.ClaimToken))
            {
                return Ok(new { success = true, jobId = job.Id, status = job.Status, message = "Job already in terminal state." });
            }
            // Stale claim attempting to modify finished job
            return Ok(new { success = true, jobId = job.Id, status = job.Status, message = "Stale acknowledgement ignored for terminal job." });
        }

        if (!tokenMatches)
        {
            return Conflict(new { message = "Stale or mismatched claim token. Job lease may have expired and been reclaimed." });
        }

        // 3. Apply state transition
        job.Status = normalizedStatus;
        job.UpdatedAtUtc = DateTime.UtcNow;

        if (normalizedStatus == "Completed")
        {
            job.CompletedAtUtc = DateTime.UtcNow;
            job.DeliveryState = "Certain";
            job.LastError = null;
        }
        else if (normalizedStatus == "UncertainDelivery")
        {
            job.DeliveryState = "Uncertain";
            job.UncertainReason = !string.IsNullOrWhiteSpace(dto.ErrorMessage) 
                ? dto.ErrorMessage 
                : "Bytes dispatched to printer socket, but acknowledgement was lost or connection terminated prematurely.";
            job.LastError = job.UncertainReason;
        }
        else // Failed
        {
            job.DeliveryState = "Certain";
            job.LastError = dto.ErrorMessage ?? "Printer error";
        }

        // 4. Record attempt history
        var history = new List<object>();
        if (!string.IsNullOrWhiteSpace(job.AttemptHistoryJson))
        {
            try { history = JsonSerializer.Deserialize<List<object>>(job.AttemptHistoryJson) ?? new(); } catch { }
        }
        history.Add(new
        {
            timestampUtc = DateTime.UtcNow,
            agentId = agent.Id,
            claimToken = dto.ClaimToken,
            status = normalizedStatus,
            deliveryState = job.DeliveryState,
            error = dto.ErrorMessage,
            durationMs = dto.DurationMs
        });
        job.AttemptHistoryJson = JsonSerializer.Serialize(history);

        await _db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();

        return Ok(new { success = true, jobId = job.Id, status = job.Status, deliveryState = job.DeliveryState });
    }

    [HttpPost("api/printers/agent/heartbeat")]
    [AllowAnonymous]
    public async Task<IActionResult> AgentHeartbeat([FromBody] AgentHeartbeatDto dto)
    {
        var agent = await AuthenticateAgentAsync();
        if (agent == null) return Unauthorized(new { message = "Invalid or missing X-Agent-Key header." });

        agent.LastHeartbeatUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.AgentVersion)) agent.Version = dto.AgentVersion;

        if (dto.PrinterStatuses != null)
        {
            foreach (var statusReport in dto.PrinterStatuses)
            {
                var printer = await _db.ThermalPrinters.FirstOrDefaultAsync(p => p.Id == statusReport.PrinterId && p.RestaurantId == agent.RestaurantId);
                if (printer != null)
                {
                    printer.Status = statusReport.Status;
                    printer.LastStatusMessage = statusReport.StatusMessage;
                    printer.LastSeenAtUtc = DateTime.UtcNow;
                    printer.UpdatedAtUtc = DateTime.UtcNow;
                }
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { success = true, serverTimeUtc = DateTime.UtcNow });
    }

    private async Task<PrintAgentRegistration?> AuthenticateAgentAsync()
    {
        if (!Request.Headers.TryGetValue("X-Agent-Key", out var headerVal) || string.IsNullOrWhiteSpace(headerVal))
        {
            return null;
        }

        string rawToken = headerVal.ToString().Trim();
        string hash = HashToken(rawToken);

        var agent = await _db.PrintAgentRegistrations
            .FirstOrDefaultAsync(a => a.ApiKeyHash == hash && a.IsActive);

        return agent;
    }

    private static string HashToken(string token)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
