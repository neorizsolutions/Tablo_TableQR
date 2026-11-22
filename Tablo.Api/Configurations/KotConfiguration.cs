using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class KotBatchConfiguration : IEntityTypeConfiguration<KotBatch>
{
    public void Configure(EntityTypeBuilder<KotBatch> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.KotNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(b => b.TriggeredBy)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(b => b.Restaurant)
            .WithMany()
            .HasForeignKey(b => b.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Order)
            .WithMany()
            .HasForeignKey(b => b.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => new { b.RestaurantId, b.OrderId });
        builder.HasIndex(b => new { b.RestaurantId, b.KotNumber });
    }
}

public class KotTicketConfiguration : IEntityTypeConfiguration<KotTicket>
{
    public void Configure(EntityTypeBuilder<KotTicket> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TicketNumber)
            .IsRequired()
            .HasMaxLength(60);

        builder.Property(t => t.StationNameSnapshot)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.TicketType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.TimestampIstFormatted)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasOne(t => t.Restaurant)
            .WithMany()
            .HasForeignKey(t => t.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.KotBatch)
            .WithMany(b => b.Tickets)
            .HasForeignKey(t => t.KotBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.KitchenStation)
            .WithMany(s => s.KotTickets)
            .HasForeignKey(t => t.KitchenStationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Printer)
            .WithMany()
            .HasForeignKey(t => t.PrinterId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.OriginalTicket)
            .WithMany()
            .HasForeignKey(t => t.OriginalTicketId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => new { t.RestaurantId, t.OrderId });
        builder.HasIndex(t => new { t.RestaurantId, t.KitchenStationId, t.CreatedAtUtc });
    }
}

public class PrintJobConfiguration : IEntityTypeConfiguration<PrintJob>
{
    public void Configure(EntityTypeBuilder<PrintJob> builder)
    {
        builder.HasKey(j => j.Id);

        builder.Property(j => j.JobType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(j => j.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(j => j.ClaimToken)
            .HasMaxLength(64);

        builder.Property(j => j.DeliveryState)
            .HasMaxLength(30)
            .HasDefaultValue("Certain");

        builder.Property(j => j.UncertainReason)
            .HasMaxLength(500);

        builder.Property(j => j.ConcurrencyToken)
            .IsConcurrencyToken();

        builder.HasOne(j => j.Restaurant)
            .WithMany()
            .HasForeignKey(j => j.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(j => j.KotTicket)
            .WithMany(t => t.PrintJobs)
            .HasForeignKey(j => j.KotTicketId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(j => j.Printer)
            .WithMany(p => p.PrintJobs)
            .HasForeignKey(j => j.PrinterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(j => new { j.RestaurantId, j.PrinterId, j.Status });
        builder.HasIndex(j => new { j.PrinterId, j.Status, j.CreatedAtUtc });
        builder.HasIndex(j => new { j.RestaurantId, j.Status, j.LeaseExpiresAtUtc });
    }
}
