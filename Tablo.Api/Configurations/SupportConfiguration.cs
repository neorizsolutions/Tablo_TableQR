using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class SupportConversationConfiguration : IEntityTypeConfiguration<SupportConversation>
{
    public void Configure(EntityTypeBuilder<SupportConversation> b)
    {
        b.ToTable("SupportConversations");
        b.HasKey(c => c.Id);

        b.Property(c => c.Subject).IsRequired().HasMaxLength(200);
        b.Property(c => c.ResolvedBy).HasMaxLength(150);
        b.Property(c => c.LastMessagePreview).HasMaxLength(300);
        b.Property(c => c.IsEscalated).HasDefaultValue(false);
        b.Property(c => c.RestaurantUnreadCount).HasDefaultValue(0);
        b.Property(c => c.SuperAdminUnreadCount).HasDefaultValue(0);
        b.Property(c => c.Version).HasDefaultValue(1L);

        b.HasIndex(c => c.RestaurantId);
        b.HasIndex(c => c.Status);
        b.HasIndex(c => c.EscalationDeadlineUtc);
        b.HasIndex(c => new { c.RestaurantId, c.LastActivityAtUtc });
        b.HasIndex(c => new { c.RestaurantId, c.ClientOperationId })
            .IsUnique()
            .HasFilter("\"ClientOperationId\" IS NOT NULL");

        b.HasOne(c => c.Restaurant)
            .WithMany()
            .HasForeignKey(c => c.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(c => c.CreatedByEmployee)
            .WithMany()
            .HasForeignKey(c => new { c.RestaurantId, c.CreatedByEmployeeId })
            .HasPrincipalKey(e => new { e.RestaurantId, e.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupportMessageConfiguration : IEntityTypeConfiguration<SupportMessage>
{
    public void Configure(EntityTypeBuilder<SupportMessage> b)
    {
        b.ToTable("SupportMessages");
        b.HasKey(m => m.Id);

        b.Property(m => m.SenderName).IsRequired().HasMaxLength(150);
        b.Property(m => m.SenderEmail).IsRequired().HasMaxLength(255);
        b.Property(m => m.Message).IsRequired().HasMaxLength(4000);

        b.HasIndex(m => m.ConversationId);
        b.HasIndex(m => m.ClientOperationId);
        b.HasIndex(m => new { m.ConversationId, m.SequenceNumber }).IsUnique();
        b.HasIndex(m => new { m.ConversationId, m.ClientOperationId })
            .IsUnique()
            .HasFilter("\"ClientOperationId\" IS NOT NULL");

        b.HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(m => m.SenderEmployee)
            .WithMany()
            .HasForeignKey(m => m.SenderEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupportEscalationLogConfiguration : IEntityTypeConfiguration<SupportEscalationLog>
{
    public void Configure(EntityTypeBuilder<SupportEscalationLog> b)
    {
        b.ToTable("SupportEscalationLogs");
        b.HasKey(l => l.Id);

        b.Property(l => l.EscalationTargetEmail).IsRequired().HasMaxLength(255);
        b.Property(l => l.AttemptCount).HasDefaultValue(0);
        b.Property(l => l.ClaimedBy).HasMaxLength(100);
        b.Property(l => l.LastError).HasMaxLength(1000);
        b.Property(l => l.ProviderMessageId).HasMaxLength(150);

        b.HasIndex(l => l.ConversationId);
        b.HasIndex(l => l.WaitingPeriodId);
        b.HasIndex(l => new { l.ConversationId, l.WaitingPeriodId }).IsUnique();
        b.HasIndex(l => new { l.Status, l.ScheduledDeadlineUtc });

        b.HasOne(l => l.Conversation)
            .WithMany(c => c.EscalationLogs)
            .HasForeignKey(l => l.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SupportAuditLogConfiguration : IEntityTypeConfiguration<SupportAuditLog>
{
    public void Configure(EntityTypeBuilder<SupportAuditLog> b)
    {
        b.ToTable("SupportAuditLogs");
        b.HasKey(a => a.Id);

        b.Property(a => a.ActorType).IsRequired().HasMaxLength(50);
        b.Property(a => a.ActorName).IsRequired().HasMaxLength(150);
        b.Property(a => a.Action).IsRequired().HasMaxLength(100);
        b.Property(a => a.Details).HasMaxLength(1000);

        b.HasIndex(a => a.ConversationId);
        b.HasIndex(a => new { a.ConversationId, a.TimestampUtc });

        b.HasOne(a => a.Conversation)
            .WithMany(c => c.AuditLogs)
            .HasForeignKey(a => a.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
