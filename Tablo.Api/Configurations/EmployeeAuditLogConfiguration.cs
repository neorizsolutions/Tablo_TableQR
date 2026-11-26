using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

/// <summary>
/// Configures persistence and restricted foreign keys for EmployeeAuditLog so archived staff attribution remains intact.
/// </summary>
public class EmployeeAuditLogConfiguration : IEntityTypeConfiguration<EmployeeAuditLog>
{
    public void Configure(EntityTypeBuilder<EmployeeAuditLog> builder)
    {
        builder.ToTable("EmployeeAuditLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TargetEmail).IsRequired().HasMaxLength(255);
        builder.Property(x => x.TargetName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.TargetRole).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ActorEmail).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Reason).HasMaxLength(500);

        builder.HasIndex(x => new { x.RestaurantId, x.TimestampUtc });
        builder.HasIndex(x => x.TargetEmployeeId);

        builder.HasOne(x => x.Restaurant)
            .WithMany()
            .HasForeignKey(x => x.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TargetEmployee)
            .WithMany()
            .HasForeignKey(x => new { x.RestaurantId, x.TargetEmployeeId })
            .HasPrincipalKey(e => new { e.RestaurantId, e.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ActorEmployee)
            .WithMany()
            .HasForeignKey(x => new { x.RestaurantId, x.ActorEmployeeId })
            .HasPrincipalKey(e => new { e.RestaurantId, e.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
