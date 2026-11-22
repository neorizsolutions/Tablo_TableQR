using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class SettledBillItemCostConfiguration : IEntityTypeConfiguration<SettledBillItemCost>
{
    public void Configure(EntityTypeBuilder<SettledBillItemCost> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.RestaurantId, x.TableBillId });
        b.HasIndex(x => new { x.RestaurantId, x.SettledAtUtc });
        b.HasIndex(x => new { x.RestaurantId, x.MenuItemId });

        b.Property(x => x.MenuItemNameSnapshot).HasMaxLength(200).IsRequired();
        b.Property(x => x.CategoryNameSnapshot).HasMaxLength(100);
        b.Property(x => x.UnitPriceSnapshot).HasPrecision(18, 2);
        b.Property(x => x.GrossRevenue).HasPrecision(18, 2);
        b.Property(x => x.AllocatedDiscount).HasPrecision(18, 2);
        b.Property(x => x.NetRevenue).HasPrecision(18, 2);
        b.Property(x => x.AllocatedTax).HasPrecision(18, 2);
        b.Property(x => x.TotalIngredientCost).HasPrecision(18, 4);
        b.Property(x => x.GrossProfit).HasPrecision(18, 4);
        b.Property(x => x.GrossMarginPercent).HasPrecision(18, 2);

        b.HasOne(x => x.Restaurant)
            .WithMany()
            .HasForeignKey(x => x.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.TableBill)
            .WithMany()
            .HasForeignKey(x => x.TableBillId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.MenuItem)
            .WithMany()
            .HasForeignKey(x => x.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.IngredientCosts)
            .WithOne(c => c.SettledBillItemCost)
            .HasForeignKey(c => c.SettledBillItemCostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SettledBillItemIngredientCostConfiguration : IEntityTypeConfiguration<SettledBillItemIngredientCost>
{
    public void Configure(EntityTypeBuilder<SettledBillItemIngredientCost> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.SettledBillItemCostId);
        b.HasIndex(x => x.IngredientId);

        b.Property(x => x.IngredientNameSnapshot).HasMaxLength(200).IsRequired();
        b.Property(x => x.BaseUnitSnapshot).HasMaxLength(20).IsRequired();
        b.Property(x => x.RawQuantityPerPortion).HasPrecision(18, 4);
        b.Property(x => x.TotalRawQuantity).HasPrecision(18, 4);
        b.Property(x => x.UnitCostSnapshot).HasPrecision(18, 6);
        b.Property(x => x.TotalCost).HasPrecision(18, 4);

        b.HasOne(x => x.Ingredient)
            .WithMany()
            .HasForeignKey(x => x.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
