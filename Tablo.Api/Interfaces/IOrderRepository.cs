using Tablo.Api.Entities;

namespace Tablo.Api.Interfaces
{
    public interface IOrderRepository
    {
        Task<RestaurantTable?> GetTableByNumberAsync(Guid restaurantId, int tableNumber);
        Task<RestaurantTable?> GetTableByQRTokenAsync(string qrToken);
        Task<MenuItem?> GetMenuItemAsync(Guid restaurantId, Guid menuItemId);
        Task<Order> CreateOrderAsync(Order order);
        Task<IEnumerable<Order>> GetOrdersAsync(Guid restaurantId, string? status = null);
        Task<Order?> GetOrderByIdAsync(Guid restaurantId, Guid orderId);
        Task UpdateOrderAsync(Order order);
        Task<int> GetNextOrderNumberAsync(Guid restaurantId);
    }
}
