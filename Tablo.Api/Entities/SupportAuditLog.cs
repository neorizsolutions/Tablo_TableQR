using System;
using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities
{
    public class SupportAuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ConversationId { get; set; }
        public SupportConversation Conversation { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string ActorType { get; set; } = string.Empty; // "Restaurant", "SuperAdmin", "SystemWorker"

        [Required]
        [MaxLength(150)]
        public string ActorName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Action { get; set; } = string.Empty; // "Created", "RestaurantReplied", "SuperAdminReplied", "Resolved", "Reopened", "EscalationScheduled", "Escalated", "EscalationCancelled"

        [MaxLength(1000)]
        public string? Details { get; set; }

        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
