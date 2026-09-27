using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.Entities;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly ApplicationDbContext _context;

        public OrderRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<RestaurantTable?> GetTableByNumberAsync(Guid restaurantId, int tableNumber)
        {
            return await _context.RestaurantTables
                .FirstOrDefaultAsync(t => t.RestaurantId == restaurantId && t.TableNumber == tableNumber);
        }

        public async Task<RestaurantTable?> GetTableByQRTokenAsync(string qrToken)
        {
            return await _context.RestaurantTables
                .FirstOrDefaultAsync(t => t.QRToken == qrToken);
        }

        public async Task<MenuItem?> GetMenuItemAsync(Guid restaurantId, Guid menuItemId)
        {
            return await _context.MenuItems
                .FirstOrDefaultAsync(m => m.Id == menuItemId && m.Category.RestaurantId == restaurantId);
        }

        public async Task<Order> CreateOrderAsync(Order order)
        {
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task<RestaurantSettings?> GetRestaurantSettingsAsync(Guid restaurantId)
        {
            return await _context.RestaurantSettings
                .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
        }

        public async Task<IEnumerable<Order>> GetOrdersAsync(Guid restaurantId, string? status = null)
        {
            var query = _context.Orders
                .Include(o => o.Table)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.RestaurantId == restaurantId);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.Status == status);
            }

            return await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<Order?> GetOrderByIdAsync(Guid restaurantId, Guid orderId)
        {
            return await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId);
        }

        public async Task UpdateOrderAsync(Order order)
        {
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetNextOrderNumberAsync(Guid restaurantId)
        {
            // Postgres atomic upsert and return the inserted NextOrderNumber - 1 (because we incremented it)
            // Or easier: return NextOrderNumber, and the returning statement will get the NEW incremented value. Wait.
            // Let's just do: insert if not exists with 2, on conflict update set NextOrderNumber = NextOrderNumber + 1 RETURNING NextOrderNumber
            var sql = @"
                INSERT INTO ""RestaurantOrderSequences"" (""RestaurantId"", ""NextOrderNumber"")
                VALUES (@p0, 2)
                ON CONFLICT (""RestaurantId"") 
                DO UPDATE SET ""NextOrderNumber"" = ""RestaurantOrderSequences"".""NextOrderNumber"" + 1
                RETURNING ""NextOrderNumber"";
            ";

            // Ef Core doesn't easily return a scalar from raw SQL if it's not an entity. 
            // Better to query via Dapper or DbConnection. 
            // EF Core 8: _context.Database.SqlQueryRaw<int>
            var resultList = await _context.Database.SqlQueryRaw<int>(sql, restaurantId).ToListAsync();
            var result = resultList.Single();
            return result - 1; // Since we incremented to the *next* one, the current one is result - 1.
            // Actually, if we insert 2, the current one is 1. If it returns 2, current is 1.
            // If it returns 3, current is 2. So yes, return result - 1.
        }
    }
}
