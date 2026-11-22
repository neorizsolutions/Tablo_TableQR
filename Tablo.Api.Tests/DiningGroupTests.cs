using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.Entities;
using Tablo.Api.Services;

namespace Tablo.Api.Tests;

public class DiningGroupTests : IDisposable
{
    private readonly ApplicationDbContext db=new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Guid tenant=Guid.NewGuid(), actor=Guid.NewGuid(), table=Guid.NewGuid();
    public DiningGroupTests()
    {
        db.Restaurants.Add(new(){Id=tenant,Name="Group test",ContactEmail="test@example.test"});
        db.RestaurantTables.Add(new(){Id=table,RestaurantId=tenant,TableNumber=4,IsActive=true,QRToken="groups"});
        db.Employees.Add(new(){Id=actor,RestaurantId=tenant,Role=new Role{Name="Waiter"},IsActive=true});
        db.SaveChanges();
    }
    [Fact]
    public async Task Create_IsReplaySafeAndSeparatesGroupsAtOnePhysicalTable()
    {
        var service=new DiningGroupService(db); var operation=Guid.NewGuid();
        var a=await service.CreateAsync(tenant,actor,table,"A",operation);
        var retry=await service.CreateAsync(tenant,actor,table,"A",operation);
        var b=await service.CreateAsync(tenant,actor,table,"B",Guid.NewGuid());
        Assert.Equal(a.Id,retry.Id); Assert.NotEqual(a.Id,b.Id);
        Assert.Equal(2,await db.DiningGroups.CountAsync());
        await Assert.ThrowsAsync<ServiceConflictException>(()=>service.CreateAsync(tenant,actor,table,"B",operation));
        await Assert.ThrowsAsync<ServiceConflictException>(()=>service.CreateAsync(tenant,actor,table,"A",Guid.NewGuid()));
    }
    [Fact]
    public async Task Create_RejectsInactiveForeignTenantAndUnsafeLabels()
    {
        var service=new DiningGroupService(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.CreateAsync(Guid.NewGuid(),actor,table,"A",Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.CreateAsync(tenant,actor,table,"<script>",Guid.NewGuid()));
        (await db.RestaurantTables.SingleAsync()).IsActive=false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(()=>service.CreateAsync(tenant,actor,table,"A",Guid.NewGuid()));
    }
    [Fact]
    public async Task ClosedLabels_CanBeReusedWithoutReusingVisitIdentity()
    {
        var service=new DiningGroupService(db);
        var old=await service.CreateAsync(tenant,actor,table,"A",Guid.NewGuid());
        (await db.DiningGroups.SingleAsync()).ClosedAtUtc=DateTime.UtcNow; await db.SaveChangesAsync();
        var next=await service.CreateAsync(tenant,actor,table,"A",Guid.NewGuid());
        Assert.NotEqual(old.Id,next.Id);
        Assert.Single(await service.ListAsync(tenant,table));
    }
    public void Dispose()=>db.Dispose();
}
