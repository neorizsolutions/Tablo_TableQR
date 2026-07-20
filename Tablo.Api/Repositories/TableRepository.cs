using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.Entities;
using Tablo.Api.Interfaces;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Tablo.Api.Repositories
{
    public class TableRepository : ITableRepository
    {
        private readonly ApplicationDbContext _context;

        public TableRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<RestaurantTable>> GetTablesAsync(Guid restaurantId)
        {
            return await _context.RestaurantTables
                .Where(t => t.RestaurantId == restaurantId)
                .OrderBy(t => t.NameOrNumber)
                .ToListAsync();
        }

        public async Task<RestaurantTable?> GetTableByIdAsync(Guid restaurantId, Guid tableId)
        {
            return await _context.RestaurantTables
                .FirstOrDefaultAsync(t => t.Id == tableId && t.RestaurantId == restaurantId);
        }

        public async Task<RestaurantTable?> GetTableByQRTokenAsync(string qrToken)
        {
            return await _context.RestaurantTables
                .Include(t => t.Restaurant)
                .FirstOrDefaultAsync(t => t.QRToken == qrToken);
        }

        public async Task<RestaurantTable> CreateTableAsync(RestaurantTable table)
        {
            _context.RestaurantTables.Add(table);
            await _context.SaveChangesAsync();
            return table;
        }

        public async Task UpdateTableAsync(RestaurantTable table)
        {
            _context.RestaurantTables.Update(table);
            await _context.SaveChangesAsync();
        }
    }
}
