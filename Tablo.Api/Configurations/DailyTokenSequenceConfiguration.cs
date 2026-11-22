using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class DailyTokenSequenceConfiguration : IEntityTypeConfiguration<DailyTokenSequence>
{
    public void Configure(EntityTypeBuilder<DailyTokenSequence> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.BusinessDate)
            .IsRequired()
            .HasMaxLength(10);

        builder.HasIndex(s => new { s.RestaurantId, s.BusinessDate })
            .IsUnique();

        builder.HasOne(s => s.Restaurant)
            .WithMany()
            .HasForeignKey(s => s.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
