using Tablo.Api.DTOs;

namespace Tablo.Api.Interfaces
{
    public interface IOrderService
    {
        Task<OrderResponseDto> CreateOrderAsync(CreateOrderRequestDto request, bool isStaff = false);
        Task<IEnumerable<OrderDto>> GetOrdersAsync(Guid restaurantId, string? status = null);
        Task<OrderDto> GetOrderByIdAsync(Guid restaurantId, Guid orderId);
        Task<OrderDto> UpdateOrderStatusAsync(Guid restaurantId, Guid orderId, UpdateOrderStatusRequestDto request);
        Task<FiredItemsResponseDto> FireOrderItemsAsync(Guid restaurantId, Guid orderId, FireOrderItemsRequestDto request);
        Task<RefillResponseDto> CreateRefillAsync(Guid restaurantId, Guid orderId, Guid orderItemId, CreateRefillRequestDto request);
        Task<ItemCancellationResponseDto> CancelOrderItemAsync(Guid restaurantId, Guid orderId, Guid orderItemId, CancelOrderItemRequestDto request);
    }
}
