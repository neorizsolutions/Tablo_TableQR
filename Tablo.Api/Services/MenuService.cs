using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Tablo.Api.Hubs;

namespace Tablo.Api.Services
{
    public class MenuService : IMenuService
    {
        private readonly IMenuRepository _menuRepository;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly IImageStorageService _imageStorageService;

        public MenuService(IMenuRepository menuRepository, IHubContext<OrderHub> hubContext, IImageStorageService imageStorageService)
        {
            _menuRepository = menuRepository;
            _hubContext = hubContext;
            _imageStorageService = imageStorageService;
        }

        public async Task<MenuResponseDto?> GetMenuAsync(Guid restaurantId, CancellationToken cancellationToken)
        {
            var exists = await _menuRepository.RestaurantExistsAsync(restaurantId, cancellationToken);
            if (!exists)
            {
                return null;
            }

            var categories = await _menuRepository.GetMenuCategoriesAsync(restaurantId, cancellationToken);

            var response = new MenuResponseDto
            {
                RestaurantId = restaurantId,
                Categories = categories.Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    DisplayOrder = c.DisplayOrder,
                    MenuItems = c.MenuItems.Select(m => new MenuItemDto
                    {
                        Id = m.Id,
                        Name = m.Name,
                        Description = m.Description,
                        Price = m.Price,
                        IsAvailable = m.IsAvailable,
                        ImageUrl = _imageStorageService.GetImageUrl(m.ImageUrl)
                    }).ToList()
                }).ToList()
            };

