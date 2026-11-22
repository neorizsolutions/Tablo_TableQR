using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> b)
    {
        b.HasKey(i => i.Id);
        b.HasIndex(i => new { i.RestaurantId, i.Name });
        b.Property(i => i.Name).HasMaxLength(200).IsRequired();
        b.Property(i => i.BaseUnit).HasMaxLength(20).IsRequired();
        b.Property(i => i.LowStockThreshold).HasPrecision(18, 4);

        b.HasOne(i => i.Restaurant)
            .WithMany()
            .HasForeignKey(i => i.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
