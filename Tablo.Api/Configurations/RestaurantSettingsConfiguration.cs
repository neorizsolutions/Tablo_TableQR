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

            // 1:1 relationship with Restaurant
            builder.HasOne(rs => rs.Restaurant)
                .WithOne(r => r.Settings)
                .HasForeignKey<RestaurantSettings>(rs => rs.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
