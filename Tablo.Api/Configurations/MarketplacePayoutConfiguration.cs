using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class MarketplacePayoutConfiguration : IEntityTypeConfiguration<MarketplacePayout>
{
    public void Configure(EntityTypeBuilder<MarketplacePayout> builder)
    {
        builder.ToTable("MarketplacePayouts");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.DeliveryProvider).IsRequired().HasMaxLength(50);
        builder.Property(p => p.PayoutReference).IsRequired().HasMaxLength(100);
        builder.Property(p => p.GrossAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.CommissionDeducted).HasColumnType("decimal(18,2)");
        builder.Property(p => p.TcsGstDeducted).HasColumnType("decimal(18,2)");
        builder.Property(p => p.OtherAdjustments).HasColumnType("decimal(18,2)");
        builder.Property(p => p.NetAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.LinkedOrderIdsJson).HasMaxLength(4000);
        builder.Property(p => p.Notes).HasMaxLength(500);

        builder.HasIndex(p => new { p.RestaurantId, p.DeliveryProvider, p.PayoutReference }).IsUnique();

        builder.HasOne(p => p.Restaurant)
            .WithMany()
            .HasForeignKey(p => p.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Outlet)
            .WithMany()
            .HasForeignKey(p => p.OutletId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.RecordedByEmployee)
            .WithMany()
            .HasForeignKey(p => p.RecordedByEmployeeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
