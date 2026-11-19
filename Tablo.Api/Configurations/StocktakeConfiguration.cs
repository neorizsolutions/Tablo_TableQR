using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class StocktakeConfiguration : IEntityTypeConfiguration<Stocktake>
{
    public void Configure(EntityTypeBuilder<Stocktake> b)
    {
        b.HasKey(s => s.Id);
        b.HasIndex(s => new { s.RestaurantId, s.OperationId }).IsUnique();
        b.HasIndex(s => new { s.RestaurantId, s.CountedAtUtc });

        b.Property(s => s.BusinessDateIst).HasMaxLength(20).IsRequired();
        b.Property(s => s.Status).HasMaxLength(20).IsRequired();
        b.Property(s => s.Notes).HasMaxLength(500);

        b.HasOne(s => s.Restaurant)
            .WithMany()
            .HasForeignKey(s => s.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(s => s.ActorEmployee)
            .WithMany()
            .HasForeignKey(s => s.ActorEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(s => s.Items)
            .WithOne(i => i.Stocktake)
            .HasForeignKey(i => i.StocktakeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class StocktakeItemConfiguration : IEntityTypeConfiguration<StocktakeItem>
{
    public void Configure(EntityTypeBuilder<StocktakeItem> b)
    {
        b.HasKey(i => i.Id);
        b.HasIndex(i => new { i.StocktakeId, i.IngredientId }).IsUnique();

        b.Property(i => i.RecordedStockSnapshot).HasPrecision(18, 4);
        b.Property(i => i.PhysicalCount).HasPrecision(18, 4);
        b.Property(i => i.Variance).HasPrecision(18, 4);
        b.Property(i => i.StockAtPosting).HasPrecision(18, 4);
        b.Property(i => i.FinalAdjustment).HasPrecision(18, 4);

        b.HasOne(i => i.Ingredient)
            .WithMany(ing => ing.StocktakeItems)
            .HasForeignKey(i => i.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
