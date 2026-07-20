using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations
{
    public class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.ToTable("Categories");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
            builder.Property(c => c.DisplayOrder).HasDefaultValue(0);

            builder.HasIndex(c => c.RestaurantId);

            builder.HasOne(c => c.Restaurant)
                .WithMany(r => r.Categories)
                .HasForeignKey(c => c.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
