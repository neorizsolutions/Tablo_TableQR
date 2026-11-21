using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations
{
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("Orders");
            builder.HasKey(o => o.Id);
            builder.HasOne(o => o.TableBill).WithMany(b => b.Orders)
                .HasForeignKey(o => new { o.RestaurantId, o.TableBillId })
                .HasPrincipalKey(b => new { b.RestaurantId, b.Id })
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(o=>o.GroupLabel).HasMaxLength(8);
            builder.HasOne<DiningGroup>().WithMany().HasForeignKey(o=>new{o.RestaurantId,o.DiningGroupId}).HasPrincipalKey(g=>new{g.RestaurantId,g.Id}).OnDelete(DeleteBehavior.Restrict);
            builder.Property(o => o.OrderNumber).IsRequired();
            builder.Property(o => o.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Placed");
            builder.Property(o => o.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(o => o.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(o => o.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(o => o.OrderChannel).HasMaxLength(30).HasDefaultValue("DineIn");
            builder.Property(o => o.OrderSource).HasMaxLength(30);
            builder.Property(o => o.IsScheduled).HasDefaultValue(false);
            builder.Property(o => o.ScheduledPickupTimeUtc);
            builder.Property(o => o.ScheduledPickupTimeLocal).HasMaxLength(50);
            builder.Property(o => o.PreparationLeadMinutes).HasDefaultValue(15);
            builder.Property(o => o.ScheduledReleaseTimeUtc);
            builder.Property(o => o.VehiclePlate).HasMaxLength(30);
            builder.Property(o => o.VehicleModel).HasMaxLength(100);
            builder.Property(o => o.ParkingBay).HasMaxLength(30);
            builder.Property(o => o.DeliveredAtUtc);
            builder.Property(o => o.DeliveredByEmployeeId);
            builder.Property(o => o.BusinessDate).HasMaxLength(10);
            builder.Property(o => o.CustomerName).HasMaxLength(100);
            builder.Property(o => o.CustomerPhone).HasMaxLength(20);
            builder.Property(o => o.SubmissionOperationId).HasMaxLength(64);
            builder.Property(o => o.SubmissionFingerprint).HasMaxLength(64);
            builder.Property(o => o.KotPrintStatus).HasMaxLength(50).HasDefaultValue("None");
            builder.Property(o => o.KotPrintError).HasMaxLength(500);
            builder.Property(o => o.PackagingCharge).HasColumnType("decimal(18,2)").HasDefaultValue(0m);

            builder.HasIndex(o => o.RestaurantId);
            builder.HasIndex(o => new { o.RestaurantId, o.Status });
            builder.HasIndex(o => new { o.RestaurantId, o.CreatedAt });
            builder.HasIndex(o => new { o.RestaurantId, o.OrderNumber }).IsUnique();
            builder.HasIndex(o => new { o.RestaurantId, o.BusinessDate, o.DailyTokenNumber });
            builder.HasIndex(o => new { o.RestaurantId, o.Status, o.ScheduledReleaseTimeUtc });
            builder.HasIndex(o => new { o.RestaurantId, o.OrderChannel, o.Status });
            builder.HasIndex(o => new { o.RestaurantId, o.SubmissionOperationId }).IsUnique().HasFilter("\"SubmissionOperationId\" IS NOT NULL");
            builder.HasIndex(o => new { o.RestaurantId, o.OutletId, o.DeliveryProvider, o.NormalizedExternalReference })
                .IsUnique()
                .HasFilter("\"NormalizedExternalReference\" IS NOT NULL");

            builder.HasOne(o => o.Outlet)
                .WithMany()
                .HasForeignKey(o => o.OutletId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(o => o.Restaurant)
                .WithMany(r => r.Orders)
                .HasForeignKey(o => o.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(o => o.Table)
                .WithMany(t => t.Orders)
                .HasForeignKey(o => o.TableId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
