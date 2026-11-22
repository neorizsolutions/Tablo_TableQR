using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> b)
    {
        b.HasKey(po => po.Id);
        b.HasIndex(po => new { po.RestaurantId, po.OrderNumber }).IsUnique();

        b.HasOne(po => po.Supplier)
            .WithMany(s => s.PurchaseOrders)
            .HasForeignKey(po => po.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(po => po.Items)
            .WithOne(poi => poi.PurchaseOrder)
            .HasForeignKey(poi => poi.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> b)
    {
        b.HasKey(pi => pi.Id);
        b.HasIndex(pi => new { pi.RestaurantId, pi.SupplierId, pi.InvoiceNumber }).IsUnique();

        b.HasOne(pi => pi.Supplier)
            .WithMany(s => s.PurchaseInvoices)
            .HasForeignKey(pi => pi.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(pi => pi.PurchaseOrder)
            .WithMany(po => po.Invoices)
            .HasForeignKey(pi => pi.PurchaseOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasMany(pi => pi.Items)
            .WithOne(pii => pii.PurchaseInvoice)
            .HasForeignKey(pii => pii.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PurchaseReturnConfiguration : IEntityTypeConfiguration<PurchaseReturn>
{
    public void Configure(EntityTypeBuilder<PurchaseReturn> b)
    {
        b.HasKey(pr => pr.Id);
        b.HasIndex(pr => new { pr.RestaurantId, pr.ReturnNumber }).IsUnique();

        b.HasOne(pr => pr.PurchaseInvoice)
            .WithMany(pi => pi.Returns)
            .HasForeignKey(pr => pr.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(pr => pr.Items)
            .WithOne(pri => pri.PurchaseReturn)
            .HasForeignKey(pri => pri.PurchaseReturnId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PreparationRecipeConfiguration : IEntityTypeConfiguration<PreparationRecipe>
{
    public void Configure(EntityTypeBuilder<PreparationRecipe> b)
    {
        b.HasKey(pr => pr.Id);
        b.HasIndex(pr => new { pr.RestaurantId, pr.OutputIngredientId });

        b.HasOne(pr => pr.OutputIngredient)
            .WithOne(i => i.PreparationRecipe)
            .HasForeignKey<PreparationRecipe>(pr => pr.OutputIngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(pr => pr.InputItems)
            .WithOne(pri => pri.PreparationRecipe)
            .HasForeignKey(pri => pri.PreparationRecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> b)
    {
        b.HasKey(st => st.Id);
        b.HasIndex(st => new { st.RestaurantId, st.TransferNumber }).IsUnique();

        b.HasOne(st => st.SourceOutlet)
            .WithMany()
            .HasForeignKey(st => st.SourceOutletId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(st => st.DestinationOutlet)
            .WithMany()
            .HasForeignKey(st => st.DestinationOutletId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(st => st.Items)
            .WithOne(sti => sti.StockTransfer)
            .HasForeignKey(sti => sti.StockTransferId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> b)
    {
        b.HasKey(sr => sr.Id);
        b.HasIndex(sr => new { sr.RestaurantId, sr.OrderId, sr.Status });
        b.HasIndex(sr => new { sr.RestaurantId, sr.IngredientId, sr.Status });

        b.HasOne(sr => sr.Order)
            .WithMany()
            .HasForeignKey(sr => sr.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(sr => sr.OrderItem)
            .WithMany()
            .HasForeignKey(sr => sr.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(sr => sr.Ingredient)
            .WithMany()
            .HasForeignKey(sr => sr.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
