using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations
{
    public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
    {
        public void Configure(EntityTypeBuilder<MenuItem> builder)
        {
            builder.ToTable("MenuItems");
            builder.HasKey(m => m.Id);

            builder.Property(m => m.Name).IsRequired().HasMaxLength(255);
            builder.Property(m => m.Description).HasMaxLength(1000);
            builder.Property(m => m.Price).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(m => m.PackagingFee).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            builder.Property(m => m.ImageUrl).HasMaxLength(2000);
            builder.Property(m => m.IsAvailable).HasDefaultValue(true);
            builder.Property(m => m.IsRefillEligible).HasDefaultValue(false);

            builder.HasIndex(m => m.RestaurantId);

            builder.HasOne(m => m.Restaurant)
                .WithMany(r => r.MenuItems)
                .HasForeignKey(m => m.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(m => m.Category)
                .WithMany(c => c.MenuItems)
                .HasForeignKey(m => m.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(m => m.KitchenStation)
                .WithMany(s => s.MenuItems)
                .HasForeignKey(m => m.KitchenStationId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(m => m.RefillKitchenStation)
                .WithMany()
                .HasForeignKey(m => m.RefillKitchenStationId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
