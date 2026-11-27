using System.Text.Json;
using Tablo.Api.Constants;
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
        private readonly Tablo.Api.Services.Printing.IKotRoutingService _kotRoutingService;
        private readonly ISubscriptionService _subscriptions;
        private readonly ManagerAuthService _managerAuth;
        private readonly IngredientResolutionService _ingredientResolver;

        public OrderService(
            IOrderRepository orderRepository,
            IHubContext<OrderHub> hubContext,
            ApplicationDbContext context,
            ILogger<OrderService> logger,
            Tablo.Api.Services.Printing.IKotRoutingService? kotRoutingService = null,
            ISubscriptionService? subscriptions = null,
            ManagerAuthService? managerAuth = null,
            IngredientResolutionService? ingredientResolver = null)
        {
            _orderRepository = orderRepository;
            _hubContext = hubContext;
            _context = context;
            _logger = logger;
            _subscriptions = subscriptions ?? new SubscriptionService(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<SubscriptionService>.Instance);
            _kotRoutingService = kotRoutingService ?? new Tablo.Api.Services.Printing.KotRoutingService(
                context,
                _subscriptions,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Tablo.Api.Services.Printing.KotRoutingService>.Instance);
            _managerAuth = managerAuth ?? new ManagerAuthService(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<ManagerAuthService>.Instance);
            _ingredientResolver = ingredientResolver ?? new IngredientResolutionService(context);
        }

        private static string ComputePayloadHash(string rawPayload)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(rawPayload);
            return Convert.ToHexString(sha.ComputeHash(bytes));
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

            // Capability checks
            bool hasModifiersCap = await _subscriptions.HasCapabilityAsync(table.RestaurantId, Capabilities.ItemModifiers);
            bool hasHoldFireCap = await _subscriptions.HasCapabilityAsync(table.RestaurantId, Capabilities.HoldFireCourses);

            var selectedGroup = await DiningGroupService.ResolveAsync(_context, table.RestaurantId, table.Id, request.DiningGroupId);
            var activeOrders = await _orderRepository.GetOrdersAsync(table.RestaurantId);
            Order? existingOrder = null;
            if (isStaff)
            {
                existingOrder = activeOrders
                    .Where(o => o.TableId == table.Id
                             && o.DiningGroupId == selectedGroup.Id
                             && (o.Status == "Accepted" || o.Status == "Preparing"))
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefault();
            }
            else
            {
                existingOrder = activeOrders
                    .Where(o => o.TableId == table.Id
                             && o.DiningGroupId == selectedGroup.Id
                             && o.Status == "Placed")
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefault();
            }
            if (existingOrder?.TableBillId is { } existingBill && await _context.TableBills.AnyAsync(b => b.Id == existingBill && (b.PaymentRevision != null || b.SplitAtUtc != null))) existingOrder = null;

            bool isNewOrder = existingOrder == null;
            var order = existingOrder ?? new Order
            {
                RestaurantId = table.RestaurantId,
                TableId = table.Id,
                DiningGroupId = selectedGroup.Id,
                GroupLabel = selectedGroup.Label,
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

            // Load menu items and modifier groups for validation
            var reqMenuItemIds = request.Items.Select(x => x.MenuItemId).Distinct().ToList();
            var menuItemsMap = await _context.MenuItems
                .Include(m => m.Category)
                .Include(m => m.ModifierGroups)
                    .ThenInclude(mmg => mmg.ModifierGroup)
                        .ThenInclude(mg => mg.Options)
                .Where(m => m.RestaurantId == table.RestaurantId && reqMenuItemIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id);

            // Validate and build items
            foreach (var itemRequest in request.Items)
            {
                if (!menuItemsMap.TryGetValue(itemRequest.MenuItemId, out var menuItem))
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

                // Modifiers validation & pricing
                decimal unitPrice = menuItem.Price;
                string? modifiersJson = null;

                if (itemRequest.SelectedModifiers != null && itemRequest.SelectedModifiers.Count > 0)
                {
                    if (!hasModifiersCap)
                    {
                        throw new InvalidOperationException("Priced modifiers and swaps are not included in the restaurant's subscription plan.");
                    }

                    var snapshots = new List<SelectedModifierSnapshotDto>();
                    var groupedRequests = itemRequest.SelectedModifiers.GroupBy(s => s.ModifierGroupId);

                    foreach (var grp in groupedRequests)
                    {
                        var mmg = menuItem.ModifierGroups.FirstOrDefault(m => m.ModifierGroupId == grp.Key);
                        var modGroup = mmg?.ModifierGroup;
                        if (modGroup == null || !modGroup.IsActive)
                        {
                            throw new ArgumentException($"Modifier group {grp.Key} is not valid for item {menuItem.Name}.");
                        }

                        var selectedOptions = grp.Select(s => s.ModifierOptionId).Distinct().ToList();
                        if (selectedOptions.Count > modGroup.MaxSelections)
                        {
                            throw new ArgumentException($"Too many selections for modifier group '{modGroup.Name}'. Maximum allowed is {modGroup.MaxSelections}.");
                        }

                        foreach (var optId in selectedOptions)
                        {
                            var option = modGroup.Options.FirstOrDefault(o => o.Id == optId && o.IsAvailable);
                            if (option == null)
                            {
                                throw new ArgumentException($"Selected modifier option {optId} is not available in group '{modGroup.Name}'.");
                            }

                            unitPrice += option.PriceDelta;
                            snapshots.Add(new SelectedModifierSnapshotDto
                            {
                                ModifierGroupId = modGroup.Id,
                                GroupName = modGroup.Name,
                                ModifierOptionId = option.Id,
                                OptionName = option.Name,
                                PriceDelta = option.PriceDelta,
                                IngredientAdjustmentType = option.IngredientAdjustmentType,
                                IngredientId = option.IngredientId,
                                IngredientName = option.Ingredient?.Name,
                                IngredientQuantity = option.IngredientQuantity,
                                OriginalIngredientId = option.OriginalIngredientId,
                                OriginalIngredientName = option.OriginalIngredient?.Name
                            });
                        }
                    }

                    // Check min selections for required groups
                    foreach (var mmg in menuItem.ModifierGroups)
                    {
                        var mg = mmg.ModifierGroup;
                        if (mg.IsActive && mg.MinSelections > 0)
                        {
                            var count = itemRequest.SelectedModifiers.Count(s => s.ModifierGroupId == mg.Id);
                            if (count < mg.MinSelections)
                            {
                                throw new ArgumentException($"Modifier group '{mg.Name}' requires at least {mg.MinSelections} selection(s).");
                            }
                        }
                    }

                    modifiersJson = JsonSerializer.Serialize(snapshots);
                }
                else
                {
                    // Check if any active group requires selection
                    var requiredGroup = menuItem.ModifierGroups.FirstOrDefault(mmg => mmg.ModifierGroup.IsActive && mmg.ModifierGroup.MinSelections > 0);
                    if (requiredGroup != null)
                    {
                        throw new ArgumentException($"Modifier group '{requiredGroup.ModifierGroup.Name}' requires at least {requiredGroup.ModifierGroup.MinSelections} selection(s) for {menuItem.Name}.");
                    }
                }

                // Hold/Fire Release State & Course
                string releaseState = "Fired";
                string? courseName = itemRequest.CourseName;

                if (isStaff && itemRequest.ReleaseState == "Held")
                {
                    if (!hasHoldFireCap)
                    {
                        throw new InvalidOperationException("Hold/Fire course management is not included in the restaurant's subscription plan.");
                    }
                    releaseState = "Held";
                }
                else if (itemRequest.ReleaseState == "Held")
                {
                    // Customer QR orders cannot place items on hold
                    releaseState = "Fired";
                }

                if (!string.IsNullOrWhiteSpace(courseName) && !hasHoldFireCap)
                {
                    courseName = null;
                }

                var orderItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    MenuItemId = menuItem.Id,
                    ItemNameSnapshot = menuItem.Name,
                    Quantity = itemRequest.Quantity,
                    UnitPrice = Math.Max(0m, unitPrice),
                    SpecialInstructions = itemRequest.SpecialInstructions,
                    ReleaseState = releaseState,
                    CourseName = courseName,
                    ModifiersJson = modifiersJson,
                    IsRefill = false,
                    IsCancelled = false,
                    CancelledQuantity = 0,
                    PackingIntent = (isStaff && itemRequest.PackingIntent == "Takeaway") ? "Takeaway" : "DineIn"
                };

                order.OrderItems.Add(orderItem);
                newlyAddedItems.Add(orderItem);
                subtotal += (orderItem.UnitPrice * orderItem.Quantity);
            }

            if (!order.OrderItems.Any())
            {
                throw new ArgumentException("Order must contain at least one valid item.");
            }

            // Authoritative ingredient resolution taking modifier swaps/adds/removes into account
            var resolvedItemIngredients = await _ingredientResolver.ResolveItemIngredientsAsync(
                table.RestaurantId,
                newlyAddedItems.Select(i => (i.MenuItemId, i.ItemNameSnapshot, i.Quantity, i.ModifiersJson, i.IsRefill)));

            var requiredIngredients = new Dictionary<Guid, (string Name, decimal Quantity)>();
            foreach (var ri in resolvedItemIngredients)
            {
                foreach (var ing in ri.Ingredients)
                {
                    if (requiredIngredients.TryGetValue(ing.IngredientId, out var existing))
                    {
                        requiredIngredients[ing.IngredientId] = (ing.IngredientName, existing.Quantity + ing.RawQuantity);
                    }
                    else
                    {
                        requiredIngredients[ing.IngredientId] = (ing.IngredientName, ing.RawQuantity);
                    }
                }
            }

            if (requiredIngredients.Count > 0 && settings != null && settings.AutoManageAvailability)
            {
                var ingIds = requiredIngredients.Keys.ToList();
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

                foreach (var (ingId, (ingName, neededQty)) in requiredIngredients)
                {
                    var avail = onHands.GetValueOrDefault(ingId, 0m) - reserved.GetValueOrDefault(ingId, 0m);
                    if (avail < neededQty)
                    {
                        throw new ArgumentException($"Insufficient stock available for {ingName}.");
                    }
                }
            }

            // Calculate packaging charges for any takeaway items on dine-in orders
            decimal packagingCharge = 0m;
            var takeawayItems = newlyAddedItems.Where(i => i.PackingIntent == "Takeaway").ToList();
            if (takeawayItems.Count > 0 && settings != null && settings.HybridDineInParcelEnabled)
            {
                bool hasHybridCap = await _subscriptions.HasCapabilityAsync(table.RestaurantId, Capabilities.HybridDineInParcel);
                if (hasHybridCap)
                {
                    string chargeMode = settings.ParcelPackagingChargeMode ?? "None";
                    switch (chargeMode)
                    {
                        case "PerItem":
                            foreach (var ti in takeawayItems)
                            {
                                if (menuItemsMap.TryGetValue(ti.MenuItemId, out var mi))
                                {
                                    packagingCharge += (mi.PackagingFee ?? 0m) * ti.Quantity;
                                }
                            }
                            break;
                        case "FlatPerOrder":
                            packagingCharge = settings.ParcelPackagingFlatRate;
                            break;
                        case "Percentage":
                            decimal takeawaySubtotal = takeawayItems.Sum(ti => ti.UnitPrice * ti.Quantity);
                            decimal pct = settings.ParcelPackagingPercentage;
                            packagingCharge = Math.Round(takeawaySubtotal * (pct / 100m), 2, MidpointRounding.AwayFromZero);
                            break;
                    }
                }
            }

            packagingCharge = Math.Max(0m, Math.Round(packagingCharge, 2, MidpointRounding.AwayFromZero));
            order.PackagingCharge = (order.PackagingCharge) + packagingCharge;

            decimal gst = Math.Round(subtotal * 0.05m, 0, MidpointRounding.AwayFromZero);
            order.Subtotal = subtotal;
            order.Tax = gst;
            order.Discount = 0;
            order.TotalAmount = subtotal + order.PackagingCharge + gst;

            await BillingService.AttachAsync(_context, table, order);

            if (isNewOrder)
            {
                order.OrderNumber = await _orderRepository.GetNextOrderNumberAsync(table.RestaurantId);
                var createdOrder = await _orderRepository.CreateOrderAsync(order);
                order = createdOrder;
            }
            else
            {
                // These lines have application-assigned IDs but do not exist in the
                // database yet. Track them as inserts before updating the order graph.
                _context.OrderItems.AddRange(newlyAddedItems);
                await _orderRepository.UpdateOrderAsync(order);
            }

            // Create stock reservations for newly added items (both Fired and Held reserve stock)
            for (int i = 0; i < newlyAddedItems.Count && i < resolvedItemIngredients.Count; i++)
            {
                var newItem = newlyAddedItems[i];
                var itemRes = resolvedItemIngredients[i];
                foreach (var ing in itemRes.Ingredients)
                {
                    _context.StockReservations.Add(new StockReservation
                    {
                        RestaurantId = table.RestaurantId,
                        OrderId = order.Id,
                        OrderItemId = newItem.Id,
                        IngredientId = ing.IngredientId,
                        ReservedQuantity = ing.RawQuantity,
                        Status = "Active",
                        ReservedAtUtc = DateTime.UtcNow
                    });
                }
            }
            await _context.SaveChangesAsync();

            // Print KOT: only print FIRED items! Held items produce NO KOT until released!
            if (isStaff && order.Status == "Accepted")
            {
                var itemsToPrint = newlyAddedItems.Where(i => i.ReleaseState == "Fired").ToList();
                if (itemsToPrint.Count > 0)
                {
                    try
                    {
                        await _kotRoutingService.ProcessAcceptedOrderItemsAsync(
                            order,
                            itemsToPrint,
                            actorEmail: "Staff",
                            trigger: isNewOrder ? "StaffOrder" : "StaffAddition");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to generate KOT jobs for staff order {OrderId}", order.Id);
                        order.KotPrintStatus = "Failed";
                        order.KotPrintError = ex.Message;
                    }
                }
            }

            if (transaction != null) await transaction.CommitAsync();

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
            var orderInfo = await _context.Orders.Where(o => o.Id == orderId && o.RestaurantId == restaurantId)
                .Select(o => new { o.TableId, o.OrderChannel }).SingleOrDefaultAsync();
            if (orderInfo == null) throw new ArgumentException("Order not found or does not belong to the restaurant.");

            await using var transaction = await TableOrderTransaction.BeginAsync(_context);
            if (orderInfo.TableId.HasValue)
            {
                await TableOrderTransaction.LockAsync(_context, restaurantId, orderInfo.TableId.Value);
            }
            var order = await _orderRepository.GetOrderByIdAsync(restaurantId, orderId);
            if (order == null)
            {
                throw new ArgumentException("Order not found or does not belong to the restaurant.");
            }

            if (order.TableId != orderInfo.TableId)
            {
                throw new ServiceConflictException("Table ownership changed due to a concurrent transfer. Please refresh and retry.");
            }

            ValidateStatusTransition(order.Status, request.Status);

            if (order.TableBillId.HasValue && await _context.TableBills.AnyAsync(b => b.Id == order.TableBillId && b.SettledAtUtc != null))
            {
                if (order.Status == request.Status) return MapToOrderDto(order);
                throw new ArgumentException("A settled order cannot be changed.");
            }

            if (order.TableBillId is { } frozenId && await _context.TableBills.AnyAsync(b => b.Id == frozenId && (b.PaymentRevision != null || b.SplitAtUtc != null)))
                throw new ArgumentException("A bill with payments or split allocations cannot change food status.");

            string previousStatus = order.Status;
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

            if (previousStatus == "Placed" && request.Status == "Accepted")
            {
                // Customer QR acceptance: print ONLY items that are FIRED! Held items must NOT be released!
                var itemsToPrint = order.OrderItems.Where(i => i.ReleaseState == "Fired" && !i.IsCancelled).ToList();
                if (itemsToPrint.Count > 0)
                {
                    try
                    {
                        await _kotRoutingService.ProcessAcceptedOrderItemsAsync(
                            order,
                            itemsToPrint,
                            actorEmail: "Staff",
                            trigger: "CustomerAccepted");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to generate KOT jobs for accepted order {OrderId}", order.Id);
                        order.KotPrintStatus = "Failed";
                        order.KotPrintError = ex.Message;
                    }
                }
            }
            else if (request.Status == "Cancelled" || request.Status == "Rejected")
            {
                try
                {
                    await _kotRoutingService.RecordOrderCancellationNoticeAsync(
                        restaurantId,
                        order.Id,
                        request.ExceptionReason ?? "Order cancelled");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to record cancellation notice for order {OrderId}", order.Id);
                }
            }

            await _orderRepository.UpdateOrderAsync(order);
            if (transaction != null) await transaction.CommitAsync();
            await NotifyAsync(order, false);

            return MapToOrderDto(order);
        }

        public async Task<FiredItemsResponseDto> FireOrderItemsAsync(Guid restaurantId, Guid orderId, FireOrderItemsRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.OperationId))
            {
                throw new ArgumentException("OperationId is required for firing order items.");
            }

            bool hasHoldFireCap = await _subscriptions.HasCapabilityAsync(restaurantId, Capabilities.HoldFireCourses);
            if (!hasHoldFireCap)
            {
                throw new InvalidOperationException("Hold/Fire course management is not included in the restaurant's subscription plan.");
            }

            var payloadHash = ComputePayloadHash($"Fire:{orderId}:{string.Join(",", (request.OrderItemIds ?? new List<Guid>()).OrderBy(x => x))}:{request.CourseName?.Trim().ToLowerInvariant()}");

            const int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                _context.ChangeTracker.Clear();

                var orderInfo = await _context.Orders.AsNoTracking()
                    .Where(o => o.Id == orderId && o.RestaurantId == restaurantId)
                    .Select(o => new { o.TableId, o.TableBillId }).SingleOrDefaultAsync();
                if (orderInfo == null)
                {
                    throw new ArgumentException("Order not found or does not belong to the restaurant.");
                }

                await using var transaction = await TableOrderTransaction.BeginAsync(_context);
                if (orderInfo.TableId.HasValue)
                {
                    await TableOrderTransaction.LockAsync(_context, restaurantId, orderInfo.TableId.Value);
                }
                if (orderInfo.TableBillId.HasValue)
                {
                    await TableOrderTransaction.LockBillAsync(_context, restaurantId, orderInfo.TableBillId.Value);
                }
                await TableOrderTransaction.LockOrderAsync(_context, restaurantId, orderId);

                // Reload order inside the lock
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId);
                if (order == null)
                {
                    throw new ArgumentException("Order not found or does not belong to the restaurant.");
                }

                // If ownership changed, rollback and retry with clean EF state - NEVER acquire locks out-of-order!
                if (order.TableId != orderInfo.TableId || order.TableBillId != orderInfo.TableBillId)
                {
                    if (transaction != null) await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    if (attempt < maxRetries - 1)
                    {
                        continue;
                    }
                    throw new ServiceConflictException("Table or bill ownership changed due to a concurrent transfer. Please refresh and retry.");
                }

                // Check OrderProductionOperations for idempotent replay or parameter conflict
                var existingOp = await _context.OrderProductionOperations
                    .FirstOrDefaultAsync(o => o.RestaurantId == restaurantId && o.OperationId == request.OperationId);
                if (existingOp != null)
                {
                    if (existingOp.PayloadHash != payloadHash)
                    {
                        throw new ServiceConflictException("Operation ID was already used with different parameters.");
                    }
                    return JsonSerializer.Deserialize<FiredItemsResponseDto>(existingOp.OriginalResponseJson)!;
                }

                if (order.TableBillId.HasValue)
                {
                    var bill = await _context.TableBills.FirstOrDefaultAsync(b => b.Id == order.TableBillId.Value && b.RestaurantId == restaurantId);
                    if (bill != null && bill.SettledAtUtc != null)
                    {
                        throw new ServiceConflictException("Cannot fire items on a settled bill.");
                    }
                }

                // Find matching held items
                var heldItemsQuery = order.OrderItems.Where(i => i.ReleaseState == "Held" && !i.IsCancelled);
                if (request.OrderItemIds != null && request.OrderItemIds.Count > 0)
                {
                    heldItemsQuery = heldItemsQuery.Where(i => request.OrderItemIds.Contains(i.Id));
                }
                else if (!string.IsNullOrWhiteSpace(request.CourseName))
                {
                    heldItemsQuery = heldItemsQuery.Where(i => string.Equals(i.CourseName, request.CourseName, StringComparison.OrdinalIgnoreCase));
                }

                var itemsToFire = heldItemsQuery.ToList();
                if (itemsToFire.Count == 0)
                {
                    var emptyResponse = new FiredItemsResponseDto
                    {
                        OrderId = order.Id,
                        FiredItemCount = 0,
                        FiredOrderItemIds = new List<Guid>(),
                        GeneratedKotTicketsCount = 0,
                        Message = "No matching held items found to release."
                    };

                    _context.OrderProductionOperations.Add(new OrderProductionOperation
                    {
                        RestaurantId = restaurantId,
                        OrderId = order.Id,
                        OperationType = "Fire",
                        OperationId = request.OperationId,
                        PayloadHash = payloadHash,
                        OriginalResponseJson = JsonSerializer.Serialize(emptyResponse),
                        CreatedAtUtc = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                    if (transaction != null) await transaction.CommitAsync();

                    return emptyResponse;
                }

                DateTime now = DateTime.UtcNow;
                foreach (var item in itemsToFire)
                {
                    item.ReleaseState = "Fired";
                    item.FiredAtUtc = now;
                    item.FiredByEmployeeId = request.FiredByEmployeeId;
                    item.FireOperationId = request.OperationId;
                }

                order.UpdatedAt = now;

                // Route KOT tickets for newly released items
                var createdTickets = await _kotRoutingService.ProcessAcceptedOrderItemsAsync(
                    order,
                    itemsToFire,
                    actorEmail: "Staff",
                    trigger: "CourseFire");

                await _orderRepository.UpdateOrderAsync(order);

                var responseDto = new FiredItemsResponseDto
                {
                    OrderId = order.Id,
                    FiredItemCount = itemsToFire.Count,
                    FiredOrderItemIds = itemsToFire.Select(i => i.Id).ToList(),
                    GeneratedKotTicketsCount = createdTickets.Count,
                    KotTicketNumbers = createdTickets.Select(t => t.TicketNumber).ToList(),
                    Message = $"Successfully released {itemsToFire.Count} item(s) to the kitchen."
                };

                _context.OrderProductionOperations.Add(new OrderProductionOperation
                {
                    RestaurantId = restaurantId,
                    OrderId = order.Id,
                    OperationType = "Fire",
                    OperationId = request.OperationId,
                    PayloadHash = payloadHash,
                    OriginalResponseJson = JsonSerializer.Serialize(responseDto),
                    CreatedAtUtc = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();

                await NotifyAsync(order, false);

                return responseDto;
            }

            throw new ServiceConflictException("Table or bill ownership changed due to a concurrent transfer. Please refresh and retry.");
        }

        public async Task<RefillResponseDto> CreateRefillAsync(Guid restaurantId, Guid orderId, Guid orderItemId, CreateRefillRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.OperationId))
            {
                throw new ArgumentException("OperationId is required for refill request.");
            }

            bool hasRefillCap = await _subscriptions.HasCapabilityAsync(restaurantId, Capabilities.FreeRefills);
            if (!hasRefillCap)
            {
                throw new InvalidOperationException("Free refills feature is not included in the restaurant's subscription plan.");
            }

            var payloadHash = ComputePayloadHash($"Refill:{orderId}:{orderItemId}:{request.Quantity}:{request.Notes?.Trim()}");

            const int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                _context.ChangeTracker.Clear();

                var orderInfo = await _context.Orders.AsNoTracking()
                    .Where(o => o.Id == orderId && o.RestaurantId == restaurantId)
                    .Select(o => new { o.TableId, o.TableBillId }).SingleOrDefaultAsync();
                if (orderInfo == null)
                {
                    throw new ArgumentException("Order not found or does not belong to the restaurant.");
                }

                await using var transaction = await TableOrderTransaction.BeginAsync(_context);
                if (orderInfo.TableId.HasValue)
                {
                    await TableOrderTransaction.LockAsync(_context, restaurantId, orderInfo.TableId.Value);
                }
                if (orderInfo.TableBillId.HasValue)
                {
                    await TableOrderTransaction.LockBillAsync(_context, restaurantId, orderInfo.TableBillId.Value);
                }
                await TableOrderTransaction.LockOrderAsync(_context, restaurantId, orderId);

                // Reload order inside the lock
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId);
                if (order == null)
                {
                    throw new ArgumentException("Order not found or does not belong to the restaurant.");
                }

                // If ownership changed, rollback and retry with clean EF state - NEVER acquire locks out-of-order!
                if (order.TableId != orderInfo.TableId || order.TableBillId != orderInfo.TableBillId)
                {
                    if (transaction != null) await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    if (attempt < maxRetries - 1)
                    {
                        continue;
                    }
                    throw new ServiceConflictException("Table or bill ownership changed due to a concurrent transfer. Please refresh and retry.");
                }

                // Check OrderProductionOperations for idempotent replay or parameter conflict
                var existingOp = await _context.OrderProductionOperations
                    .FirstOrDefaultAsync(o => o.RestaurantId == restaurantId && o.OperationId == request.OperationId);
                if (existingOp != null)
                {
                    if (existingOp.PayloadHash != payloadHash)
                    {
                        throw new ServiceConflictException("Operation ID was already used with different parameters.");
                    }
                    return JsonSerializer.Deserialize<RefillResponseDto>(existingOp.OriginalResponseJson)!;
                }

                var originalItem = order.OrderItems.FirstOrDefault(i => i.Id == orderItemId);
                if (originalItem == null)
                {
                    throw new ArgumentException("Original order item not found.");
                }

                var menuItem = await _context.MenuItems.FirstOrDefaultAsync(m => m.Id == originalItem.MenuItemId && m.RestaurantId == restaurantId);
                if (menuItem == null || !menuItem.IsRefillEligible)
                {
                    throw new ArgumentException($"Menu item '{originalItem.ItemNameSnapshot}' is not configured for free refills.");
                }

                int existingCount = order.OrderItems.Count(i => i.OriginalOrderItemId == originalItem.Id && !i.IsCancelled && i.IsRefill);
                if (existingCount >= menuItem.RefillMaxCount)
                {
                    throw new ArgumentException($"Maximum refill limit ({menuItem.RefillMaxCount}) reached for '{originalItem.ItemNameSnapshot}'.");
                }

                int refillSeq = existingCount + 1;
                var refillItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    MenuItemId = originalItem.MenuItemId,
                    ItemNameSnapshot = $"{originalItem.ItemNameSnapshot} (Refill #{refillSeq})",
                    Quantity = request.Quantity,
                    UnitPrice = 0.00m, // Free refill is zero food charge
                    SpecialInstructions = request.Notes,
                    ReleaseState = "Fired",
                    CourseName = originalItem.CourseName,
                    ModifiersJson = originalItem.ModifiersJson,
                    IsRefill = true,
                    OriginalOrderItemId = originalItem.Id,
                    RefillCount = refillSeq,
                    FireOperationId = request.OperationId,
                    FiredAtUtc = DateTime.UtcNow,
                    IsCancelled = false,
                    CancelledQuantity = 0
                };

                order.OrderItems.Add(refillItem);
                order.UpdatedAt = DateTime.UtcNow;

                // Handle stock/inventory for refill through authoritative ingredient resolver
                bool isSettled = order.TableBillId.HasValue && await _context.TableBills.AnyAsync(b => b.Id == order.TableBillId && b.SettledAtUtc != null);
                var resolvedIngredients = await _ingredientResolver.ResolveIngredientsAsync(
                    restaurantId,
                    new[] { (refillItem.MenuItemId, refillItem.Quantity, refillItem.ModifiersJson, true) });

                foreach (var r in resolvedIngredients)
                {
                    if (isSettled)
                    {
                        // Post consumption immediately even after bill settled
                        _context.StockMovements.Add(new StockMovement
                        {
                            RestaurantId = restaurantId,
                            IngredientId = r.IngredientId,
                            ActorEmployeeId = request.ActorEmployeeId,
                            TimestampUtc = DateTime.UtcNow,
                            QuantityChange = -r.RawQuantity,
                            UnitSnapshot = r.BaseUnit,
                            Reason = "RefillConsumption",
                            UnitCostSnapshot = r.UnitCost,
                            TotalCost = r.ExtendedCost,
                            ReferenceType = "RefillAfterSettlement",
                            ReferenceId = refillItem.Id.ToString(),
                            Notes = $"Free refill #{refillSeq} for {originalItem.ItemNameSnapshot}"
                        });
                    }
                    else
                    {
                        // Reserve stock
                        _context.StockReservations.Add(new StockReservation
                        {
                            RestaurantId = restaurantId,
                            OrderId = order.Id,
                            OrderItemId = refillItem.Id,
                            IngredientId = r.IngredientId,
                            ReservedQuantity = r.RawQuantity,
                            Status = "Active",
                            ReservedAtUtc = DateTime.UtcNow
                        });
                    }
                }

                await _context.SaveChangesAsync();

                // Dispatch refill KOT ticket with prominent refill trigger
                var tickets = await _kotRoutingService.ProcessAcceptedOrderItemsAsync(
                    order,
                    new List<OrderItem> { refillItem },
                    actorEmail: "Staff",
                    trigger: "Refill");

                var responseDto = new RefillResponseDto
                {
                    OrderId = order.Id,
                    RefillOrderItemId = refillItem.Id,
                    OriginalOrderItemId = originalItem.Id,
                    ItemName = refillItem.ItemNameSnapshot,
                    Quantity = refillItem.Quantity,
                    RefillCount = refillSeq,
                    PriceCharged = 0.00m,
                    KotTicketNumber = tickets.FirstOrDefault()?.TicketNumber ?? string.Empty,
                    Message = $"Refill #{refillSeq} created and dispatched to kitchen."
                };

                _context.OrderProductionOperations.Add(new OrderProductionOperation
                {
                    RestaurantId = restaurantId,
                    OrderId = order.Id,
                    OperationType = "Refill",
                    OperationId = request.OperationId,
                    PayloadHash = payloadHash,
                    OriginalResponseJson = JsonSerializer.Serialize(responseDto),
                    CreatedAtUtc = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();

                await NotifyAsync(order, false);

                return responseDto;
            }

            throw new ServiceConflictException("Table or bill ownership changed due to a concurrent transfer. Please refresh and retry.");
        }

        public async Task<ItemCancellationResponseDto> CancelOrderItemAsync(Guid restaurantId, Guid orderId, Guid orderItemId, CancelOrderItemRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.OperationId))
            {
                throw new ArgumentException("OperationId is required.");
            }

            bool hasWasteCap = await _subscriptions.HasCapabilityAsync(restaurantId, Capabilities.ManagerWasteCancellation);
            if (!hasWasteCap)
            {
                throw new InvalidOperationException("Manager waste cancellation is not included in the restaurant's subscription plan.");
            }

            var payloadHash = ComputePayloadHash($"Cancel:{orderId}:{orderItemId}:{request.Quantity}:{request.Outcome?.Trim()}:{request.Reason?.Trim()}");

            const int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                _context.ChangeTracker.Clear();

                var orderInfo = await _context.Orders.AsNoTracking()
                    .Where(o => o.Id == orderId && o.RestaurantId == restaurantId)
                    .Select(o => new { o.TableId, o.TableBillId }).SingleOrDefaultAsync();
                if (orderInfo == null)
                {
                    throw new ArgumentException("Order not found or does not belong to the restaurant.");
                }

                // Check if bill is settled or frozen
                if (orderInfo.TableBillId.HasValue && await _context.TableBills.AnyAsync(b => b.Id == orderInfo.TableBillId && b.SettledAtUtc != null))
                {
                    throw new ServiceConflictException("Cannot cancel items on a settled bill.");
                }

                if (orderInfo.TableBillId is { } frozenId && await _context.TableBills.AnyAsync(b => b.Id == frozenId && (b.PaymentRevision != null || b.SplitAtUtc != null)))
                {
                    throw new ServiceConflictException("Cannot cancel items on a bill with active payments or splits.");
                }

                await using var transaction = await TableOrderTransaction.BeginAsync(_context);
                if (orderInfo.TableId.HasValue)
                {
                    await TableOrderTransaction.LockAsync(_context, restaurantId, orderInfo.TableId.Value);
                }
                if (orderInfo.TableBillId.HasValue)
                {
                    await TableOrderTransaction.LockBillAsync(_context, restaurantId, orderInfo.TableBillId.Value);
                }
                await TableOrderTransaction.LockOrderAsync(_context, restaurantId, orderId);

                // Reload order inside the lock
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId);
                if (order == null)
                {
                    throw new ArgumentException("Order not found or does not belong to the restaurant.");
                }

                // If ownership changed, rollback and retry with clean EF state - NEVER acquire locks out-of-order!
                if (order.TableId != orderInfo.TableId || order.TableBillId != orderInfo.TableBillId)
                {
                    if (transaction != null) await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    if (attempt < maxRetries - 1)
                    {
                        continue;
                    }
                    throw new ServiceConflictException("Table or bill ownership changed due to a concurrent transfer. Please refresh and retry.");
                }

                // Check OrderProductionOperations for idempotent replay or parameter conflict
                var existingOp = await _context.OrderProductionOperations
                    .FirstOrDefaultAsync(o => o.RestaurantId == restaurantId && o.OperationId == request.OperationId);
                if (existingOp != null)
                {
                    if (existingOp.PayloadHash != payloadHash)
                    {
                        throw new ServiceConflictException("Operation ID was already used with different parameters.");
                    }
                    return JsonSerializer.Deserialize<ItemCancellationResponseDto>(existingOp.OriginalResponseJson)!;
                }

                // Authoritative bill check inside the lock
                if (order.TableBillId.HasValue)
                {
                    var bill = await _context.TableBills.FirstOrDefaultAsync(b => b.Id == order.TableBillId.Value && b.RestaurantId == restaurantId);
                    if (bill != null)
                    {
                        if (bill.SettledAtUtc != null)
                        {
                            throw new ServiceConflictException("Cannot cancel items on a settled bill.");
                        }
                        if (bill.PaymentRevision != null || bill.SplitAtUtc != null)
                        {
                            throw new ServiceConflictException("Cannot cancel items on a bill with active payments or splits.");
                        }
                    }
                }

            var item = order.OrderItems.FirstOrDefault(i => i.Id == orderItemId);
            if (item == null)
            {
                throw new ArgumentException("Order item not found in this order.");
            }

            if (request.Quantity <= 0)
            {
                throw new ArgumentException("Quantity to cancel must be greater than 0.");
            }

            int availableToCancel = item.Quantity - item.CancelledQuantity;
            if (request.Quantity > availableToCancel)
            {
                throw new ServiceConflictException($"Cannot cancel {request.Quantity}. Only {availableToCancel} portion(s) available to cancel.");
            }

            // Food already released to kitchen requires manager authorization
            bool wasReleased = item.ReleaseState == "Fired" && order.Status != "Placed";
            string? approverName = null;
            Guid? approvedByEmployeeId = null;

            if (wasReleased)
            {
                var authResult = await _managerAuth.ValidateManagerApprovalAsync(
                    restaurantId,
                    request.RequestedByEmployeeId ?? Guid.Empty,
                    request.ManagerPin,
                    request.ApprovedByEmployeeId);

                if (!authResult.Success || authResult.Approver == null)
                {
                    throw new UnauthorizedAccessException(authResult.ErrorMessage ?? "Manager authorization failed.");
                }
                approverName = authResult.Approver.FullName;
                approvedByEmployeeId = authResult.Approver.Id;
            }

            // Outcome & Inventory handling
            decimal? wastedCost = null;

            // In both NotCooked and KitchenWaste cases, release the active reservations for the cancelled portions
            var reservations = await _context.StockReservations
                .Where(r => r.RestaurantId == restaurantId && r.OrderItemId == item.Id && r.Status == "Active")
                .ToListAsync();

            foreach (var res in reservations)
            {
                decimal fraction = (decimal)request.Quantity / availableToCancel;
                decimal qtyToRelease = Math.Min(res.ReservedQuantity, res.ReservedQuantity * fraction);
                res.ReservedQuantity -= qtyToRelease;
                if (res.ReservedQuantity <= 0)
                {
                    res.Status = "Released";
                    res.ReleasedAtUtc = DateTime.UtcNow;
                }
            }

            if (request.Outcome == "KitchenWaste")
            {
                // Kitchen waste: record ingredient loss via authoritative resolver
                var resolvedWaste = await _ingredientResolver.ResolveIngredientsAsync(
                    restaurantId,
                    new[] { (item.MenuItemId, request.Quantity, item.ModifiersJson, item.IsRefill) });

                decimal totalCost = 0m;
                foreach (var ingReq in resolvedWaste)
                {
                    totalCost += ingReq.ExtendedCost;
                    _context.StockMovements.Add(new StockMovement
                    {
                        RestaurantId = restaurantId,
                        IngredientId = ingReq.IngredientId,
                        ActorEmployeeId = approvedByEmployeeId ?? request.RequestedByEmployeeId,
                        TimestampUtc = DateTime.UtcNow,
                        QuantityChange = -ingReq.RawQuantity,
                        UnitSnapshot = ingReq.BaseUnit,
                        Reason = "KitchenWaste",
                        UnitCostSnapshot = ingReq.UnitCost,
                        TotalCost = ingReq.ExtendedCost,
                        ReferenceType = "ItemCancellationWaste",
                        ReferenceId = orderItemId.ToString(),
                        Notes = $"Cancelled item waste: {request.Quantity}x {item.ItemNameSnapshot}. Reason: {request.Reason}"
                    });
                }

                wastedCost = totalCost;
            }
            else if (request.Outcome != "NotCooked")
            {
                throw new ArgumentException($"Invalid cancellation outcome '{request.Outcome}'. Expected 'NotCooked' or 'KitchenWaste'.");
            }

            // Update item quantity
            item.CancelledQuantity += request.Quantity;
            if (item.CancelledQuantity >= item.Quantity)
            {
                item.IsCancelled = true;
            }

            // Recalculate order financials
            decimal removedSubtotal = item.UnitPrice * request.Quantity;
            order.Subtotal = Math.Max(0m, order.Subtotal - removedSubtotal);
            order.Tax = Math.Round(order.Subtotal * 0.05m, 0, MidpointRounding.AwayFromZero);
            order.TotalAmount = order.Subtotal + order.Tax;
            order.UpdatedAt = DateTime.UtcNow;

            // If all items cancelled, cancel entire order
            if (order.OrderItems.All(i => i.IsCancelled || i.Quantity <= i.CancelledQuantity))
            {
                order.Status = "Cancelled";
                order.ExceptionReason = $"All items cancelled: {request.Reason}";
            }

            // Station cancellation notice KOT
            string? noticeTicketNumber = null;
            if (wasReleased)
            {
                try
                {
                    var noticeTicket = await _kotRoutingService.RecordItemCancellationNoticeAsync(
                        restaurantId,
                        order.Id,
                        item,
                        request.Quantity,
                        request.Outcome,
                        request.Reason ?? "Cancelled",
                        approverName);
                    noticeTicketNumber = noticeTicket?.TicketNumber;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to record KOT cancellation notice for order item {OrderItemId}", item.Id);
                }
            }

            // Audit log
            var log = new ItemCancellationLog
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurantId,
                OrderId = order.Id,
                OrderItemId = item.Id,
                ItemNameSnapshot = item.ItemNameSnapshot,
                QuantityCancelled = request.Quantity,
                PreviousQuantity = item.Quantity - (item.CancelledQuantity - request.Quantity),
                NewQuantity = item.Quantity - item.CancelledQuantity,
                ProductionOutcome = request.Outcome,
                Reason = request.Reason ?? "Cancelled",
                RequestedByEmployeeId = request.RequestedByEmployeeId,
                ApprovedByEmployeeId = approvedByEmployeeId,
                ApproverNameSnapshot = approverName,
                OperationId = request.OperationId,
                WastedCost = wastedCost,
                TimestampUtc = DateTime.UtcNow
            };
            _context.ItemCancellationLogs.Add(log);

            var responseDto = new ItemCancellationResponseDto
            {
                CancellationLogId = log.Id,
                OrderId = order.Id,
                OrderItemId = item.Id,
                ItemName = item.ItemNameSnapshot,
                QuantityCancelled = request.Quantity,
                RemainingQuantity = item.Quantity - item.CancelledQuantity,
                ProductionOutcome = request.Outcome,
                NewOrderTotal = order.TotalAmount,
                WastedCost = wastedCost,
                CancellationKotNoticeNumber = noticeTicketNumber,
                Message = $"Successfully cancelled {request.Quantity} portion(s) with outcome '{request.Outcome}'."
            };

            _context.OrderProductionOperations.Add(new OrderProductionOperation
            {
                RestaurantId = restaurantId,
                OrderId = order.Id,
                OperationType = "Cancellation",
                OperationId = request.OperationId,
                PayloadHash = payloadHash,
                OriginalResponseJson = JsonSerializer.Serialize(responseDto),
                CreatedAtUtc = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            if (transaction != null) await transaction.CommitAsync();

            await NotifyAsync(order, false);

            return responseDto;
        }

        throw new ServiceConflictException("Table or bill ownership changed due to a concurrent transfer. Please refresh and retry.");
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
                OrderChannel = order.OrderChannel ?? "DineIn",
                DailyTokenNumber = order.DailyTokenNumber,
                BusinessDate = order.BusinessDate,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                KotPrintStatus = order.KotPrintStatus,
                PackagingCharge = order.PackagingCharge,
                DiningGroupId = order.DiningGroupId,
                GroupLabel = order.GroupLabel,
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
                    UnitPrice = oi.UnitPrice,
                    SpecialInstructions = oi.SpecialInstructions,
                    ReleaseState = oi.ReleaseState,
                    CourseName = oi.CourseName,
                    FiredAtUtc = oi.FiredAtUtc,
                    ModifiersJson = oi.ModifiersJson,
                    IsRefill = oi.IsRefill,
                    OriginalOrderItemId = oi.OriginalOrderItemId,
                    RefillCount = oi.RefillCount,
                    CancelledQuantity = oi.CancelledQuantity,
                    IsCancelled = oi.IsCancelled
                }).ToList()
            };
        }
    }
}
