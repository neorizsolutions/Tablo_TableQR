using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities
{
    public enum SupportConversationStatus
    {
        AwaitingSupport = 0,
        AwaitingRestaurant = 1,
        Resolved = 2
    }

    public class SupportConversation
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid RestaurantId { get; set; }
        public Restaurant Restaurant { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        public SupportConversationStatus Status { get; set; } = SupportConversationStatus.AwaitingSupport;

        public Guid CreatedByEmployeeId { get; set; }
        public Employee CreatedByEmployee { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastActivityAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? ResolvedAtUtc { get; set; }
        [MaxLength(150)]
        public string? ResolvedBy { get; set; }

        public DateTime? AwaitingSupportSinceUtc { get; set; }
        public Guid? CurrentWaitingPeriodId { get; set; }
        public DateTime? EscalationDeadlineUtc { get; set; }
        public bool IsEscalated { get; set; } = false;
        public DateTime? EscalatedAtUtc { get; set; }

        public int RestaurantUnreadCount { get; set; } = 0;
        public int SuperAdminUnreadCount { get; set; } = 0;

        public Guid? ClientOperationId { get; set; }

        [MaxLength(300)]
        public string? LastMessagePreview { get; set; }

        public SupportSenderType? LastMessageSenderType { get; set; }
        public Guid? LastMessageId { get; set; }

        public DateTime? RestaurantLastReadAtUtc { get; set; }
        public DateTime? SuperAdminLastReadAtUtc { get; set; }

        public long Version { get; set; } = 1;

        public ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
        public ICollection<SupportEscalationLog> EscalationLogs { get; set; } = new List<SupportEscalationLog>();
        public ICollection<SupportAuditLog> AuditLogs { get; set; } = new List<SupportAuditLog>();
    }
}
