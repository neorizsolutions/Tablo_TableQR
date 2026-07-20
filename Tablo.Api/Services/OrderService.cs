using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Tablo.Api.Hubs;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IHubContext<OrderHub> _hubContext;

        public OrderService(IOrderRepository orderRepository, IHubContext<OrderHub> hubContext)
        {
            _orderRepository = orderRepository;
            _hubContext = hubContext;
        }

        public async Task<OrderResponseDto> CreateOrderAsync(CreateOrderRequestDto request)
        {
            // Validate table using QRToken
            var table = await _orderRepository.GetTableByQRTokenAsync(request.QRToken);
            if (table == null)
            {
                throw new ArgumentException("Invalid table QR token.");
            }

            if (!table.IsActive)
            {
                throw new ArgumentException("This table is currently inactive and cannot accept orders.");
            }

            var order = new Order
            {
                RestaurantId = table.RestaurantId,
                TableId = table.Id,
                Status = "Placed",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                OrderItems = new List<OrderItem>()
            };

            decimal totalAmount = 0;

            // Validate and build items
            foreach (var itemRequest in request.Items)
            {
                var menuItem = await _orderRepository.GetMenuItemAsync(table.RestaurantId, itemRequest.MenuItemId);
                
                if (menuItem == null)
                {
                    throw new ArgumentException($"Menu item with ID {itemRequest.MenuItemId} not found in this restaurant.");
                }

                if (!menuItem.IsAvailable)
                {
                    throw new ArgumentException($"Menu item {menuItem.Name} is currently sold out or unavailable.");
                }

                if (itemRequest.Quantity <= 0)
                {
                    throw new ArgumentException($"Quantity for menu item {menuItem.Name} must be greater than 0.");
                }

                var orderItem = new OrderItem
                {
                    MenuItemId = menuItem.Id,
                    Quantity = itemRequest.Quantity,
                    UnitPrice = menuItem.Price
                };

                order.OrderItems.Add(orderItem);
                totalAmount += (orderItem.UnitPrice * orderItem.Quantity);
            }

            if (!order.OrderItems.Any())
            {
                throw new ArgumentException("Order must contain at least one valid item.");
            }

            order.TotalAmount = totalAmount;

            var createdOrder = await _orderRepository.CreateOrderAsync(order);

            return new OrderResponseDto
            {
                OrderId = createdOrder.Id,
                OrderNumber = createdOrder.OrderNumber,
                Status = createdOrder.Status,
                TotalAmount = createdOrder.TotalAmount,
                CreatedAt = createdOrder.CreatedAt
            };
        }

        public async Task<IEnumerable<OrderDto>> GetOrdersAsync(Guid restaurantId, string? status = null)
        {
            var orders = await _orderRepository.GetOrdersAsync(restaurantId, status);
            return orders.Select(MapToOrderDto);
        }

        public async Task<OrderDto> GetOrderByIdAsync(Guid restaurantId, Guid orderId)
        {
            var order = await _orderRepository.GetOrderByIdAsync(restaurantId, orderId);
            if (order == null)
            {
                throw new ArgumentException("Order not found or does not belong to the restaurant.");
            }
            return MapToOrderDto(order);
        }

        public async Task<OrderDto> UpdateOrderStatusAsync(Guid restaurantId, Guid orderId, UpdateOrderStatusRequestDto request)
        {
            var order = await _orderRepository.GetOrderByIdAsync(restaurantId, orderId);
            if (order == null)
            {
                throw new ArgumentException("Order not found or does not belong to the restaurant.");
            }

            ValidateStatusTransition(order.Status, request.Status);

            order.Status = request.Status;
            order.UpdatedAt = DateTime.UtcNow;

            await _orderRepository.UpdateOrderAsync(order);

            await _hubContext.Clients.All.SendAsync("OrderStatusUpdated", orderId, request.Status);

            return MapToOrderDto(order);
        }

        private void ValidateStatusTransition(string currentStatus, string newStatus)
        {
            if (currentStatus == newStatus)
                return;

            var validTransitions = new Dictionary<string, string>
            {
                { "Placed", "Accepted" },
                { "Accepted", "Preparing" },
                { "Preparing", "Ready" },
                { "Ready", "Served" }
            };

            if (validTransitions.TryGetValue(currentStatus, out var allowedNextStatus))
            {
                if (newStatus == allowedNextStatus)
                {
                    return;
                }
            }
            // Allow cancelling from placed? User said: Placed -> Accepted -> Preparing -> Ready. Do not allow other transitions.
            throw new ArgumentException($"Invalid status transition from {currentStatus} to {newStatus}.");
        }

        private OrderDto MapToOrderDto(Order order)
        {
            int tableNum = 0;
            if (order.Table != null && !string.IsNullOrEmpty(order.Table.NameOrNumber))
            {
                var tableStr = order.Table.NameOrNumber.Replace("Table ", "").Trim();
                int.TryParse(tableStr, out tableNum);
            }

            return new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                TableNumber = tableNum,
                TableId = order.TableId,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt,
                Items = order.OrderItems.Select(oi => new OrderItemDto
                {
                    Id = oi.Id,
                    MenuItemId = oi.MenuItemId,
                    Name = oi.MenuItem?.Name ?? string.Empty,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice
                }).ToList()
            };
        }
    }
}
