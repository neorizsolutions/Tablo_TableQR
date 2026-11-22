using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.Entities;

namespace Tablo.Api.Services;

public record DiningGroupDto(Guid Id,Guid TableId,string Label,DateTime CreatedAtUtc);

public class DiningGroupService(ApplicationDbContext db)
{
    public static async Task<(Guid? Id,string? Label)> ResolveAsync(ApplicationDbContext db,Guid tenant,Guid table,Guid? groupId)
    {
        if(groupId.HasValue)
        {
            var group=await db.DiningGroups.AsNoTracking().SingleOrDefaultAsync(g=>g.Id==groupId && g.RestaurantId==tenant && g.TableId==table && g.ClosedAtUtc==null)
                ??throw new ArgumentException("This dining group is no longer active at this table. Select the current group.");
            return(group.Id,group.Label);
        }
        if(await db.DiningGroups.AnyAsync(g=>g.RestaurantId==tenant && g.TableId==table && g.ClosedAtUtc==null))
            throw new ArgumentException("Select a dining group before ordering.");
        return(null,null);
    }
    public async Task<IReadOnlyList<DiningGroupDto>> ListAsync(Guid tenant,Guid table)
    {
        if(!await db.RestaurantTables.AnyAsync(t=>t.Id==table && t.RestaurantId==tenant)) throw new KeyNotFoundException("Table not found.");
        return await db.DiningGroups.AsNoTracking().Where(g=>g.RestaurantId==tenant && g.TableId==table && g.ClosedAtUtc==null)
            .OrderBy(g=>g.Label).Select(g=>new DiningGroupDto(g.Id,g.TableId,g.Label,g.CreatedAtUtc)).ToListAsync();
    }

    public async Task<DiningGroupDto> CreateAsync(Guid tenant,Guid actor,Guid table,string label,Guid operation)
    {
        await BillingService.RequireActorAsync(db,tenant,actor);
        label=(label??"").Trim().ToUpperInvariant();
        if(operation==Guid.Empty || table==Guid.Empty || !Regex.IsMatch(label,"^[A-Z][A-Z0-9]{0,7}$",RegexOptions.CultureInvariant))
            throw new ArgumentException("Enter a group label of one to eight letters/digits, starting with a letter, and a valid operation ID.");
        var hash=BillingService.Hash(JsonSerializer.Serialize(new{actor,table,label}));
        await using var tx=await TableOrderTransaction.BeginAsync(db);
        await TableOrderTransaction.LockAsync(db,tenant,table);
        var replay=await db.DiningGroups.SingleOrDefaultAsync(g=>g.RestaurantId==tenant && g.CreationOperationId==operation);
        if(replay!=null)
        {
            if(replay.CreationHash!=hash) throw new ServiceConflictException("This operation was used for another dining group.");
            return new(replay.Id,table,replay.Label,replay.CreatedAtUtc);
        }
        var physical=await db.RestaurantTables.SingleOrDefaultAsync(t=>t.Id==table && t.RestaurantId==tenant)??throw new KeyNotFoundException("Table not found.");
        if(!physical.IsActive) throw new ArgumentException("This table is inactive.");
        if(await db.DiningGroups.AnyAsync(g=>g.RestaurantId==tenant && g.TableId==table && g.Label==label && g.ClosedAtUtc==null))
            throw new ServiceConflictException("That group label is already active at this table.");
        var group=new DiningGroup{RestaurantId=tenant,TableId=table,Label=label,CreatedByEmployeeId=actor,CreationOperationId=operation,CreationHash=hash};
        db.DiningGroups.Add(group); await db.SaveChangesAsync();
        if(tx!=null) await tx.CommitAsync();
        return new(group.Id,table,label,group.CreatedAtUtc);
    }
}
