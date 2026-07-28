using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations
{
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("Orders");
            builder.HasKey(o => o.Id);

            builder.Property(o => o.OrderNumber).IsRequired();
            builder.Property(o => o.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Placed");
            builder.Property(o => o.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(o => o.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(o => o.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.HasIndex(o => o.RestaurantId);
            builder.HasIndex(o => new { o.RestaurantId, o.Status });
            builder.HasIndex(o => new { o.RestaurantId, o.CreatedAt });
            builder.HasIndex(o => new { o.RestaurantId, o.OrderNumber }).IsUnique();

            builder.HasOne(o => o.Restaurant)
                .WithMany(r => r.Orders)
                .HasForeignKey(o => o.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(o => o.Table)
                .WithMany(t => t.Orders)
                .HasForeignKey(o => o.TableId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
