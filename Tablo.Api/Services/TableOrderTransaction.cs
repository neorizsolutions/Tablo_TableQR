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

    public static async Task LockAsync(ApplicationDbContext db, Guid restaurantId, Guid? tableId)
    {
        if (tableId.HasValue && db.Database.IsRelational())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"RestaurantTables\" WHERE \"Id\" = {tableId.Value} AND \"RestaurantId\" = {restaurantId} FOR UPDATE");
    }

    public static async Task LockTablesAsync(ApplicationDbContext db, Guid restaurantId, IEnumerable<Guid> tableIds)
    {
        if (db.Database.IsRelational())
        {
            foreach (var id in tableIds.Distinct().OrderBy(id => id))
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"RestaurantTables\" WHERE \"Id\" = {id} AND \"RestaurantId\" = {restaurantId} FOR UPDATE");
            }
        }
    }

    public static async Task LockBillAsync(ApplicationDbContext db, Guid restaurantId, Guid billId)
    {
        if (db.Database.IsRelational())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"TableBills\" WHERE \"Id\" = {billId} AND \"RestaurantId\" = {restaurantId} FOR UPDATE");
    }

    public static async Task LockBillsAsync(ApplicationDbContext db, Guid restaurantId, IEnumerable<Guid> billIds)
    {
        if (db.Database.IsRelational())
        {
            foreach (var id in billIds.Distinct().OrderBy(id => id))
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"TableBills\" WHERE \"Id\" = {id} AND \"RestaurantId\" = {restaurantId} FOR UPDATE");
            }
        }
    }

    public static async Task LockOrderAsync(ApplicationDbContext db, Guid restaurantId, Guid orderId)
    {
        if (db.Database.IsRelational())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Orders\" WHERE \"Id\" = {orderId} AND \"RestaurantId\" = {restaurantId} FOR UPDATE");
    }
}

public class ServiceConflictException(string message) : Exception(message);
