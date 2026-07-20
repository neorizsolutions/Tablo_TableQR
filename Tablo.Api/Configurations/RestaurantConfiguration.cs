using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations
{
    public class RestaurantConfiguration : IEntityTypeConfiguration<Restaurant>
    {
        public void Configure(EntityTypeBuilder<Restaurant> builder)
        {
            builder.ToTable("Restaurants");
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Name).IsRequired().HasMaxLength(255);
            builder.Property(r => r.Description).HasMaxLength(1000);
            builder.Property(r => r.Address).HasMaxLength(500);
            builder.Property(r => r.ContactEmail).IsRequired().HasMaxLength(255);
            builder.Property(r => r.ContactPhone).HasMaxLength(50);
            
            builder.Property(r => r.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(r => r.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
