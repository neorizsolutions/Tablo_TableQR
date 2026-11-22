using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class BillPaymentConfiguration : IEntityTypeConfiguration<BillPayment>
{
    public void Configure(EntityTypeBuilder<BillPayment> b)
    {
        b.HasKey(p=>p.Id);
        b.HasIndex(p=>new{p.RestaurantId,p.OperationId}).IsUnique();
        b.HasIndex(p=>new{p.RestaurantId,p.CollectedAtUtc});
        b.Property(p=>p.Method).HasMaxLength(20).IsRequired();
        b.Property(p=>p.PayloadHash).HasMaxLength(64).IsRequired();
        b.Property(p=>p.Amount).HasPrecision(18,2);
        b.Property(p=>p.AcknowledgementJson).HasColumnType("jsonb");
        b.HasOne<TableBill>().WithMany().HasForeignKey(p=>new{p.RestaurantId,p.TableBillId})
            .HasPrincipalKey(t=>new{t.RestaurantId,t.Id}).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(p=>new{p.RestaurantId,p.ActorEmployeeId})
            .HasPrincipalKey(e=>new{e.RestaurantId,e.Id}).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t=>t.HasCheckConstraint("CK_BillPayments_PositiveAmount","\"Amount\" > 0"));
    }
}
