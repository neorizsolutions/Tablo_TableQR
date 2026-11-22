using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class KitchenStationConfiguration : IEntityTypeConfiguration<KitchenStation>
{
    public void Configure(EntityTypeBuilder<KitchenStation> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasOne(s => s.Restaurant)
            .WithMany()
            .HasForeignKey(s => s.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Printer)
            .WithMany(p => p.Stations)
            .HasForeignKey(s => s.PrinterId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(s => new { s.RestaurantId, s.Code })
            .IsUnique();

        builder.HasIndex(s => new { s.RestaurantId, s.IsDefault });
    }
}
