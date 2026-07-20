using System;
using System.Threading;
using System.Threading.Tasks;
using Tablo.Api.DTOs;

namespace Tablo.Api.Interfaces
{
    public interface IMenuService
    {
        Task<MenuResponseDto?> GetMenuAsync(Guid restaurantId, CancellationToken cancellationToken = default);
        Task<MenuResponseDto?> GetAdminMenuAsync(Guid restaurantId, CancellationToken cancellationToken = default);
        
        Task<CategoryDto> CreateCategoryAsync(Guid restaurantId, CreateCategoryRequestDto request, CancellationToken cancellationToken = default);
        Task<CategoryDto> UpdateCategoryAsync(Guid categoryId, UpdateCategoryRequestDto request, CancellationToken cancellationToken = default);
        Task DeleteCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);

        Task<MenuItemDto> CreateMenuItemAsync(Guid restaurantId, CreateMenuItemRequestDto request, CancellationToken cancellationToken = default);
        Task<MenuItemDto> UpdateMenuItemAsync(Guid menuItemId, UpdateMenuItemRequestDto request, CancellationToken cancellationToken = default);
        Task<MenuItemDto> UpdateMenuItemAvailabilityAsync(Guid menuItemId, UpdateMenuItemAvailabilityRequestDto request, CancellationToken cancellationToken = default);
        Task DeleteMenuItemAsync(Guid menuItemId, CancellationToken cancellationToken = default);
    }
}
