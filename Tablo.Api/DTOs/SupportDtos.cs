using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs
{
    public class CreateSupportConversationDto
    {
        [Required]
        [MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [MaxLength(4000)]
        public string Message { get; set; } = string.Empty;

        public Guid? ClientOperationId { get; set; }
    }

    public class CreateSupportMessageDto
    {
        [Required]
        [MaxLength(4000)]
        public string Message { get; set; } = string.Empty;

        public Guid? ClientOperationId { get; set; }
    }

    public class ReopenSupportConversationDto
    {
        [MaxLength(4000)]
        public string? Message { get; set; }

        public Guid? ClientOperationId { get; set; }
    }

    public class ResolveSupportConversationDto
    {
        [MaxLength(500)]
        public string? Note { get; set; }
    }

    public class SupportMessageDto
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }
        public string SenderType { get; set; } = string.Empty;
        public Guid? SenderEmployeeId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public long SequenceNumber { get; set; }
        public Guid? ClientOperationId { get; set; }
    }

    public class SupportEscalationLogDto
    {
        public Guid Id { get; set; }
        public Guid WaitingPeriodId { get; set; }
        public string EscalationTargetEmail { get; set; } = string.Empty;
        public DateTime ScheduledDeadlineUtc { get; set; }
        public DateTime? DispatchedAtUtc { get; set; }
        public int AttemptCount { get; set; }
        public DateTime? LastAttemptAtUtc { get; set; }
        public DateTime? NextRetryAtUtc { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? LastError { get; set; }
        public string? ProviderMessageId { get; set; }
    }

    public class SupportAuditLogDto
    {
        public Guid Id { get; set; }
        public string ActorType { get; set; } = string.Empty;
        public string ActorName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime TimestampUtc { get; set; }
    }

    public class SupportConversationSummaryDto
    {
        public Guid Id { get; set; }
        public Guid RestaurantId { get; set; }
        public string RestaurantName { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime LastActivityAtUtc { get; set; }
        public DateTime? ResolvedAtUtc { get; set; }
        public string? ResolvedBy { get; set; }
        public DateTime? AwaitingSupportSinceUtc { get; set; }
        public Guid? CurrentWaitingPeriodId { get; set; }
        public DateTime? EscalationDeadlineUtc { get; set; }
        public bool IsEscalated { get; set; }
        public DateTime? EscalatedAtUtc { get; set; }
        public int RestaurantUnreadCount { get; set; }
        public int SuperAdminUnreadCount { get; set; }
        public string? LastMessagePreview { get; set; }
        public string? LastMessageSenderType { get; set; }
        public Guid? LastMessageId { get; set; }
        public long Version { get; set; }
        public double? WaitingDurationSeconds { get; set; }
    }

    public class SupportConversationDetailDto
    {
        public SupportConversationSummaryDto Conversation { get; set; } = null!;
        public List<SupportMessageDto> Messages { get; set; } = new();
        public List<SupportEscalationLogDto> EscalationLogs { get; set; } = new();
        public List<SupportAuditLogDto> AuditLogs { get; set; } = new();
    }

    public class SupportSyncResponseDto
    {
        public DateTime ServerUtc { get; set; }
        public List<SupportConversationSummaryDto> Conversations { get; set; } = new();
        public int TotalUnreadCount { get; set; }
    }

    public class SupportConversationMutationResultDto
    {
        public SupportConversationSummaryDto Conversation { get; set; } = null!;
        public SupportMessageDto? Message { get; set; }
        public bool Deduplicated { get; set; }
    }

    public class SupportRealtimeEventDto
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public string EventType { get; set; } = string.Empty;
        public DateTime OccurredAtUtc { get; set; }
        public Guid ConversationId { get; set; }
        public Guid RestaurantId { get; set; }
        public long ConversationVersion { get; set; }
        public SupportConversationSummaryDto? Conversation { get; set; }
        public SupportMessageDto? Message { get; set; }
        public SupportEscalationLogDto? Escalation { get; set; }
        public string? ReadByRole { get; set; }
    }
}
