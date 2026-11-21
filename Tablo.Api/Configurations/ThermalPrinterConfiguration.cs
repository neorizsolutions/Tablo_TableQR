using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class ThermalPrinterConfiguration : IEntityTypeConfiguration<ThermalPrinter>
{
    public void Configure(EntityTypeBuilder<ThermalPrinter> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.ConnectionType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(p => p.Restaurant)
            .WithMany()
            .HasForeignKey(p => p.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.RestaurantId, p.IsActive });
    }
}
