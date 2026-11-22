using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> builder)
    {
        builder.HasOne(s => s.DiningGroup).WithMany()
            .HasForeignKey(s => new { s.RestaurantId, s.DiningGroupId })
            .HasPrincipalKey(g => new { g.RestaurantId, g.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
