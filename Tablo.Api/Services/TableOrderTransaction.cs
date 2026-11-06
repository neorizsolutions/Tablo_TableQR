using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Tablo.Api.Data;

namespace Tablo.Api.Services;

// All order/bill writers use the physical table row, including the no-order-yet case.
internal static class TableOrderTransaction
{
    public static async Task<IDbContextTransaction?> BeginAsync(ApplicationDbContext db)
        => db.Database.IsRelational() && db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync() : null;

    public static async Task LockAsync(ApplicationDbContext db, Guid restaurantId, Guid tableId)
    {
        if (db.Database.IsRelational())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"RestaurantTables\" WHERE \"Id\" = {tableId} AND \"RestaurantId\" = {restaurantId} FOR UPDATE");
    }
}

public class ServiceConflictException(string message) : Exception(message);
