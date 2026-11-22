using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tablo.Api.Entities;
namespace Tablo.Api.Configurations;
public class FloorOperationConfiguration:IEntityTypeConfiguration<FloorOperation>
{
    public void Configure(EntityTypeBuilder<FloorOperation> b)
    {
        b.HasKey(x=>x.Id);b.HasIndex(x=>new{x.RestaurantId,x.OperationId}).IsUnique();
        b.Property(x=>x.Kind).HasMaxLength(20);b.Property(x=>x.Hash).HasMaxLength(64);b.Property(x=>x.ResultJson).HasColumnType("jsonb");
        b.HasOne<Employee>().WithMany().HasForeignKey(x=>new{x.RestaurantId,x.ActorEmployeeId}).HasPrincipalKey(e=>new{e.RestaurantId,e.Id}).OnDelete(DeleteBehavior.Restrict);
    }
}
