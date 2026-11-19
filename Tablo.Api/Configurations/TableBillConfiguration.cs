using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class TableBillConfiguration : IEntityTypeConfiguration<TableBill>
{
    public void Configure(EntityTypeBuilder<TableBill> b)
    {
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.RestaurantId, x.Id });
        b.Property(x => x.PaymentRevision).HasMaxLength(64);
        b.HasIndex(x=>new{x.RestaurantId,x.TableId}).IsUnique().HasDatabaseName("IX_TableBills_UngroupedOrdering").HasFilter("\"DiningGroupId\" IS NULL AND \"SettledAtUtc\" IS NULL AND \"SplitAtUtc\" IS NULL AND \"PaymentRevision\" IS NULL AND \"ParentBillId\" IS NULL");
        b.HasIndex(x => new { x.RestaurantId, x.TableId, x.DiningGroupId }).IsUnique().HasFilter("\"SettledAtUtc\" IS NULL AND \"SplitAtUtc\" IS NULL AND \"PaymentRevision\" IS NULL AND \"ParentBillId\" IS NULL");
        b.Property(x=>x.GroupLabelSnapshot).HasMaxLength(8);
        b.Property(x=>x.AllocationJson).HasColumnType("jsonb");
        b.HasOne<DiningGroup>().WithMany().HasForeignKey(x=>new{x.RestaurantId,x.DiningGroupId}).HasPrincipalKey(x=>new{x.RestaurantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TableBill>().WithMany().HasForeignKey(x=>new{x.RestaurantId,x.ParentBillId}).HasPrincipalKey(x=>new{x.RestaurantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.RestaurantId, x.SettlementOperationId }).IsUnique()
            .HasFilter("\"SettlementOperationId\" IS NOT NULL");
        b.HasIndex(x => new { x.RestaurantId, x.SettledAtUtc });
        b.HasOne<RestaurantTable>().WithMany().HasForeignKey(x => new { x.RestaurantId, x.TableId })
            .HasPrincipalKey(x => new { x.RestaurantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => new { x.RestaurantId, x.SettledByEmployeeId })
            .HasPrincipalKey(x => new { x.RestaurantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.PaymentMethod).HasMaxLength(20);
        b.Property(x => x.SettlementHash).HasMaxLength(64);
        b.Property(x => x.ReceiptJson).HasColumnType("jsonb");
        b.Property(x => x.Subtotal).HasPrecision(18, 2);
        b.Property(x => x.Tax).HasPrecision(18, 2);
        b.Property(x => x.Discount).HasPrecision(18, 2);
        b.Property(x => x.TotalAmount).HasPrecision(18, 2);
    }
}
