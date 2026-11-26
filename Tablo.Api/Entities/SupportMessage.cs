using System;
using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities
{
    public enum SupportSenderType
    {
        Restaurant = 0,
        SuperAdmin = 1
    }

    public class SupportMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ConversationId { get; set; }
        public SupportConversation Conversation { get; set; } = null!;

        public SupportSenderType SenderType { get; set; }

        public Guid? SenderEmployeeId { get; set; }
        public Employee? SenderEmployee { get; set; }

        [Required]
        [MaxLength(150)]
        public string SenderName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string SenderEmail { get; set; } = string.Empty;

        [Required]
        [MaxLength(4000)]
        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public long SequenceNumber { get; set; }

        public Guid? ClientOperationId { get; set; }
    }
}
