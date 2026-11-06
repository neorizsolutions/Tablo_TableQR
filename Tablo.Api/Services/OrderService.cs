using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Tablo.Api.Hubs;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;

namespace Tablo.Api.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrderService> _logger;

        public OrderService(IOrderRepository orderRepository, IHubContext<OrderHub> hubContext, ApplicationDbContext context, ILogger<OrderService> logger)
        {
            _orderRepository = orderRepository;
            _hubContext = hubContext;
            _context = context;
            _logger = logger;
        }

        public async Task<OrderResponseDto> CreateOrderAsync(CreateOrderRequestDto request)
        {
            // Validate table using QRToken
            var table = await _orderRepository.GetTableByQRTokenAsync(request.QRToken);
            if (table == null)
            {
                throw new ArgumentException("Invalid table QR token.");
            }

            await using var transaction = await TableOrderTransaction.BeginAsync(_context);
            await TableOrderTransaction.LockAsync(_context, table.RestaurantId, table.Id);
            await _context.Entry(table).ReloadAsync();
            if (table.QRToken != request.QRToken) throw new ArgumentException("Invalid table QR token.");

            if (!table.IsActive)
            {
                throw new ArgumentException("This table is currently inactive and cannot accept orders.");
            }

            var settings = await _orderRepository.GetRestaurantSettingsAsync(table.RestaurantId);
            if (settings != null && !settings.IsAcceptingOrders)
            {
                throw new ArgumentException("This restaurant is currently not accepting orders.");
            }

            var activeOrders = await _orderRepository.GetOrdersAsync(table.RestaurantId);
            var existingOrder = activeOrders
                .Where(o => o.TableId == table.Id && o.Status != "Served" && o.Status != "Cancelled" && o.Status != "Rejected")
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefault();

            bool isNewOrder = existingOrder == null;
            var order = existingOrder ?? new Order
            {
                RestaurantId = table.RestaurantId,
                TableId = table.Id,
                Status = "Placed",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                OrderItems = new List<OrderItem>()
            };

            if (!isNewOrder)
            {
                order.UpdatedAt = DateTime.UtcNow;
            }

            decimal subtotal = order.Subtotal;

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
                    ItemNameSnapshot = menuItem.Name, // Snapshot historical name
                    Quantity = itemRequest.Quantity,
                    UnitPrice = menuItem.Price
                };

                order.OrderItems.Add(orderItem);
                subtotal += (orderItem.UnitPrice * orderItem.Quantity);
            }

            if (!order.OrderItems.Any())
            {
                throw new ArgumentException("Order must contain at least one valid item.");
            }

            decimal gst = Math.Round(subtotal * 0.05m, 0, MidpointRounding.AwayFromZero);
            order.Subtotal = subtotal;
            order.Tax = gst;
            order.Discount = 0; // Discount currently not supported in requests
            order.TotalAmount = subtotal + gst;

            await BillingService.AttachAsync(_context, table, order);

            if (isNewOrder)
            {
                // Atomically fetch the next order number for this restaurant
                order.OrderNumber = await _orderRepository.GetNextOrderNumberAsync(table.RestaurantId);
                var createdOrder = await _orderRepository.CreateOrderAsync(order);
                order = createdOrder;
            }
            else
            {
                await _orderRepository.UpdateOrderAsync(order);
            }

            if (transaction != null) await transaction.CommitAsync();

            // Nested staff submissions notify after their enclosing receipt transaction commits.
            if (transaction != null || !_context.Database.IsRelational())
            {
                await NotifyAsync(order, isNewOrder);
            }

            return new OrderResponseDto
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt
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
            var tableId = await _context.Orders.Where(o => o.Id == orderId && o.RestaurantId == restaurantId)
                .Select(o => (Guid?)o.TableId).SingleOrDefaultAsync();
            if (!tableId.HasValue) throw new ArgumentException("Order not found or does not belong to the restaurant.");
            await using var transaction = await TableOrderTransaction.BeginAsync(_context);
            await TableOrderTransaction.LockAsync(_context, restaurantId, tableId.Value);
            var order = await _orderRepository.GetOrderByIdAsync(restaurantId, orderId);
            if (order == null)
            {
                throw new ArgumentException("Order not found or does not belong to the restaurant.");
            }

            ValidateStatusTransition(order.Status, request.Status);

            if (order.TableBillId.HasValue && await _context.TableBills.AnyAsync(b => b.Id == order.TableBillId && b.SettledAtUtc != null))
            {
                if (order.Status == request.Status) return MapToOrderDto(order);
                throw new ArgumentException("A settled order cannot be changed.");
            }

            order.Status = request.Status;
            order.UpdatedAt = DateTime.UtcNow;

            if (request.Status == "Rejected" || request.Status == "Cancelled")
            {
                order.ExceptionReason = request.ExceptionReason;
            }

            await _orderRepository.UpdateOrderAsync(order);
            if (transaction != null) await transaction.CommitAsync();
            await NotifyAsync(order, false);

            return MapToOrderDto(order);
        }

        private void ValidateStatusTransition(string currentStatus, string newStatus)
        {
            if (currentStatus == newStatus)
                return;

            if (newStatus == "Cancelled" && (currentStatus == "Placed" || currentStatus == "Accepted" || currentStatus == "Preparing" || currentStatus == "Ready"))
                return;

            if (newStatus == "Rejected" && currentStatus == "Placed")
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

            throw new ArgumentException($"Invalid status transition from {currentStatus} to {newStatus}.");
        }

        private async Task NotifyAsync(Order order, bool isNewOrder)
        {
            try
            {
                if (isNewOrder) await _hubContext.Clients.Group($"restaurant_{order.RestaurantId}").SendAsync("NewOrder", order.Id);
                else
                {
                    await _hubContext.Clients.Group($"restaurant_{order.RestaurantId}").SendAsync("OrderStatusUpdated", order.Id, order.Status);
                    await _hubContext.Clients.Group($"order_{order.Id}").SendAsync("OrderStatusUpdated", order.Id, order.Status);
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Order committed but notification failed for order {OrderId}", order.Id); }
        }

        private OrderDto MapToOrderDto(Order order)
        {
            int tableNum = 0;
            if (order.Table != null)
            {
                tableNum = order.Table.TableNumber;
            }

            return new OrderDto
            {
                Id = order.Id,
                RestaurantId = order.RestaurantId,
                OrderNumber = order.OrderNumber,
                TableNumber = tableNum,
                TableId = order.TableId,
                Status = order.Status,
                Subtotal = order.Subtotal,
                Tax = order.Tax,
                Discount = order.Discount,
                TotalAmount = order.TotalAmount,
                ExceptionReason = order.ExceptionReason,
                CreatedAt = order.CreatedAt,
                Items = order.OrderItems.Select(oi => new OrderItemDto
                {
                    Id = oi.Id,
                    MenuItemId = oi.MenuItemId,
                    Name = !string.IsNullOrEmpty(oi.ItemNameSnapshot) ? oi.ItemNameSnapshot : (oi.MenuItem?.Name ?? string.Empty),
                    ItemNameSnapshot = oi.ItemNameSnapshot,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice
                }).ToList()
            };
        }
    }
}
