using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class RecipeItemConfiguration : IEntityTypeConfiguration<RecipeItem>
{
    public void Configure(EntityTypeBuilder<RecipeItem> b)
    {
        b.HasKey(r => r.Id);
        b.HasIndex(r => new { r.RestaurantId, r.MenuItemId, r.IngredientId }).IsUnique();
        b.HasIndex(r => new { r.RestaurantId, r.MenuItemId });

        b.Property(r => r.UsableQuantity).HasPrecision(18, 4);
        b.Property(r => r.YieldPercent).HasPrecision(18, 4);

        b.HasOne(r => r.Restaurant)
            .WithMany()
            .HasForeignKey(r => r.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(r => r.MenuItem)
            .WithMany()
            .HasForeignKey(r => r.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(r => r.Ingredient)
            .WithMany(i => i.RecipeItems)
            .HasForeignKey(r => r.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
