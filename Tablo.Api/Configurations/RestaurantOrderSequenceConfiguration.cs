using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations
{
    public class RestaurantOrderSequenceConfiguration : IEntityTypeConfiguration<RestaurantOrderSequence>
    {
        public void Configure(EntityTypeBuilder<RestaurantOrderSequence> builder)
        {
            builder.ToTable("RestaurantOrderSequences");
            builder.HasKey(s => s.RestaurantId);

            builder.Property(s => s.NextOrderNumber).IsRequired().HasDefaultValue(1);

            builder.HasOne(s => s.Restaurant)
                .WithOne() // Restaurant has no navigation property to the sequence
                .HasForeignKey<RestaurantOrderSequence>(s => s.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
