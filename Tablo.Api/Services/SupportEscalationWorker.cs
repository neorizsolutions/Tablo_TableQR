using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Hubs;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services
{
    public class SupportEscalationWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<SupportEscalationWorker> logger) : BackgroundService
    {
        public const int MaxAttempts = 5;
        public static readonly TimeSpan StaleClaimLease = TimeSpan.FromMinutes(2);
        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> InMemoryClaimLocks = new();
        private readonly string _workerId = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}"[..24];

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("SupportEscalationWorker {WorkerId} started.", _workerId);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), timeProvider);

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessDueEscalationsAsync(stoppingToken);
                    await PruneRevokedRealtimeConnectionsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error in SupportEscalationWorker loop.");
                }
            }

            logger.LogInformation("SupportEscalationWorker {WorkerId} stopping.", _workerId);
        }

        public async Task PruneRevokedRealtimeConnectionsAsync(CancellationToken cancellationToken = default)
        {
            using var scope = scopeFactory.CreateScope();
            var registry = scope.ServiceProvider.GetService<SupportConnectionRegistry>();
            if (registry == null || registry.ActiveCount == 0)
                return;

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await registry.PruneAllInvalidConnectionsAsync(db, cancellationToken);
        }

        public async Task<int> ProcessDueEscalationsAsync(CancellationToken cancellationToken = default)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var realtime = scope.ServiceProvider.GetService<ISupportRealtimeNotifier>();
            var configuration = scope.ServiceProvider.GetService<IConfiguration>();

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var staleCutoff = now.Subtract(StaleClaimLease);

            var candidates = await db.SupportEscalationLogs
                .AsNoTracking()
                .Where(l =>
                    l.AttemptCount < MaxAttempts &&
                    (
                        (l.Status == SupportEscalationStatus.Pending && l.ScheduledDeadlineUtc <= now) ||
                        (l.Status == SupportEscalationStatus.Failed && (l.NextRetryAtUtc == null || l.NextRetryAtUtc <= now)) ||
                        (l.Status == SupportEscalationStatus.Processing && l.ClaimedAtUtc != null && l.ClaimedAtUtc <= staleCutoff)
                    ))
                .OrderBy(l => l.ScheduledDeadlineUtc)
                .Take(25)
                .Select(l => l.Id)
                .ToListAsync(cancellationToken);

            int dispatchedCount = 0;

            foreach (var logId in candidates)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    if (await TryProcessSingleCandidateAsync(db, emailService, realtime, configuration, logId, now, staleCutoff, cancellationToken))
                    {
                        dispatchedCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled exception while processing escalation log {LogId}", logId);
                }
            }

            return dispatchedCount;
        }

        private async Task<bool> TryProcessSingleCandidateAsync(
            ApplicationDbContext db,
            IEmailService emailService,
            ISupportRealtimeNotifier? realtime,
            IConfiguration? configuration,
            Guid logId,
            DateTime now,
            DateTime staleCutoff,
            CancellationToken ct)
        {
            // 1. Atomically claim this escalation log row so concurrent workers never double-send.
            bool claimed;
            if (db.Database.IsRelational())
            {
                int rows = await db.SupportEscalationLogs
                    .Where(l =>
                        l.Id == logId &&
                        l.AttemptCount < MaxAttempts &&
                        (
                            (l.Status == SupportEscalationStatus.Pending && l.ScheduledDeadlineUtc <= now) ||
                            (l.Status == SupportEscalationStatus.Failed && (l.NextRetryAtUtc == null || l.NextRetryAtUtc <= now)) ||
                            (l.Status == SupportEscalationStatus.Processing && l.ClaimedAtUtc != null && l.ClaimedAtUtc <= staleCutoff)
                        ))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(l => l.Status, SupportEscalationStatus.Processing)
                        .SetProperty(l => l.ClaimedAtUtc, now)
                        .SetProperty(l => l.ClaimedBy, _workerId)
                        .SetProperty(l => l.LastAttemptAtUtc, now)
                        .SetProperty(l => l.AttemptCount, l => l.AttemptCount + 1), ct);
                claimed = rows == 1;
            }
            else
            {
                var sem = InMemoryClaimLocks.GetOrAdd(logId, _ => new SemaphoreSlim(1, 1));
                await sem.WaitAsync(ct);
                try
                {
                    var row = await db.SupportEscalationLogs.FirstOrDefaultAsync(l => l.Id == logId, ct);
                    if (row != null &&
                        row.AttemptCount < MaxAttempts &&
                        (
                            (row.Status == SupportEscalationStatus.Pending && row.ScheduledDeadlineUtc <= now) ||
                            (row.Status == SupportEscalationStatus.Failed && (row.NextRetryAtUtc == null || row.NextRetryAtUtc <= now)) ||
                            (row.Status == SupportEscalationStatus.Processing && row.ClaimedAtUtc != null && row.ClaimedAtUtc <= staleCutoff)
                        ))
                    {
                        row.Status = SupportEscalationStatus.Processing;
                        row.ClaimedAtUtc = now;
                        row.ClaimedBy = _workerId;
                        row.LastAttemptAtUtc = now;
                        row.AttemptCount += 1;
                        await db.SaveChangesAsync(ct);
                        claimed = true;
                    }
                    else
                    {
                        claimed = false;
                    }
                }
                finally
                {
                    sem.Release();
                }
            }

            if (!claimed)
                return false;

            // Clear tracker to force authoritative re-read of log & conversation state before sending external email.
            db.ChangeTracker.Clear();

            var log = await db.SupportEscalationLogs.FirstOrDefaultAsync(l => l.Id == logId, ct);
            if (log == null)
                return false;

            var conv = await db.SupportConversations
                .AsNoTracking()
                .Include(c => c.Restaurant)
                .FirstOrDefaultAsync(c => c.Id == log.ConversationId, ct);

            // 2. Recheck conversation state right before dispatch.
            if (conv == null ||
                conv.Status != SupportConversationStatus.AwaitingSupport ||
                conv.CurrentWaitingPeriodId != log.WaitingPeriodId ||
                conv.IsEscalated ||
                conv.EscalationDeadlineUtc == null ||
                conv.EscalationDeadlineUtc > now)
            {
                log.Status = SupportEscalationStatus.Cancelled;
                log.ClaimedAtUtc = null;
                log.NextRetryAtUtc = null;
                log.LastError = "Cancelled during pre-dispatch conversation state recheck.";
                await db.SaveChangesAsync(ct);
                return false;
            }

            var waitingDuration = conv.AwaitingSupportSinceUtc.HasValue
                ? now - conv.AwaitingSupportSinceUtc.Value
                : TimeSpan.FromMinutes(10);

            var frontendBaseUrl = configuration?["App:FrontendBaseUrl"]?.TrimEnd('/')
                                  ?? "https://tablopos.neorizsolutions.com";
            var inboxLink = $"{frontendBaseUrl}/super-admin?tab=inbox&conversationId={conv.Id}";
            var idempotencyKey = $"support-escalation-{conv.Id:N}-{log.WaitingPeriodId:N}";
            var emailSubject = $"[TABLO Urgent Escalation] Unanswered Support Ticket: {conv.Subject}";
            var emailBody = $@"TABLO Support 10-Minute Emergency Escalation

