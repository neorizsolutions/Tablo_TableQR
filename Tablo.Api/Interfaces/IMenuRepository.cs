using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tablo.Api.Entities;

namespace Tablo.Api.Interfaces
{
    public interface IMenuRepository
    {
        Task<bool> IsActiveKitchenStationAsync(Guid restaurantId, Guid stationId, CancellationToken cancellationToken = default);
        Task<bool> RestaurantExistsAsync(Guid restaurantId, CancellationToken cancellationToken = default);
        Task<List<Category>> GetMenuCategoriesAsync(Guid restaurantId, CancellationToken cancellationToken = default);
        Task<List<Category>> GetAdminMenuCategoriesAsync(Guid restaurantId, CancellationToken cancellationToken = default);
        
        Task<Category?> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
        Task<Category> CreateCategoryAsync(Category category, CancellationToken cancellationToken = default);
        Task<Category> UpdateCategoryAsync(Category category, CancellationToken cancellationToken = default);
        Task DeleteCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
        
        Task<MenuItem?> GetMenuItemAsync(Guid menuItemId, CancellationToken cancellationToken = default);
        Task<MenuItem> CreateMenuItemAsync(MenuItem menuItem, CancellationToken cancellationToken = default);
        Task<MenuItem> UpdateMenuItemAsync(MenuItem menuItem, CancellationToken cancellationToken = default);
        Task SoftDeleteMenuItemAsync(Guid menuItemId, CancellationToken cancellationToken = default);
    }
}
