using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Hubs;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services
{
    public class SupportService(
        ApplicationDbContext db,
        TimeProvider timeProvider,
        ISupportRealtimeNotifier realtime,
        ILogger<SupportService> logger) : ISupportService
    {
        public const string EscalationTargetEmail = "support@neorizsolutions.com";
        public static readonly TimeSpan EscalationDelay = TimeSpan.FromMinutes(10);

        // Used solely for synchronizing non-relational (InMemory) test writes cleanly.
        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> InMemoryConversationLocks = new();

        private async Task<IDisposable?> AcquireConversationLockAsync(Guid conversationId, CancellationToken ct)
        {
            if (db.Database.IsRelational())
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"SupportConversations\" WHERE \"Id\" = {conversationId} FOR UPDATE", ct);
                return null;
            }

            var sem = InMemoryConversationLocks.GetOrAdd(conversationId, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(ct);
            return new SemaphoreReleaser(sem);
        }

        private sealed class SemaphoreReleaser(SemaphoreSlim sem) : IDisposable
        {
            public void Dispose() => sem.Release();
        }

        private DateTime NowUtc => timeProvider.GetUtcNow().UtcDateTime;

        private async Task<Employee> ValidateRestaurantAdminActorAsync(Guid restaurantId, Guid employeeId, CancellationToken ct)
        {
            if (restaurantId == Guid.Empty || employeeId == Guid.Empty)
                throw new UnauthorizedAccessException("Valid restaurant administrator identity is required.");

            var employee = await db.Employees
                .AsNoTracking()
                .Include(e => e.Role)
                .Include(e => e.Restaurant)
                .FirstOrDefaultAsync(e => e.Id == employeeId && e.RestaurantId == restaurantId && !e.IsDeleted && e.IsActive, ct);

            if (employee == null)
                throw new UnauthorizedAccessException("Account is inactive or not found.");

            if (employee.Role == null || employee.Role.Name is not ("Admin" or "Owner"))
                throw new UnauthorizedAccessException("Only Restaurant Admin or Owner accounts can access Support.");

            if (!NavigationAccess.Allows(employee, new[] { "support" }))
                throw new UnauthorizedAccessException("Support menu access has been restricted.");

            return employee;
        }

        public async Task<SupportSyncResponseDto> GetRestaurantConversationsAsync(
            Guid restaurantId,
            DateTime? sinceUtc = null,
            CancellationToken cancellationToken = default)
        {
            var now = NowUtc;
            var query = db.SupportConversations
                .AsNoTracking()
                .Include(c => c.Restaurant)
                .Where(c => c.RestaurantId == restaurantId);

            if (sinceUtc.HasValue)
            {
                query = query.Where(c => c.UpdatedAtUtc >= sinceUtc.Value);
            }

            var conversations = await query
                .OrderByDescending(c => c.LastActivityAtUtc)
                .ToListAsync(cancellationToken);

            var totalUnread = await db.SupportConversations
                .AsNoTracking()
                .Where(c => c.RestaurantId == restaurantId && c.RestaurantUnreadCount > 0)
                .SumAsync(c => c.RestaurantUnreadCount, cancellationToken);

            return new SupportSyncResponseDto
            {
                ServerUtc = now,
                Conversations = conversations.Select(c => ToSummaryDto(c, now)).ToList(),
                TotalUnreadCount = totalUnread
            };
        }

        public async Task<SupportConversationDetailDto?> GetConversationDetailAsync(
            Guid conversationId,
            Guid? restaurantId,
            long? afterSequence = null,
            CancellationToken cancellationToken = default)
        {
            var now = NowUtc;
            var query = db.SupportConversations
                .AsNoTracking()
                .Include(c => c.Restaurant)
                .Where(c => c.Id == conversationId);

            if (restaurantId.HasValue)
            {
                query = query.Where(c => c.RestaurantId == restaurantId.Value);
            }

            var conv = await query.FirstOrDefaultAsync(cancellationToken);
            if (conv == null) return null;

            var msgQuery = db.SupportMessages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId);

            if (afterSequence.HasValue)
            {
                msgQuery = msgQuery.Where(m => m.SequenceNumber > afterSequence.Value);
            }

            var messages = await msgQuery
                .OrderBy(m => m.SequenceNumber)
                .ThenBy(m => m.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            var escalations = await db.SupportEscalationLogs
                .AsNoTracking()
                .Where(e => e.ConversationId == conversationId)
                .OrderByDescending(e => e.ScheduledDeadlineUtc)
                .ToListAsync(cancellationToken);

            var audits = await db.SupportAuditLogs
                .AsNoTracking()
                .Where(a => a.ConversationId == conversationId)
                .OrderByDescending(a => a.TimestampUtc)
                .Take(50)
                .ToListAsync(cancellationToken);

            return new SupportConversationDetailDto
            {
                Conversation = ToSummaryDto(conv, now),
                Messages = messages.Select(ToMessageDto).ToList(),
                EscalationLogs = escalations.Select(ToEscalationDto).ToList(),
                AuditLogs = audits.Select(ToAuditDto).ToList()
            };
        }

        public async Task<SupportConversationMutationResultDto> CreateConversationAsync(
            Guid restaurantId,
            Guid employeeId,
            CreateSupportConversationDto request,
            CancellationToken cancellationToken = default)
        {
            var subject = request.Subject?.Trim() ?? string.Empty;
            var body = request.Message?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(subject))
                throw new ArgumentException("Subject is required.");
            if (string.IsNullOrWhiteSpace(body))
                throw new ArgumentException("Message is required.");

            var actor = await ValidateRestaurantAdminActorAsync(restaurantId, employeeId, cancellationToken);
            var now = NowUtc;

            if (request.ClientOperationId is Guid opId && opId != Guid.Empty)
            {
                var existing = await db.SupportConversations
                    .AsNoTracking()
                    .Include(c => c.Restaurant)
                    .Include(c => c.Messages)
                    .FirstOrDefaultAsync(c => c.RestaurantId == restaurantId && c.ClientOperationId == opId, cancellationToken);
                if (existing != null)
                {
                    var firstMsg = existing.Messages.OrderBy(m => m.SequenceNumber).FirstOrDefault();
                    return new SupportConversationMutationResultDto
                    {
                        Conversation = ToSummaryDto(existing, now),
                        Message = firstMsg != null ? ToMessageDto(firstMsg) : null,
                        Deduplicated = true
                    };
                }
            }

            var waitingPeriodId = Guid.NewGuid();
            var deadline = now.Add(EscalationDelay);
            var conversationId = Guid.NewGuid();
            var messageId = Guid.NewGuid();

            var conversation = new SupportConversation
            {
                Id = conversationId,
                RestaurantId = restaurantId,
                Subject = subject,
                Status = SupportConversationStatus.AwaitingSupport,
                CreatedByEmployeeId = employeeId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                LastActivityAtUtc = now,
                AwaitingSupportSinceUtc = now,
                CurrentWaitingPeriodId = waitingPeriodId,
                EscalationDeadlineUtc = deadline,
                IsEscalated = false,
                EscalatedAtUtc = null,
                RestaurantUnreadCount = 0,
                SuperAdminUnreadCount = 1,
                ClientOperationId = request.ClientOperationId,
                LastMessagePreview = TruncatePreview(body),
                LastMessageSenderType = SupportSenderType.Restaurant,
                LastMessageId = messageId,
                RestaurantLastReadAtUtc = now,
                Version = 1
            };

            var message = new SupportMessage
            {
                Id = messageId,
                ConversationId = conversationId,
                SenderType = SupportSenderType.Restaurant,
                SenderEmployeeId = employeeId,
                SenderName = string.IsNullOrWhiteSpace(actor.FullName) ? actor.Email : actor.FullName,
                SenderEmail = actor.Email,
                Message = body,
                CreatedAtUtc = now,
                SequenceNumber = 1,
                ClientOperationId = request.ClientOperationId
            };

            var escalationLog = new SupportEscalationLog
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                WaitingPeriodId = waitingPeriodId,
                EscalationTargetEmail = EscalationTargetEmail,
                ScheduledDeadlineUtc = deadline,
                Status = SupportEscalationStatus.Pending
            };

            var createdAudit = new SupportAuditLog
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                ActorType = "Restaurant",
                ActorName = message.SenderName,
                Action = "Created",
                Details = $"Subject: {subject}",
                TimestampUtc = now
            };

            var escalationAudit = new SupportAuditLog
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                ActorType = "System",
                ActorName = "EscalationScheduler",
                Action = "EscalationScheduled",
                Details = $"WaitingPeriodId={waitingPeriodId}; DeadlineUtc={deadline:O}",
                TimestampUtc = now
            };

            db.SupportConversations.Add(conversation);
            db.SupportMessages.Add(message);
            db.SupportEscalationLogs.Add(escalationLog);
            db.SupportAuditLogs.AddRange(createdAudit, escalationAudit);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException) when (request.ClientOperationId is Guid retryOp && retryOp != Guid.Empty)
            {
                db.ChangeTracker.Clear();
                var raced = await db.SupportConversations
                    .AsNoTracking()
                    .Include(c => c.Restaurant)
                    .Include(c => c.Messages)
                    .FirstOrDefaultAsync(c => c.RestaurantId == restaurantId && c.ClientOperationId == retryOp, cancellationToken);
                if (raced != null)
                {
                    var firstMsg = raced.Messages.OrderBy(m => m.SequenceNumber).FirstOrDefault();
                    return new SupportConversationMutationResultDto
                    {
                        Conversation = ToSummaryDto(raced, now),
                        Message = firstMsg != null ? ToMessageDto(firstMsg) : null,
                        Deduplicated = true
                    };
                }
                throw;
            }

            // Keep navigation fixup on the context identity map when this scoped service is reused.
            // The validated actor is read without tracking and may carry another Restaurant instance.
            conversation.Restaurant = await db.Restaurants.FindAsync([restaurantId], cancellationToken)
                ?? throw new KeyNotFoundException("Support restaurant not found.");
            var summaryDto = ToSummaryDto(conversation, now);
            var msgDto = ToMessageDto(message);

            await PublishEventAsync("ConversationCreated", summaryDto, msgDto, null, null, now, cancellationToken);

            return new SupportConversationMutationResultDto
            {
                Conversation = summaryDto,
                Message = msgDto,
                Deduplicated = false
            };
        }

        public async Task<SupportConversationMutationResultDto> AddRestaurantReplyAsync(
            Guid conversationId,
            Guid restaurantId,
            Guid employeeId,
            CreateSupportMessageDto request,
            CancellationToken cancellationToken = default)
        {
            var body = request.Message?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(body))
                throw new ArgumentException("Message cannot be empty.");

            var actor = await ValidateRestaurantAdminActorAsync(restaurantId, employeeId, cancellationToken);

            await using var tx = await TableOrderTransaction.BeginAsync(db);
            using var memLock = await AcquireConversationLockAsync(conversationId, cancellationToken);

            var now = NowUtc;

            var conv = await db.SupportConversations
                .Include(c => c.Restaurant)
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.RestaurantId == restaurantId, cancellationToken)
                ?? throw new KeyNotFoundException("Support conversation not found.");

            if (request.ClientOperationId is Guid opId && opId != Guid.Empty)
            {
                var dup = await db.SupportMessages.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.ClientOperationId == opId, cancellationToken);
                if (dup != null)
                {
                    return new SupportConversationMutationResultDto
                    {
                        Conversation = ToSummaryDto(conv, now),
                        Message = ToMessageDto(dup),
                        Deduplicated = true
                    };
                }
            }

            var nextSeq = (await db.SupportMessages
                .Where(m => m.ConversationId == conversationId)
                .MaxAsync(m => (long?)m.SequenceNumber, cancellationToken) ?? 0) + 1;

            var senderName = string.IsNullOrWhiteSpace(actor.FullName) ? actor.Email : actor.FullName;
            var message = new SupportMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                SenderType = SupportSenderType.Restaurant,
                SenderEmployeeId = employeeId,
                SenderName = senderName,
                SenderEmail = actor.Email,
                Message = body,
                CreatedAtUtc = now,
                SequenceNumber = nextSeq,
                ClientOperationId = request.ClientOperationId
            };
            db.SupportMessages.Add(message);

            bool wasResolved = conv.Status == SupportConversationStatus.Resolved;
            bool startedNewWaitingPeriod = conv.Status != SupportConversationStatus.AwaitingSupport;

            if (startedNewWaitingPeriod)
            {
                // AwaitingRestaurant or Resolved transitions into a new AwaitingSupport period.
                var waitingPeriodId = Guid.NewGuid();
                var deadline = now.Add(EscalationDelay);

                conv.Status = SupportConversationStatus.AwaitingSupport;
                if (wasResolved)
                {
                    conv.ResolvedAtUtc = null;
                    conv.ResolvedBy = null;
                }
                conv.AwaitingSupportSinceUtc = now;
                conv.CurrentWaitingPeriodId = waitingPeriodId;
                conv.EscalationDeadlineUtc = deadline;
                conv.IsEscalated = false;
                conv.EscalatedAtUtc = null;

                db.SupportEscalationLogs.Add(new SupportEscalationLog
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    WaitingPeriodId = waitingPeriodId,
                    EscalationTargetEmail = EscalationTargetEmail,
                    ScheduledDeadlineUtc = deadline,
                    Status = SupportEscalationStatus.Pending
                });

                db.SupportAuditLogs.Add(new SupportAuditLog
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    ActorType = "System",
                    ActorName = "EscalationScheduler",
                    Action = "EscalationScheduled",
                    Details = $"WaitingPeriodId={waitingPeriodId}; DeadlineUtc={deadline:O}",
                    TimestampUtc = now
                });
            }
            // Note: When already AwaitingSupport, original AwaitingSupportSinceUtc & EscalationDeadlineUtc are strictly preserved!

            conv.LastActivityAtUtc = now;
            conv.UpdatedAtUtc = now;
            conv.LastMessagePreview = TruncatePreview(body);
            conv.LastMessageSenderType = SupportSenderType.Restaurant;
            conv.LastMessageId = message.Id;
            conv.SuperAdminUnreadCount += 1;
            conv.RestaurantUnreadCount = 0;
            conv.RestaurantLastReadAtUtc = now;
            conv.Version += 1;

            db.SupportAuditLogs.Add(new SupportAuditLog
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                ActorType = "Restaurant",
                ActorName = senderName,
                Action = wasResolved ? "Reopened" : "RestaurantReplied",
                Details = wasResolved ? "Reopened with follow-up message." : null,
                TimestampUtc = now
            });

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                if (tx != null) await tx.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException) when (request.ClientOperationId is Guid retryOp && retryOp != Guid.Empty)
            {
                if (tx != null) await tx.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                var currentConv = await db.SupportConversations.AsNoTracking().Include(c => c.Restaurant)
                    .FirstAsync(c => c.Id == conversationId, cancellationToken);
                var existingMsg = await db.SupportMessages.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.ClientOperationId == retryOp, cancellationToken);
                if (existingMsg != null)
                {
                    return new SupportConversationMutationResultDto
                    {
                        Conversation = ToSummaryDto(currentConv, now),
                        Message = ToMessageDto(existingMsg),
                        Deduplicated = true
                    };
                }
                throw;
            }

            var summaryDto = ToSummaryDto(conv, now);
            var msgDto = ToMessageDto(message);
            await PublishEventAsync(wasResolved ? "ConversationReopened" : "MessageCreated", summaryDto, msgDto, null, null, now, cancellationToken);

            return new SupportConversationMutationResultDto
            {
                Conversation = summaryDto,
                Message = msgDto,
                Deduplicated = false
            };
        }

        public async Task<SupportConversationMutationResultDto> ReopenConversationAsync(
            Guid conversationId,
            Guid restaurantId,
            Guid employeeId,
            ReopenSupportConversationDto request,
            CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(request.Message))
            {
                return await AddRestaurantReplyAsync(
                    conversationId,
                    restaurantId,
                    employeeId,
                    new CreateSupportMessageDto
                    {
                        Message = request.Message,
                        ClientOperationId = request.ClientOperationId
                    },
                    cancellationToken);
            }

            var actor = await ValidateRestaurantAdminActorAsync(restaurantId, employeeId, cancellationToken);

            await using var tx = await TableOrderTransaction.BeginAsync(db);
            using var memLock = await AcquireConversationLockAsync(conversationId, cancellationToken);

            var now = NowUtc;
            var conv = await db.SupportConversations
                .Include(c => c.Restaurant)
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.RestaurantId == restaurantId, cancellationToken)
                ?? throw new KeyNotFoundException("Support conversation not found.");

            if (conv.Status != SupportConversationStatus.Resolved)
            {
                return new SupportConversationMutationResultDto
                {
                    Conversation = ToSummaryDto(conv, now),
                    Message = null,
                    Deduplicated = true
                };
            }

            var actorName = string.IsNullOrWhiteSpace(actor.FullName) ? actor.Email : actor.FullName;
            // Reopened without a new message transitions to AwaitingRestaurant until the restaurant sends an unanswered message.
            conv.Status = SupportConversationStatus.AwaitingRestaurant;
            conv.ResolvedAtUtc = null;
            conv.ResolvedBy = null;
            conv.UpdatedAtUtc = now;
            conv.LastActivityAtUtc = now;
            conv.Version += 1;

            db.SupportAuditLogs.Add(new SupportAuditLog
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                ActorType = "Restaurant",
                ActorName = actorName,
                Action = "Reopened",
                Details = "Reopened without a message (awaiting follow-up).",
                TimestampUtc = now
            });

            await db.SaveChangesAsync(cancellationToken);
            if (tx != null) await tx.CommitAsync(cancellationToken);

            var summaryDto = ToSummaryDto(conv, now);
            await PublishEventAsync("ConversationReopened", summaryDto, null, null, null, now, cancellationToken);

            return new SupportConversationMutationResultDto
            {
                Conversation = summaryDto,
                Message = null,
                Deduplicated = false
            };
        }

        public async Task<SupportConversationSummaryDto?> MarkRestaurantReadAsync(
            Guid conversationId,
            Guid restaurantId,
            string actorName,
            CancellationToken cancellationToken = default)
        {
            await using var tx = await TableOrderTransaction.BeginAsync(db);
            using var memLock = await AcquireConversationLockAsync(conversationId, cancellationToken);

            var now = NowUtc;
            var conv = await db.SupportConversations
                .Include(c => c.Restaurant)
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.RestaurantId == restaurantId, cancellationToken);
            if (conv == null) return null;

            bool changed = conv.RestaurantUnreadCount > 0;
            conv.RestaurantUnreadCount = 0;
            conv.RestaurantLastReadAtUtc = now;
            if (changed)
            {
                conv.UpdatedAtUtc = now;
                conv.Version += 1;
            }

            await db.SaveChangesAsync(cancellationToken);
            if (tx != null) await tx.CommitAsync(cancellationToken);

            var dto = ToSummaryDto(conv, now);
            if (changed)
            {
                await PublishEventAsync("ConversationRead", dto, null, null, "Restaurant", now, cancellationToken);
            }
            return dto;
        }

        public async Task<SupportConversationSummaryDto?> ResolveConversationAsync(
            Guid conversationId,
            Guid? restaurantId,
            string actorType,
            string actorName,
            string? note = null,
            CancellationToken cancellationToken = default)
        {
            await using var tx = await TableOrderTransaction.BeginAsync(db);
            using var memLock = await AcquireConversationLockAsync(conversationId, cancellationToken);

            var now = NowUtc;
            var query = db.SupportConversations
                .Include(c => c.Restaurant)
                .Where(c => c.Id == conversationId);
            if (restaurantId.HasValue)
            {
                query = query.Where(c => c.RestaurantId == restaurantId.Value);
            }

            var conv = await query.FirstOrDefaultAsync(cancellationToken);
            if (conv == null) return null;

            if (conv.Status == SupportConversationStatus.Resolved)
            {
                return ToSummaryDto(conv, now);
            }

            bool hadPendingWaitingPeriod = conv.CurrentWaitingPeriodId.HasValue;

            var pendingEscalations = await db.SupportEscalationLogs
                .Where(l => l.ConversationId == conversationId &&
                            (l.Status == SupportEscalationStatus.Pending ||
                             l.Status == SupportEscalationStatus.Failed ||
                             l.Status == SupportEscalationStatus.Processing))
                .ToListAsync(cancellationToken);

            foreach (var log in pendingEscalations)
            {
                log.Status = SupportEscalationStatus.Cancelled;
                log.ClaimedAtUtc = null;
                log.NextRetryAtUtc = null;
                log.LastError = $"Cancelled when conversation was resolved by {actorType}.";
            }

            conv.Status = SupportConversationStatus.Resolved;
            conv.ResolvedAtUtc = now;
            conv.ResolvedBy = actorName;
            conv.AwaitingSupportSinceUtc = null;
            conv.CurrentWaitingPeriodId = null;
            conv.EscalationDeadlineUtc = null;
            conv.IsEscalated = false;
            conv.EscalatedAtUtc = null;
            conv.UpdatedAtUtc = now;
            conv.LastActivityAtUtc = now;
            conv.Version += 1;

            if (string.Equals(actorType, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                conv.SuperAdminUnreadCount = 0;
                conv.SuperAdminLastReadAtUtc = now;
            }
            else
            {
                conv.RestaurantUnreadCount = 0;
                conv.RestaurantLastReadAtUtc = now;
            }

            db.SupportAuditLogs.Add(new SupportAuditLog
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                ActorType = actorType,
                ActorName = actorName,
                Action = "Resolved",
                Details = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                TimestampUtc = now
            });

            if (hadPendingWaitingPeriod || pendingEscalations.Count > 0)
            {
                db.SupportAuditLogs.Add(new SupportAuditLog
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    ActorType = "System",
                    ActorName = "EscalationScheduler",
                    Action = "EscalationCancelled",
                    Details = "Cancelled upon conversation resolution.",
                    TimestampUtc = now
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            if (tx != null) await tx.CommitAsync(cancellationToken);

            var dto = ToSummaryDto(conv, now);
            await PublishEventAsync("ConversationResolved", dto, null, null, null, now, cancellationToken);
            return dto;
        }

        public async Task<SupportSyncResponseDto> GetSuperAdminInboxAsync(
            string? statusFilter = null,
            string? search = null,
            bool? escalatedOnly = null,
            DateTime? sinceUtc = null,
            CancellationToken cancellationToken = default)
        {
            var now = NowUtc;
            var query = db.SupportConversations
                .AsNoTracking()
                .Include(c => c.Restaurant)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(statusFilter) && !string.Equals(statusFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(statusFilter, "Escalated", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.IsEscalated && c.Status == SupportConversationStatus.AwaitingSupport);
                }
                else if (Enum.TryParse<SupportConversationStatus>(statusFilter, true, out var parsedStatus))
                {
                    query = query.Where(c => c.Status == parsedStatus);
                }
            }

            if (escalatedOnly == true)
            {
                query = query.Where(c => c.IsEscalated && c.Status == SupportConversationStatus.AwaitingSupport);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                query = query.Where(c =>
                    c.Subject.ToLower().Contains(term) ||
                    c.Restaurant.Name.ToLower().Contains(term) ||
                    (c.LastMessagePreview != null && c.LastMessagePreview.ToLower().Contains(term)));
            }

            if (sinceUtc.HasValue)
            {
                query = query.Where(c => c.UpdatedAtUtc >= sinceUtc.Value);
            }

            var items = await query
                .OrderByDescending(c => c.IsEscalated)
                .ThenBy(c => c.Status == SupportConversationStatus.Resolved ? 1 : 0)
                .ThenByDescending(c => c.LastActivityAtUtc)
                .ToListAsync(cancellationToken);

            var totalUnread = await db.SupportConversations
                .AsNoTracking()
                .Where(c => c.SuperAdminUnreadCount > 0)
                .SumAsync(c => c.SuperAdminUnreadCount, cancellationToken);

            return new SupportSyncResponseDto
            {
                ServerUtc = now,
                Conversations = items.Select(c => ToSummaryDto(c, now)).ToList(),
                TotalUnreadCount = totalUnread
            };
        }

        public async Task<SupportConversationMutationResultDto> AddSuperAdminReplyAsync(
            Guid conversationId,
            string actorName,
            string actorEmail,
            CreateSupportMessageDto request,
            CancellationToken cancellationToken = default)
        {
            var body = request.Message?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(body))
                throw new ArgumentException("Reply message cannot be empty.");

            await using var tx = await TableOrderTransaction.BeginAsync(db);
            using var memLock = await AcquireConversationLockAsync(conversationId, cancellationToken);

            var now = NowUtc;
            var conv = await db.SupportConversations
                .Include(c => c.Restaurant)
                .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
                ?? throw new KeyNotFoundException("Support conversation not found.");

            if (request.ClientOperationId is Guid opId && opId != Guid.Empty)
            {
                var dup = await db.SupportMessages.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.ClientOperationId == opId, cancellationToken);
                if (dup != null)
                {
                    return new SupportConversationMutationResultDto
                    {
                        Conversation = ToSummaryDto(conv, now),
                        Message = ToMessageDto(dup),
                        Deduplicated = true
                    };
                }
            }

            var nextSeq = (await db.SupportMessages
                .Where(m => m.ConversationId == conversationId)
                .MaxAsync(m => (long?)m.SequenceNumber, cancellationToken) ?? 0) + 1;

            var message = new SupportMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                SenderType = SupportSenderType.SuperAdmin,
                SenderEmployeeId = null,
                SenderName = actorName,
                SenderEmail = actorEmail,
                Message = body,
                CreatedAtUtc = now,
                SequenceNumber = nextSeq,
                ClientOperationId = request.ClientOperationId
            };
            db.SupportMessages.Add(message);

            bool hadActiveWaitingPeriod = conv.CurrentWaitingPeriodId.HasValue ||
                                          conv.Status == SupportConversationStatus.AwaitingSupport;

            var activeLogs = await db.SupportEscalationLogs
                .Where(l => l.ConversationId == conversationId &&
                            (l.Status == SupportEscalationStatus.Pending ||
                             l.Status == SupportEscalationStatus.Failed ||
                             l.Status == SupportEscalationStatus.Processing))
                .ToListAsync(cancellationToken);

            foreach (var log in activeLogs)
            {
                log.Status = SupportEscalationStatus.Cancelled;
                log.ClaimedAtUtc = null;
                log.NextRetryAtUtc = null;
                log.LastError = "Cancelled by Super Admin reply.";
            }

            conv.Status = SupportConversationStatus.AwaitingRestaurant;
            conv.ResolvedAtUtc = null;
            conv.ResolvedBy = null;
            conv.AwaitingSupportSinceUtc = null;
            conv.CurrentWaitingPeriodId = null;
            conv.EscalationDeadlineUtc = null;
            conv.IsEscalated = false;
            conv.EscalatedAtUtc = null;
            conv.SuperAdminUnreadCount = 0;
            conv.SuperAdminLastReadAtUtc = now;
            conv.RestaurantUnreadCount += 1;
            conv.UpdatedAtUtc = now;
            conv.LastActivityAtUtc = now;
            conv.LastMessagePreview = TruncatePreview(body);
            conv.LastMessageSenderType = SupportSenderType.SuperAdmin;
            conv.LastMessageId = message.Id;
            conv.Version += 1;

            db.SupportAuditLogs.Add(new SupportAuditLog
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                ActorType = "SuperAdmin",
                ActorName = actorName,
                Action = "SuperAdminReplied",
                TimestampUtc = now
            });

            if (hadActiveWaitingPeriod || activeLogs.Count > 0)
            {
                db.SupportAuditLogs.Add(new SupportAuditLog
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    ActorType = "System",
                    ActorName = "EscalationScheduler",
                    Action = "EscalationCancelled",
                    Details = "Waiting period ended by Super Admin reply.",
                    TimestampUtc = now
                });
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                if (tx != null) await tx.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException) when (request.ClientOperationId is Guid retryOp && retryOp != Guid.Empty)
            {
                if (tx != null) await tx.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                var currentConv = await db.SupportConversations.AsNoTracking().Include(c => c.Restaurant)
                    .FirstAsync(c => c.Id == conversationId, cancellationToken);
                var existingMsg = await db.SupportMessages.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.ClientOperationId == retryOp, cancellationToken);
                if (existingMsg != null)
                {
                    return new SupportConversationMutationResultDto
                    {
                        Conversation = ToSummaryDto(currentConv, now),
                        Message = ToMessageDto(existingMsg),
                        Deduplicated = true
                    };
                }
                throw;
            }

            var summaryDto = ToSummaryDto(conv, now);
            var msgDto = ToMessageDto(message);
            await PublishEventAsync("MessageCreated", summaryDto, msgDto, null, null, now, cancellationToken);

            return new SupportConversationMutationResultDto
            {
                Conversation = summaryDto,
                Message = msgDto,
                Deduplicated = false
            };
        }

        public async Task<SupportConversationSummaryDto?> MarkSuperAdminReadAsync(
            Guid conversationId,
            string actorName,
            CancellationToken cancellationToken = default)
        {
            await using var tx = await TableOrderTransaction.BeginAsync(db);
            using var memLock = await AcquireConversationLockAsync(conversationId, cancellationToken);

            var now = NowUtc;
            var conv = await db.SupportConversations
                .Include(c => c.Restaurant)
                .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
            if (conv == null) return null;

            bool changed = conv.SuperAdminUnreadCount > 0;
            conv.SuperAdminUnreadCount = 0;
            conv.SuperAdminLastReadAtUtc = now;
            if (changed)
            {
                conv.UpdatedAtUtc = now;
                conv.Version += 1;
            }

            await db.SaveChangesAsync(cancellationToken);
            if (tx != null) await tx.CommitAsync(cancellationToken);

            var dto = ToSummaryDto(conv, now);
            if (changed)
            {
                await PublishEventAsync("ConversationRead", dto, null, null, "SuperAdmin", now, cancellationToken);
            }
            return dto;
        }

        private async Task PublishEventAsync(
            string eventType,
            SupportConversationSummaryDto conv,
            SupportMessageDto? message,
            SupportEscalationLogDto? escalation,
            string? readByRole,
            DateTime now,
            CancellationToken ct)
        {
            try
            {
                await realtime.PublishAsync(new SupportRealtimeEventDto
                {
                    EventId = Guid.NewGuid(),
                    EventType = eventType,
                    OccurredAtUtc = now,
                    ConversationId = conv.Id,
                    RestaurantId = conv.RestaurantId,
                    ConversationVersion = conv.Version,
                    Conversation = conv,
                    Message = message,
                    Escalation = escalation,
                    ReadByRole = readByRole
                }, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Support real-time dispatch failed after commit for event {EventType} ({ConversationId})", eventType, conv.Id);
            }
        }

        public static SupportConversationSummaryDto ToSummaryDto(SupportConversation c, DateTime now)
        {
            double? waitSeconds = null;
            if (c.Status == SupportConversationStatus.AwaitingSupport && c.AwaitingSupportSinceUtc.HasValue)
            {
                waitSeconds = Math.Max(0, (now - c.AwaitingSupportSinceUtc.Value).TotalSeconds);
            }

            return new SupportConversationSummaryDto
            {
                Id = c.Id,
                RestaurantId = c.RestaurantId,
                RestaurantName = c.Restaurant?.Name ?? "Restaurant",
                Subject = c.Subject,
                Status = c.Status.ToString(),
                CreatedAtUtc = c.CreatedAtUtc,
                UpdatedAtUtc = c.UpdatedAtUtc,
                LastActivityAtUtc = c.LastActivityAtUtc,
                ResolvedAtUtc = c.ResolvedAtUtc,
                ResolvedBy = c.ResolvedBy,
                AwaitingSupportSinceUtc = c.AwaitingSupportSinceUtc,
                CurrentWaitingPeriodId = c.CurrentWaitingPeriodId,
                EscalationDeadlineUtc = c.EscalationDeadlineUtc,
                IsEscalated = c.IsEscalated,
                EscalatedAtUtc = c.EscalatedAtUtc,
                RestaurantUnreadCount = c.RestaurantUnreadCount,
                SuperAdminUnreadCount = c.SuperAdminUnreadCount,
                LastMessagePreview = c.LastMessagePreview,
                LastMessageSenderType = c.LastMessageSenderType?.ToString(),
                LastMessageId = c.LastMessageId,
                Version = c.Version,
                WaitingDurationSeconds = waitSeconds
            };
        }

        public static SupportMessageDto ToMessageDto(SupportMessage m) => new()
        {
            Id = m.Id,
            ConversationId = m.ConversationId,
            SenderType = m.SenderType.ToString(),
            SenderEmployeeId = m.SenderEmployeeId,
            SenderName = m.SenderName,
            SenderEmail = m.SenderEmail,
            Message = m.Message,
            CreatedAtUtc = m.CreatedAtUtc,
            SequenceNumber = m.SequenceNumber,
            ClientOperationId = m.ClientOperationId
        };

        public static SupportEscalationLogDto ToEscalationDto(SupportEscalationLog e) => new()
        {
            Id = e.Id,
            WaitingPeriodId = e.WaitingPeriodId,
            EscalationTargetEmail = e.EscalationTargetEmail,
            ScheduledDeadlineUtc = e.ScheduledDeadlineUtc,
            DispatchedAtUtc = e.DispatchedAtUtc,
            AttemptCount = e.AttemptCount,
            LastAttemptAtUtc = e.LastAttemptAtUtc,
            NextRetryAtUtc = e.NextRetryAtUtc,
            Status = e.Status.ToString(),
            LastError = e.LastError,
            ProviderMessageId = e.ProviderMessageId
        };

        public static SupportAuditLogDto ToAuditDto(SupportAuditLog a) => new()
        {
            Id = a.Id,
            ActorType = a.ActorType,
            ActorName = a.ActorName,
            Action = a.Action,
            Details = a.Details,
            TimestampUtc = a.TimestampUtc
        };

        private static string TruncatePreview(string body)
        {
            var singleLine = body.Replace("\r", " ").Replace("\n", " ").Trim();
            return singleLine.Length <= 240 ? singleLine : singleLine[..237] + "...";
        }
    }
}
