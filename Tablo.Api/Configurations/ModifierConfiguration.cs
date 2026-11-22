using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class ModifierGroupConfiguration : IEntityTypeConfiguration<ModifierGroup>
{
    public void Configure(EntityTypeBuilder<ModifierGroup> builder)
    {
        builder.ToTable("ModifierGroups");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name).IsRequired().HasMaxLength(100);
        builder.Property(g => g.MinSelections).HasDefaultValue(0);
        builder.Property(g => g.MaxSelections).HasDefaultValue(1);
        builder.Property(g => g.DisplayOrder).HasDefaultValue(0);
        builder.Property(g => g.IsActive).HasDefaultValue(true);

        builder.HasIndex(g => new { g.RestaurantId, g.DisplayOrder });

        builder.HasOne(g => g.Restaurant)
            .WithMany()
            .HasForeignKey(g => g.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ModifierOptionConfiguration : IEntityTypeConfiguration<ModifierOption>
{
    public void Configure(EntityTypeBuilder<ModifierOption> builder)
    {
        builder.ToTable("ModifierOptions");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name).IsRequired().HasMaxLength(100);
        builder.Property(o => o.PriceDelta).HasColumnType("decimal(18,2)").HasDefaultValue(0.00m);
        builder.Property(o => o.DisplayOrder).HasDefaultValue(0);
        builder.Property(o => o.IsAvailable).HasDefaultValue(true);
        builder.Property(o => o.IngredientAdjustmentType).HasMaxLength(30).HasDefaultValue("None");
        builder.Property(o => o.IngredientQuantity).HasColumnType("decimal(18,4)").HasDefaultValue(0m);

        builder.HasIndex(o => new { o.ModifierGroupId, o.DisplayOrder });
        builder.HasIndex(o => o.RestaurantId);

        builder.HasOne(o => o.ModifierGroup)
            .WithMany(g => g.Options)
            .HasForeignKey(o => o.ModifierGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Restaurant)
            .WithMany()
            .HasForeignKey(o => o.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Ingredient)
            .WithMany()
            .HasForeignKey(o => o.IngredientId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(o => o.OriginalIngredient)
            .WithMany()
            .HasForeignKey(o => o.OriginalIngredientId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class MenuItemModifierGroupConfiguration : IEntityTypeConfiguration<MenuItemModifierGroup>
{
    public void Configure(EntityTypeBuilder<MenuItemModifierGroup> builder)
    {
        builder.ToTable("MenuItemModifierGroups");
        builder.HasKey(mg => new { mg.MenuItemId, mg.ModifierGroupId });

        builder.Property(mg => mg.DisplayOrder).HasDefaultValue(0);

        builder.HasOne(mg => mg.MenuItem)
            .WithMany(m => m.ModifierGroups)
            .HasForeignKey(mg => mg.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mg => mg.ModifierGroup)
            .WithMany(g => g.MenuItemLinks)
            .HasForeignKey(mg => mg.ModifierGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ItemCancellationLogConfiguration : IEntityTypeConfiguration<ItemCancellationLog>
{
    public void Configure(EntityTypeBuilder<ItemCancellationLog> builder)
    {
        builder.ToTable("ItemCancellationLogs");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ItemNameSnapshot).IsRequired().HasMaxLength(200);
        builder.Property(c => c.ProductionOutcome).IsRequired().HasMaxLength(30);
        builder.Property(c => c.Reason).IsRequired().HasMaxLength(500);
        builder.Property(c => c.ApproverNameSnapshot).HasMaxLength(100);
        builder.Property(c => c.WastedCost).HasColumnType("decimal(18,4)");
        builder.Property(c => c.OperationId).HasMaxLength(64);

        builder.HasIndex(c => new { c.RestaurantId, c.TimestampUtc });
        builder.HasIndex(c => c.OrderId);
        builder.HasIndex(c => c.OrderItemId);
        builder.HasIndex(c => new { c.RestaurantId, c.OperationId });

        builder.HasOne(c => c.Restaurant)
            .WithMany()
            .HasForeignKey(c => c.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Order)
            .WithMany()
            .HasForeignKey(c => c.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.OrderItem)
            .WithMany()
            .HasForeignKey(c => c.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.RequestedByEmployee)
            .WithMany()
            .HasForeignKey(c => c.RequestedByEmployeeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.ApprovedByEmployee)
            .WithMany()
            .HasForeignKey(c => c.ApprovedByEmployeeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
