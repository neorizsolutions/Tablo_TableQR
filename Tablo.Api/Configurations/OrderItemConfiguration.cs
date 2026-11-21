using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations
{
    public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.ToTable("OrderItems");
            builder.HasKey(oi => oi.Id);

            builder.Property(oi => oi.Quantity).IsRequired();
            builder.Property(oi => oi.UnitPrice).HasColumnType("decimal(18,2)").IsRequired().HasDefaultValue(0m);
            builder.Property(oi => oi.ItemNameSnapshot).HasMaxLength(255).IsRequired().HasDefaultValue("");
            builder.Property(oi => oi.SpecialInstructions).HasMaxLength(500);
            builder.Property(oi => oi.ReleaseState).HasMaxLength(30).IsRequired().HasDefaultValue("Fired");
            builder.Property(oi => oi.PackingIntent).HasMaxLength(30).IsRequired().HasDefaultValue("DineIn");
            builder.Property(oi => oi.PackagingFeeApplied).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            builder.Property(oi => oi.IsPackagingWaived).HasDefaultValue(false);
            builder.Property(oi => oi.PackagingWaiverReason).HasMaxLength(200);
            builder.Property(oi => oi.PackagingWaivedBy).HasMaxLength(100);
            builder.Property(oi => oi.CourseName).HasMaxLength(50);
            builder.Property(oi => oi.FireOperationId).HasMaxLength(64);
            builder.Property(oi => oi.IsRefill).HasDefaultValue(false);
            builder.Property(oi => oi.RefillCount).HasDefaultValue(0);
            builder.Property(oi => oi.CancelledQuantity).HasDefaultValue(0);
            builder.Property(oi => oi.IsCancelled).HasDefaultValue(false);

            builder.HasIndex(oi => new { oi.OrderId, oi.ReleaseState });

            builder.HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(oi => oi.MenuItem)
                .WithMany(m => m.OrderItems)
                .HasForeignKey(oi => oi.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
