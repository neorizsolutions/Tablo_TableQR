using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations
{
    public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> builder)
        {
            builder.ToTable("Employees");
            builder.HasKey(e => e.Id);

            builder.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            builder.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            builder.Property(e => e.Email).IsRequired().HasMaxLength(255);
            builder.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);

            builder.HasIndex(e => e.Email).IsUnique();
            builder.HasIndex(e => e.RestaurantId);

            builder.HasOne(e => e.Restaurant)
                .WithMany(r => r.Employees)
                .HasForeignKey(e => e.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.Role)
                .WithMany(r => r.Employees)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
