using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.Entities;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Repositories
{
    public class MenuRepository : IMenuRepository
    {
        private readonly ApplicationDbContext _context;

        public MenuRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> RestaurantExistsAsync(Guid restaurantId, CancellationToken cancellationToken = default)
        {
            return await _context.Restaurants
                .AnyAsync(r => r.Id == restaurantId, cancellationToken);
        }

        public async Task<List<Category>> GetMenuCategoriesAsync(Guid restaurantId, CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .Include(c => c.MenuItems.Where(m => m.IsAvailable && !m.IsDeleted))
                .Where(c => c.RestaurantId == restaurantId && c.MenuItems.Any(m => m.IsAvailable && !m.IsDeleted))
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Category>> GetAdminMenuCategoriesAsync(Guid restaurantId, CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .Include(c => c.MenuItems.Where(m => !m.IsDeleted))
                .Where(c => c.RestaurantId == restaurantId)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync(cancellationToken);
        }

        public async Task<Category?> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
        {
            return await _context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
        }

        public async Task<Category> CreateCategoryAsync(Category category, CancellationToken cancellationToken = default)
        {
            _context.Categories.Add(category);
            await _context.SaveChangesAsync(cancellationToken);
            return category;
        }

        public async Task<Category> UpdateCategoryAsync(Category category, CancellationToken cancellationToken = default)
        {
            _context.Categories.Update(category);
            await _context.SaveChangesAsync(cancellationToken);
            return category;
        }

        public async Task DeleteCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
        {
            var category = await _context.Categories
                .Include(c => c.MenuItems.Where(m => !m.IsDeleted))
                .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);

            if (category == null) return;

            if (category.MenuItems.Any())
            {
                throw new InvalidOperationException("Cannot delete a category that contains active menu items.");
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<MenuItem?> GetMenuItemAsync(Guid menuItemId, CancellationToken cancellationToken = default)
        {
            return await _context.MenuItems.FirstOrDefaultAsync(m => m.Id == menuItemId && !m.IsDeleted, cancellationToken);
        }

        public async Task<MenuItem> CreateMenuItemAsync(MenuItem menuItem, CancellationToken cancellationToken = default)
        {
            _context.MenuItems.Add(menuItem);
            await _context.SaveChangesAsync(cancellationToken);
            return menuItem;
        }

        public async Task<MenuItem> UpdateMenuItemAsync(MenuItem menuItem, CancellationToken cancellationToken = default)
        {
            _context.MenuItems.Update(menuItem);
            await _context.SaveChangesAsync(cancellationToken);
            return menuItem;
        }

        public async Task SoftDeleteMenuItemAsync(Guid menuItemId, CancellationToken cancellationToken = default)
        {
            var menuItem = await _context.MenuItems.FirstOrDefaultAsync(m => m.Id == menuItemId, cancellationToken);
            if (menuItem != null)
            {
                menuItem.IsDeleted = true;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
