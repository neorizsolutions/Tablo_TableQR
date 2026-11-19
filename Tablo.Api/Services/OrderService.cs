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

        public async Task<OrderResponseDto> CreateOrderAsync(CreateOrderRequestDto request, bool isStaff = false)
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

            var selectedGroup = await DiningGroupService.ResolveAsync(_context,table.RestaurantId,table.Id,request.DiningGroupId);
            var activeOrders = await _orderRepository.GetOrdersAsync(table.RestaurantId);
            Order? existingOrder = null;
            if (isStaff)
            {
                // If staff add items to an existing Accepted or Preparing order, preserve its status.
                // If the table/group only has an unaccepted QR order (Placed), do not attach to it:
                // keep the QR order pending, and create a separate Accepted ticket on the same bill.
                existingOrder = activeOrders
                    .Where(o => o.TableId == table.Id
                             && o.DiningGroupId == selectedGroup.Id
                             && (o.Status == "Accepted" || o.Status == "Preparing"))
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefault();
            }
            else
            {
                // Customer table QR order:
                // Starts as Placed (shown as New). If there is already an unaccepted Placed order,
                // customer additions append to it. If existing orders are Accepted or Preparing,
                // customer additions start as a separate Placed order requiring staff acceptance.
                existingOrder = activeOrders
                    .Where(o => o.TableId == table.Id
                             && o.DiningGroupId == selectedGroup.Id
                             && o.Status == "Placed")
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefault();
            }
            if(existingOrder?.TableBillId is {} existingBill && await _context.TableBills.AnyAsync(b=>b.Id==existingBill && (b.PaymentRevision!=null || b.SplitAtUtc!=null))) existingOrder=null;

            bool isNewOrder = existingOrder == null;
            var order = existingOrder ?? new Order
            {
                RestaurantId = table.RestaurantId,
                TableId = table.Id,
                DiningGroupId=selectedGroup.Id,GroupLabel=selectedGroup.Label,
                Status = isStaff ? "Accepted" : "Placed",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                OrderItems = new List<OrderItem>()
            };

            if (!isNewOrder)
            {
                order.UpdatedAt = DateTime.UtcNow;
            }

            decimal subtotal = order.Subtotal;
            var newlyAddedItems = new List<OrderItem>();

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
                newlyAddedItems.Add(orderItem);
                subtotal += (orderItem.UnitPrice * orderItem.Quantity);
            }

            if (!order.OrderItems.Any())
            {
                throw new ArgumentException("Order must contain at least one valid item.");
            }

            // Recipe and Stock Reservation verification
            var reqMenuItemIds = newlyAddedItems.Select(x => x.MenuItemId).Distinct().ToList();
            var itemRecipes = await _context.RecipeItems
                .Where(r => r.RestaurantId == table.RestaurantId && reqMenuItemIds.Contains(r.MenuItemId))
                .ToListAsync();

            if (itemRecipes.Count > 0 && settings != null && settings.AutoManageAvailability)
            {
                var ingIds = itemRecipes.Select(r => r.IngredientId).Distinct().ToList();
                var onHands = await _context.StockMovements.AsNoTracking()
                    .Where(m => m.RestaurantId == table.RestaurantId && ingIds.Contains(m.IngredientId))
                    .GroupBy(m => m.IngredientId)
                    .Select(g => new { IngredientId = g.Key, Qty = g.Sum(m => m.QuantityChange) })
                    .ToDictionaryAsync(x => x.IngredientId, x => x.Qty);

                var reserved = await _context.StockReservations.AsNoTracking()
                    .Where(r => r.RestaurantId == table.RestaurantId && ingIds.Contains(r.IngredientId) && r.Status == "Active")
                    .GroupBy(r => r.IngredientId)
                    .Select(g => new { IngredientId = g.Key, Reserved = g.Sum(r => r.ReservedQuantity) })
                    .ToDictionaryAsync(x => x.IngredientId, x => x.Reserved);

                foreach (var newItem in newlyAddedItems)
                {
                    var rList = itemRecipes.Where(r => r.MenuItemId == newItem.MenuItemId).ToList();
                    foreach (var r in rList)
                    {
                        var needed = UnitConverter.CalculateRawQuantity(r.UsableQuantity, r.YieldPercent) * newItem.Quantity;
                        var avail = onHands.GetValueOrDefault(r.IngredientId, 0m) - reserved.GetValueOrDefault(r.IngredientId, 0m);
                        if (avail < needed)
                        {
                            throw new ArgumentException($"Menu item '{newItem.ItemNameSnapshot}' does not have sufficient ingredient stock available.");
                        }
                    }
                }
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

            // Create stock reservations for newly added items
            if (itemRecipes.Count > 0)
            {
                foreach (var newItem in newlyAddedItems)
                {
                    var rList = itemRecipes.Where(r => r.MenuItemId == newItem.MenuItemId).ToList();
                    foreach (var r in rList)
                    {
                        var rawQty = UnitConverter.CalculateRawQuantity(r.UsableQuantity, r.YieldPercent) * newItem.Quantity;
                        _context.StockReservations.Add(new StockReservation
                        {
                            RestaurantId = table.RestaurantId,
                            OrderId = order.Id,
                            OrderItemId = newItem.Id,
                            IngredientId = r.IngredientId,
                            ReservedQuantity = rawQty,
                            Status = "Active",
                            ReservedAtUtc = DateTime.UtcNow
                        });
                    }
                }
                await _context.SaveChangesAsync();
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

            if(order.TableBillId is {} frozenId && await _context.TableBills.AnyAsync(b=>b.Id==frozenId && (b.PaymentRevision!=null || b.SplitAtUtc!=null)))
                throw new ArgumentException("A bill with payments or split allocations cannot change food status.");
            order.Status = request.Status;
            order.UpdatedAt = DateTime.UtcNow;

            if (request.Status == "Rejected" || request.Status == "Cancelled")
            {
                order.ExceptionReason = request.ExceptionReason;
                var activeRes = await _context.StockReservations
                    .Where(r => r.RestaurantId == restaurantId && r.OrderId == order.Id && r.Status == "Active")
                    .ToListAsync();
                foreach (var res in activeRes)
                {
                    res.Status = "Released";
                    res.ReleasedAtUtc = DateTime.UtcNow;
                }
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
                DiningGroupId=order.DiningGroupId,GroupLabel=order.GroupLabel,
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
