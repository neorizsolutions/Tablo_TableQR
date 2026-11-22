using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;

namespace Tablo.Api.Configurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => p.Code).IsUnique();
        builder.Property(p => p.Code).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.MonthlyPriceInr).HasPrecision(18, 2);
    }
}

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.RestaurantId).IsUnique(); // One active subscription per restaurant
        builder.HasOne(s => s.Restaurant)
               .WithOne(r => r.Subscription)
               .HasForeignKey<Subscription>(s => s.RestaurantId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Plan)
               .WithMany(p => p.Subscriptions)
               .HasForeignKey(s => s.PlanId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SubscriptionAuditLogConfiguration : IEntityTypeConfiguration<SubscriptionAuditLog>
{
    public void Configure(EntityTypeBuilder<SubscriptionAuditLog> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => new { a.RestaurantId, a.TimestampUtc });

        builder.HasOne(a => a.Restaurant)
               .WithMany(r => r.SubscriptionAuditLogs)
               .HasForeignKey(a => a.RestaurantId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.FromPlan)
               .WithMany()
               .HasForeignKey(a => a.FromPlanId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ToPlan)
               .WithMany()
               .HasForeignKey(a => a.ToPlanId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
