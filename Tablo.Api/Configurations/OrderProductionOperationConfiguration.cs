using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class OrderProductionOperationConfiguration : IEntityTypeConfiguration<OrderProductionOperation>
{
    public void Configure(EntityTypeBuilder<OrderProductionOperation> builder)
    {
        builder.HasKey(o => o.Id);

        builder.HasIndex(o => new { o.RestaurantId, o.OperationId })
            .IsUnique();

        builder.HasIndex(o => new { o.RestaurantId, o.OrderId });

        builder.HasOne(o => o.Restaurant)
            .WithMany()
            .HasForeignKey(o => o.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Order)
            .WithMany()
            .HasForeignKey(o => o.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
