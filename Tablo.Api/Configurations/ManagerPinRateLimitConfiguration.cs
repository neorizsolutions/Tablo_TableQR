using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class ManagerPinRateLimitConfiguration : IEntityTypeConfiguration<ManagerPinRateLimit>
{
    public void Configure(EntityTypeBuilder<ManagerPinRateLimit> builder)
    {
        builder.HasKey(m => m.RestaurantId);

        builder.HasOne(m => m.Restaurant)
            .WithMany()
            .HasForeignKey(m => m.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
