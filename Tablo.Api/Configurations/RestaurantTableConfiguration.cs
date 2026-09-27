using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations
{
    public class RestaurantTableConfiguration : IEntityTypeConfiguration<RestaurantTable>
    {
        public void Configure(EntityTypeBuilder<RestaurantTable> builder)
        {
            builder.ToTable("RestaurantTables");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.TableNumber).IsRequired();
            builder.HasIndex(t => new { t.RestaurantId, t.TableNumber }).IsUnique();
            builder.Property(t => t.QRToken).IsRequired().HasMaxLength(255);

            builder.HasIndex(t => t.QRToken).IsUnique();

            builder.HasOne(t => t.Restaurant)
                .WithMany(r => r.Tables)
                .HasForeignKey(t => t.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