A restaurant support request has gone unanswered for 10+ minutes.

Restaurant Name: {conv.Restaurant?.Name ?? "Unknown Restaurant"}
Restaurant ID: {conv.RestaurantId}
Conversation ID: {conv.Id}
Subject: {conv.Subject}
Waiting Started (UTC): {conv.AwaitingSupportSinceUtc:yyyy-MM-dd HH:mm:ss}Z
Waiting Duration: {Math.Floor(waitingDuration.TotalMinutes)}m {waitingDuration.Seconds}s

Open in Super Admin Support Inbox:
{inboxLink}

This escalation alert is automated and does not constitute a reply to the restaurant.";

            try
            {
                var dispatchResult = await emailService.SendEmailWithResultAsync(
                    log.EscalationTargetEmail,
                    emailSubject,
                    emailBody,
                    idempotencyKey,
                    ct);

                log.Status = SupportEscalationStatus.Dispatched;
                log.DispatchedAtUtc = now;
                log.ProviderMessageId = dispatchResult.ProviderMessageId;
                log.LastError = null;
                log.ClaimedAtUtc = null;
                log.NextRetryAtUtc = null;

                await using var tx = await TableOrderTransaction.BeginAsync(db);

                // Conditionally mark conversation escalated only if it is STILL waiting on this exact period.
                bool conversationUpdated = false;
                if (db.Database.IsRelational())
                {
                    int updatedRows = await db.SupportConversations
                        .Where(c =>
                            c.Id == conv.Id &&
                            c.Status == SupportConversationStatus.AwaitingSupport &&
                            c.CurrentWaitingPeriodId == log.WaitingPeriodId &&
                            !c.IsEscalated)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(c => c.IsEscalated, true)
                            .SetProperty(c => c.EscalatedAtUtc, now)
                            .SetProperty(c => c.UpdatedAtUtc, now)
                            .SetProperty(c => c.Version, c => c.Version + 1), ct);
                    conversationUpdated = updatedRows == 1;
                }
                else
                {
                    var trackedConv = await db.SupportConversations.FirstOrDefaultAsync(c => c.Id == conv.Id, ct);
                    if (trackedConv != null &&
                        trackedConv.Status == SupportConversationStatus.AwaitingSupport &&
                        trackedConv.CurrentWaitingPeriodId == log.WaitingPeriodId &&
                        !trackedConv.IsEscalated)
                    {
                        trackedConv.IsEscalated = true;
                        trackedConv.EscalatedAtUtc = now;
                        trackedConv.UpdatedAtUtc = now;
                        trackedConv.Version += 1;
                        conversationUpdated = true;
                    }
                }

                db.SupportAuditLogs.Add(new SupportAuditLog
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conv.Id,
                    ActorType = "SystemWorker",
                    ActorName = _workerId,
                    Action = "Escalated",
                    Details = $"Escalated to {log.EscalationTargetEmail}; WaitingPeriodId={log.WaitingPeriodId}; ProviderMessageId={dispatchResult.ProviderMessageId}",
                    TimestampUtc = now
                });

                await db.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);

                if (conversationUpdated && realtime != null)
                {
                    var freshConv = await db.SupportConversations
                        .AsNoTracking()
                        .Include(c => c.Restaurant)
                        .FirstOrDefaultAsync(c => c.Id == conv.Id, ct);

                    if (freshConv != null)
                    {
                        await realtime.PublishAsync(new SupportRealtimeEventDto
                        {
                            EventId = Guid.NewGuid(),
                            EventType = "ConversationEscalated",
                            OccurredAtUtc = now,
                            ConversationId = freshConv.Id,
                            RestaurantId = freshConv.RestaurantId,
                            ConversationVersion = freshConv.Version,
                            Conversation = SupportService.ToSummaryDto(freshConv, now),
                            Escalation = SupportService.ToEscalationDto(log)
                        }, ct);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Escalation email attempt {Attempt}/{Max} failed for conversation {ConversationId}",
                    log.AttemptCount, MaxAttempts, conv.Id);

                log.Status = SupportEscalationStatus.Failed;
                log.ClaimedAtUtc = null;
                log.LastError = ex.Message.Length > 980 ? ex.Message[..980] : ex.Message;

                if (log.AttemptCount < MaxAttempts)
                {
                    double backoffSeconds = Math.Min(300, 15 * Math.Pow(2, log.AttemptCount - 1));
                    log.NextRetryAtUtc = now.AddSeconds(backoffSeconds);
                }
                else
                {
                    log.NextRetryAtUtc = null;
                }

                db.SupportAuditLogs.Add(new SupportAuditLog
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conv.Id,
                    ActorType = "SystemWorker",
                    ActorName = _workerId,
                    Action = "EscalationFailed",
                    Details = $"Attempt {log.AttemptCount}/{MaxAttempts}: {log.LastError}",
                    TimestampUtc = now
                });

                await db.SaveChangesAsync(ct);
                return false;
            }
        }
    }
}
