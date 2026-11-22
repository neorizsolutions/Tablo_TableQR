using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations
{
    public class RestaurantSettingsConfiguration : IEntityTypeConfiguration<RestaurantSettings>
    {
        public void Configure(EntityTypeBuilder<RestaurantSettings> builder)
        {
            builder.ToTable("RestaurantSettings");
            builder.HasKey(rs => rs.RestaurantId);

            builder.Property(rs => rs.IsAcceptingOrders)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(rs => rs.ParcelPackagingChargeMode)
                .HasMaxLength(30)
                .HasDefaultValue("None");

            builder.Property(rs => rs.ParcelPackagingFlatRate)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0m);

            builder.Property(rs => rs.ParcelPackagingPercentage)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0m);

            builder.Property(rs => rs.ParcelPackagingTaxable)
                .HasDefaultValue(false);

            builder.Property(rs => rs.ParcelPackagingTaxRate)
                .HasColumnType("decimal(18,2)");

            // Scheduled Pickup Settings
            builder.Property(rs => rs.ScheduledPickupEnabled)
                .HasDefaultValue(false);
            builder.Property(rs => rs.DefaultPreparationLeadMinutes)
                .HasDefaultValue(15);

            // Car-Side Ordering Settings
            builder.Property(rs => rs.CarSideOrderingEnabled)
                .HasDefaultValue(false);

            // Hybrid Dine-In / Parcel Settings
            builder.Property(rs => rs.HybridDineInParcelEnabled)
                .HasDefaultValue(false);
            builder.Property(rs => rs.LeftoverContainerCharge)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0m);

            // Dynamic UPI QR Settings
            builder.Property(rs => rs.UpiQrEnabled)
                .HasDefaultValue(false);
            builder.Property(rs => rs.UpiVpa)
                .HasMaxLength(100);
            builder.Property(rs => rs.UpiPayeeName)
                .HasMaxLength(100);
            builder.Property(rs => rs.UpiMerchantCode)
                .HasMaxLength(30);

            // 1:1 relationship with Restaurant
            builder.HasOne(rs => rs.Restaurant)
                .WithOne(r => r.Settings)
                .HasForeignKey<RestaurantSettings>(rs => rs.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(rs => rs.DefaultKitchenStation)
                .WithMany()
                .HasForeignKey(rs => rs.DefaultKitchenStationId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
