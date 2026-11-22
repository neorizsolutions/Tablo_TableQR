using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class DiningGroupConfiguration : IEntityTypeConfiguration<DiningGroup>
{
    public void Configure(EntityTypeBuilder<DiningGroup> b)
    {
        b.HasKey(g=>g.Id);
        b.HasAlternateKey(g=>new{g.RestaurantId,g.Id});
        b.Property(g=>g.Label).HasMaxLength(8).IsRequired();
        b.Property(g=>g.CreationHash).HasMaxLength(64).IsRequired();
        b.HasIndex(g=>new{g.RestaurantId,g.TableId,g.Label}).IsUnique().HasFilter("\"ClosedAtUtc\" IS NULL");
        b.HasIndex(g=>new{g.RestaurantId,g.CreationOperationId}).IsUnique();
        b.HasOne<RestaurantTable>().WithMany().HasForeignKey(g=>new{g.RestaurantId,g.TableId})
            .HasPrincipalKey(t=>new{t.RestaurantId,t.Id}).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(g=>new{g.RestaurantId,g.CreatedByEmployeeId})
            .HasPrincipalKey(e=>new{e.RestaurantId,e.Id}).OnDelete(DeleteBehavior.Restrict);
    }
}
