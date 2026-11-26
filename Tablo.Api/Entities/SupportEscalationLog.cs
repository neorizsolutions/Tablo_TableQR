using System;
using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities
{
    public enum SupportEscalationStatus
    {
        Pending = 0,
        Dispatched = 1,
        Failed = 2,
        Cancelled = 3,
        Processing = 4
    }

    public class SupportEscalationLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ConversationId { get; set; }
        public SupportConversation Conversation { get; set; } = null!;

        public Guid WaitingPeriodId { get; set; }

        [Required]
        [MaxLength(255)]
        public string EscalationTargetEmail { get; set; } = "support@neorizsolutions.com";

        public DateTime ScheduledDeadlineUtc { get; set; }
        public DateTime? DispatchedAtUtc { get; set; }

        public int AttemptCount { get; set; } = 0;
        public DateTime? LastAttemptAtUtc { get; set; }
        public DateTime? NextRetryAtUtc { get; set; }

        public DateTime? ClaimedAtUtc { get; set; }
        [MaxLength(100)]
        public string? ClaimedBy { get; set; }

        public SupportEscalationStatus Status { get; set; } = SupportEscalationStatus.Pending;

        [MaxLength(1000)]
        public string? LastError { get; set; }

        [MaxLength(150)]
        public string? ProviderMessageId { get; set; }
    }
}
