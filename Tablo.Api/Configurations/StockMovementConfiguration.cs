using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.HasKey(m => m.Id);
        b.HasIndex(m => new { m.RestaurantId, m.IngredientId, m.TimestampUtc });
        b.HasIndex(m => new { m.RestaurantId, m.TimestampUtc });
        b.HasIndex(m => new { m.RestaurantId, m.ReferenceType, m.ReferenceId, m.IngredientId })
            .IsUnique()
            .HasFilter("\"ReferenceType\" IS NOT NULL AND \"ReferenceId\" IS NOT NULL");
        b.HasIndex(m => new { m.RestaurantId, m.OperationId })
            .HasFilter("\"OperationId\" IS NOT NULL");

        b.Property(m => m.QuantityChange).HasPrecision(18, 4);
        b.Property(m => m.UnitSnapshot).HasMaxLength(20).IsRequired();
        b.Property(m => m.Reason).HasMaxLength(50).IsRequired();
        b.Property(m => m.ReferenceType).HasMaxLength(50);
        b.Property(m => m.ReferenceId).HasMaxLength(100);
        b.Property(m => m.Notes).HasMaxLength(500);

        b.HasOne(m => m.Restaurant)
            .WithMany()
            .HasForeignKey(m => m.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(m => m.Ingredient)
            .WithMany(i => i.StockMovements)
            .HasForeignKey(m => m.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(m => m.ActorEmployee)
            .WithMany()
            .HasForeignKey(m => m.ActorEmployeeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
