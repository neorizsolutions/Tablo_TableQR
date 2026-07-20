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
            string tableStr = tableNumber.ToString();
            string tableStrWithPrefix = $"Table {tableNumber}";
            
            return await _context.RestaurantTables
                .FirstOrDefaultAsync(t => t.RestaurantId == restaurantId && 
                    (t.NameOrNumber == tableStr || t.NameOrNumber == tableStrWithPrefix));
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
    }
}
