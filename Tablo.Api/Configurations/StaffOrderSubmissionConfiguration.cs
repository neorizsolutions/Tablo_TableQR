using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class StaffOrderSubmissionConfiguration : IEntityTypeConfiguration<StaffOrderSubmission>
{
    public void Configure(EntityTypeBuilder<StaffOrderSubmission> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.RestaurantId, x.OperationId }).IsUnique();
        b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.OriginalResponseJson).HasColumnType("jsonb").IsRequired();
        b.HasOne<RestaurantTable>().WithMany().HasForeignKey(x => new { x.RestaurantId, x.TableId })
            .HasPrincipalKey(x => new { x.RestaurantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => new { x.RestaurantId, x.ActorEmployeeId })
            .HasPrincipalKey(x => new { x.RestaurantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
