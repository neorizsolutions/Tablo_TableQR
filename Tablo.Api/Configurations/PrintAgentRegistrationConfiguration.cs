using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class PrintAgentRegistrationConfiguration : IEntityTypeConfiguration<PrintAgentRegistration>
{
    public void Configure(EntityTypeBuilder<PrintAgentRegistration> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AgentName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.ApiKeyHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasOne(a => a.Restaurant)
            .WithMany()
            .HasForeignKey(a => a.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.RestaurantId, a.IsActive });
        builder.HasIndex(a => a.ApiKeyHash);
    }
}
