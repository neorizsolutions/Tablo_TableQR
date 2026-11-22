using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.HasKey(s => s.Id);
        b.HasIndex(s => new { s.RestaurantId, s.Name });

        b.HasOne(s => s.Restaurant)
            .WithMany()
            .HasForeignKey(s => s.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SupplierIngredientConfiguration : IEntityTypeConfiguration<SupplierIngredient>
{
    public void Configure(EntityTypeBuilder<SupplierIngredient> b)
    {
        b.HasKey(si => si.Id);
        b.HasIndex(si => new { si.RestaurantId, si.SupplierId, si.IngredientId });

        b.HasOne(si => si.Supplier)
            .WithMany(s => s.SupplierIngredients)
            .HasForeignKey(si => si.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(si => si.Ingredient)
            .WithMany(i => i.SupplierIngredients)
            .HasForeignKey(si => si.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OutletConfiguration : IEntityTypeConfiguration<Outlet>
{
    public void Configure(EntityTypeBuilder<Outlet> b)
    {
        b.HasKey(o => o.Id);
        b.HasIndex(o => new { o.RestaurantId, o.Code });

        b.HasOne(o => o.Restaurant)
            .WithMany(r => r.Outlets)
            .HasForeignKey(o => o.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class StockLotConfiguration : IEntityTypeConfiguration<StockLot>
{
    public void Configure(EntityTypeBuilder<StockLot> b)
    {
        b.HasKey(l => l.Id);
        b.HasIndex(l => new { l.RestaurantId, l.IngredientId, l.Status });
        b.HasIndex(l => new { l.RestaurantId, l.LotCode });

        b.HasOne(l => l.Ingredient)
            .WithMany(i => i.StockLots)
            .HasForeignKey(l => l.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(l => l.Outlet)
            .WithMany()
            .HasForeignKey(l => l.OutletId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
