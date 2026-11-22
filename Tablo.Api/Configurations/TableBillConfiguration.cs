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
        b.HasIndex(x=>new{x.RestaurantId,x.TableId}).IsUnique().HasDatabaseName("IX_TableBills_UngroupedOrdering").HasFilter("\"TableId\" IS NOT NULL AND \"DiningGroupId\" IS NULL AND \"SettledAtUtc\" IS NULL AND \"SplitAtUtc\" IS NULL AND \"PaymentRevision\" IS NULL AND \"ParentBillId\" IS NULL");
        b.HasIndex(x => new { x.RestaurantId, x.TableId, x.DiningGroupId }).IsUnique().HasFilter("\"TableId\" IS NOT NULL AND \"SettledAtUtc\" IS NULL AND \"SplitAtUtc\" IS NULL AND \"PaymentRevision\" IS NULL AND \"ParentBillId\" IS NULL");
        b.Property(x=>x.GroupLabelSnapshot).HasMaxLength(8);
        b.Property(x=>x.AllocationJson).HasColumnType("jsonb");
        b.Property(x=>x.OrderChannel).HasMaxLength(30).HasDefaultValue("DineIn");
        b.Property(x=>x.BusinessDate).HasMaxLength(10);
        b.Property(x=>x.PackagingChargeModeSnapshot).HasMaxLength(30);
        b.Property(x=>x.PackagingWaiverReason).HasMaxLength(200);
        b.Property(x=>x.PackagingWaivedBy).HasMaxLength(100);
        b.Property(x=>x.PackagingCharge).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(x=>x.LeftoverPackingCharge).HasPrecision(18, 2).HasDefaultValue(0m);
        b.Property(x=>x.VehiclePlate).HasMaxLength(30);
        b.Property(x=>x.ParkingBay).HasMaxLength(30);
        b.Property(x=>x.PackagingRateSnapshot).HasPrecision(18, 2).HasDefaultValue(0m);
        b.HasOne<DiningGroup>().WithMany().HasForeignKey(x=>new{x.RestaurantId,x.DiningGroupId}).HasPrincipalKey(x=>new{x.RestaurantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TableBill>().WithMany().HasForeignKey(x=>new{x.RestaurantId,x.ParentBillId}).HasPrincipalKey(x=>new{x.RestaurantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.RestaurantId, x.SettlementOperationId }).IsUnique()
            .HasFilter("\"SettlementOperationId\" IS NOT NULL");
        b.HasIndex(x => new { x.RestaurantId, x.SettledAtUtc });
        b.HasIndex(x => new { x.RestaurantId, x.BusinessDate, x.DailyTokenNumber });
        b.HasOne<RestaurantTable>().WithMany().HasForeignKey(x => new { x.RestaurantId, x.TableId })
            .HasPrincipalKey(x => new { x.RestaurantId, x.Id }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
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