            return response;
        }

        public async Task<MenuResponseDto?> GetAdminMenuAsync(Guid restaurantId, CancellationToken cancellationToken = default)
        {
            var exists = await _menuRepository.RestaurantExistsAsync(restaurantId, cancellationToken);
            if (!exists)
            {
                return null;
            }

            var categories = await _menuRepository.GetAdminMenuCategoriesAsync(restaurantId, cancellationToken);

            var response = new MenuResponseDto
            {
                RestaurantId = restaurantId,
                Categories = categories.Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    DisplayOrder = c.DisplayOrder,
                    MenuItems = c.MenuItems.Select(m => new MenuItemDto
                    {
                        Id = m.Id,
                        Name = m.Name,
                        Description = m.Description,
                        Price = m.Price,
                        IsAvailable = m.IsAvailable,
                        ImageUrl = _imageStorageService.GetImageUrl(m.ImageUrl)
                    }).ToList()
                }).ToList()
            };

            return response;
        }

        public async Task<CategoryDto> CreateCategoryAsync(Guid restaurantId, CreateCategoryRequestDto request, CancellationToken cancellationToken = default)
        {
            var exists = await _menuRepository.RestaurantExistsAsync(restaurantId, cancellationToken);
            if (!exists) throw new ArgumentException("Restaurant not found.");

            // Get existing categories to determine DisplayOrder
            var existingCategories = await _menuRepository.GetAdminMenuCategoriesAsync(restaurantId, cancellationToken);
            
            if (existingCategories.Any(c => c.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"A category with the name '{request.Name}' already exists.");
            }

            var maxOrder = existingCategories.Any() ? existingCategories.Max(c => c.DisplayOrder) : 0;

            var newCategory = new Entities.Category
            {
                RestaurantId = restaurantId,
                Name = request.Name,
                DisplayOrder = maxOrder + 1
            };

            var created = await _menuRepository.CreateCategoryAsync(newCategory, cancellationToken);
            await _hubContext.Clients.Group($"restaurant_{restaurantId}").SendAsync("MenuUpdated", cancellationToken: cancellationToken);
            await _hubContext.Clients.Group($"menu_{restaurantId}").SendAsync("MenuUpdated", cancellationToken: cancellationToken);

            return new CategoryDto
            {
                Id = created.Id,
                Name = created.Name,
                DisplayOrder = created.DisplayOrder,
                MenuItems = new System.Collections.Generic.List<MenuItemDto>()
            };
        }

        public async Task<CategoryDto> UpdateCategoryAsync(Guid categoryId, UpdateCategoryRequestDto request, CancellationToken cancellationToken = default)
        {
            var category = await _menuRepository.GetCategoryAsync(categoryId, cancellationToken);
            if (category == null) throw new ArgumentException("Category not found.");

            var restaurantCategories = await _menuRepository.GetAdminMenuCategoriesAsync(category.RestaurantId, cancellationToken);
            if (restaurantCategories.Any(c => c.Id != categoryId && c.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"A category with the name '{request.Name}' already exists in this restaurant.");
            }

            category.Name = request.Name;
            var updated = await _menuRepository.UpdateCategoryAsync(category, cancellationToken);
            await _hubContext.Clients.Group($"restaurant_{category.RestaurantId}").SendAsync("MenuUpdated", cancellationToken: cancellationToken);
            await _hubContext.Clients.Group($"menu_{category.RestaurantId}").SendAsync("MenuUpdated", cancellationToken: cancellationToken);

            return new CategoryDto
            {
                Id = updated.Id,
                Name = updated.Name,
                DisplayOrder = updated.DisplayOrder,
                MenuItems = updated.MenuItems?.Select(m => new MenuItemDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Description = m.Description,
                    Price = m.Price,
                    IsAvailable = m.IsAvailable,
                    ImageUrl = _imageStorageService.GetImageUrl(m.ImageUrl)
                }).ToList() ?? new System.Collections.Generic.List<MenuItemDto>()
            };
        }

        public async Task DeleteCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
        {
            var category = await _menuRepository.GetCategoryAsync(categoryId, cancellationToken);
            await _menuRepository.DeleteCategoryAsync(categoryId, cancellationToken);
            if (category != null) {
                await _hubContext.Clients.Group($"restaurant_{category.RestaurantId}").SendAsync("MenuUpdated", cancellationToken: cancellationToken);
                await _hubContext.Clients.Group($"menu_{category.RestaurantId}").SendAsync("MenuUpdated", cancellationToken: cancellationToken);
            }
        }

        public async Task<MenuItemDto> CreateMenuItemAsync(Guid restaurantId, CreateMenuItemRequestDto request, CancellationToken cancellationToken = default)
        {
            var category = await _menuRepository.GetCategoryAsync(request.CategoryId, cancellationToken);
            if (category == null || category.RestaurantId != restaurantId)
            {
                throw new ArgumentException("Category not found or does not belong to the specified restaurant.");
            }

            var newItem = new Entities.MenuItem
            {
                RestaurantId = restaurantId,
                CategoryId = request.CategoryId,
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                IsAvailable = request.IsAvailable,
                ImageUrl = request.ImageUrl
            };

            var created = await _menuRepository.CreateMenuItemAsync(newItem, cancellationToken);
            await _hubContext.Clients.Group($"restaurant_{restaurantId}").SendAsync("MenuUpdated", cancellationToken: cancellationToken);
            await _hubContext.Clients.Group($"menu_{restaurantId}").SendAsync("MenuUpdated", cancellationToken: cancellationToken);

            return new MenuItemDto
            {
                Id = created.Id,
                Name = created.Name,
                Description = created.Description,
                Price = created.Price,
                IsAvailable = created.IsAvailable,
                ImageUrl = _imageStorageService.GetImageUrl(created.ImageUrl)
            };
        }

        public async Task<MenuItemDto> UpdateMenuItemAsync(Guid menuItemId, UpdateMenuItemRequestDto request, CancellationToken cancellationToken = default)
        {
            var menuItem = await _menuRepository.GetMenuItemAsync(menuItemId, cancellationToken);
            if (menuItem == null) throw new ArgumentException("Menu item not found.");

            if (menuItem.CategoryId != request.CategoryId)
            {
                var newCategory = await _menuRepository.GetCategoryAsync(request.CategoryId, cancellationToken);
                if (newCategory == null || newCategory.RestaurantId != menuItem.RestaurantId)
                {
                    throw new ArgumentException("New category not found or does not belong to the restaurant.");
                }
            }

            menuItem.CategoryId = request.CategoryId;
            menuItem.Name = request.Name;
            menuItem.Description = request.Description;
            menuItem.Price = request.Price;
            menuItem.ImageUrl = request.ImageUrl;

            var updated = await _menuRepository.UpdateMenuItemAsync(menuItem, cancellationToken);
            await _hubContext.Clients.Group($"restaurant_{menuItem.RestaurantId}").SendAsync("MenuUpdated", cancellationToken: cancellationToken);
            await _hubContext.Clients.Group($"menu_{menuItem.RestaurantId}").SendAsync("MenuUpdated", cancellationToken: cancellationToken);

            return new MenuItemDto
            {
                Id = updated.Id,
                Name = updated.Name,
                Description = updated.Description,
                Price = updated.Price,
                IsAvailable = updated.IsAvailable,
                ImageUrl = _imageStorageService.GetImageUrl(updated.ImageUrl)
            };
        }

        public async Task<MenuItemDto> UpdateMenuItemAvailabilityAsync(Guid menuItemId, UpdateMenuItemAvailabilityRequestDto request, CancellationToken cancellationToken = default)
        {
            var menuItem = await _menuRepository.GetMenuItemAsync(menuItemId, cancellationToken);
            if (menuItem == null) throw new ArgumentException("Menu item not found.");

            menuItem.IsAvailable = request.IsAvailable;
            var updated = await _menuRepository.UpdateMenuItemAsync(menuItem, cancellationToken);
            await _hubContext.Clients.Group($"restaurant_{menuItem.RestaurantId}").SendAsync("MenuUpdated", cancellationToken: cancellationToken);
            await _hubContext.Clients.Group($"menu_{menuItem.RestaurantId}").SendAsync("MenuUpdated", cancellationToken: cancellationToken);

            return new MenuItemDto
            {
                Id = updated.Id,
                Name = updated.Name,
                Description = updated.Description,
                Price = updated.Price,
                IsAvailable = updated.IsAvailable,
                ImageUrl = _imageStorageService.GetImageUrl(updated.ImageUrl)
            };
        }

        public async Task DeleteMenuItemAsync(Guid menuItemId, CancellationToken cancellationToken = default)
        {
            var menuItem = await _menuRepository.GetMenuItemAsync(menuItemId, cancellationToken);
            await _menuRepository.SoftDeleteMenuItemAsync(menuItemId, cancellationToken);
            if (menuItem != null) {
                await _hubContext.Clients.Group($"restaurant_{menuItem.RestaurantId}").SendAsync("MenuUpdated", cancellationToken: cancellationToken);
                await _hubContext.Clients.Group($"menu_{menuItem.RestaurantId}").SendAsync("MenuUpdated", cancellationToken: cancellationToken);
            }
        }
    }
}
