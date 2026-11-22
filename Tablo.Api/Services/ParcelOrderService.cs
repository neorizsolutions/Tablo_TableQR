using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tablo.Api.Constants;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Hubs;
using Tablo.Api.Interfaces;
using Tablo.Api.Repositories;
using Tablo.Api.Services.Printing;

namespace Tablo.Api.Services;

public class ParcelOrderService : IParcelOrderService
{
    private static readonly TimeZoneInfo IstZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    private readonly ApplicationDbContext _db;
    private readonly ISubscriptionService _subscriptions;
    private readonly IKotRoutingService _kotRoutingService;
    private readonly BillingService _billingService;
    private readonly IOrderRepository _orderRepository;
    private readonly IHubContext<OrderHub> _hubContext;
    private readonly ILogger<ParcelOrderService> _logger;

    public ParcelOrderService(
        ApplicationDbContext db,
        ISubscriptionService subscriptions,
        IKotRoutingService kotRoutingService,
        BillingService billingService,
        IOrderRepository orderRepository,
        IHubContext<OrderHub> hubContext,
        ILogger<ParcelOrderService> logger)
    {
        _db = db;
        _subscriptions = subscriptions;
        _kotRoutingService = kotRoutingService;
        _billingService = billingService;
        _orderRepository = orderRepository;
        _hubContext = hubContext;
        _logger = logger;
    }

    private static string GetCurrentIstBusinessDate()
    {
        try
        {
            var ist = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstZone);
            return ist.ToString("yyyy-MM-dd");
        }
        catch
        {
            var ist = DateTime.UtcNow.AddMinutes(330);
            return ist.ToString("yyyy-MM-dd");
        }
    }

    public async Task<ParcelOrderResponseDto> CreateParcelOrderAsync(
        Guid restaurantId,
        Guid actorId,
        string actorEmail,
        CreateParcelOrderRequestDto request)
    {
        await BillingService.RequireActorAsync(_db, restaurantId, actorId);

        // Capability check for parcel counter
        bool hasParcelCap = await _subscriptions.HasCapabilityAsync(restaurantId, Capabilities.ParcelCounter);
        if (!hasParcelCap)
        {
            throw new InvalidOperationException("Parcel Counter capability is not enabled in your current subscription plan.");
        }

        if (request.OrderChannel == "CarParking")
        {
            bool hasCarCap = await _subscriptions.HasCapabilityAsync(restaurantId, Capabilities.CarSideOrdering);
            if (!hasCarCap)
            {
                throw new InvalidOperationException("Car-Side Ordering capability is not enabled in your current subscription plan.");
            }
        }

        DateTime? scheduledPickupUtc = null;
        string? scheduledPickupLocal = null;
        DateTime? scheduledReleaseUtc = null;
        int? leadMinutes = null;

        if (request.IsScheduled)
        {
            bool hasSchedCap = await _subscriptions.HasCapabilityAsync(restaurantId, Capabilities.ScheduledPickup);
            if (!hasSchedCap)
            {
                throw new InvalidOperationException("Scheduled Parcel Pickup capability is not enabled in your current subscription plan.");
            }

            if (string.IsNullOrWhiteSpace(request.ScheduledPickupTimeLocal))
            {
                throw new ArgumentException("Scheduled pickup time in Asia/Kolkata is required.");
            }

            DateTime localPickupTime;
            if (!DateTime.TryParse(request.ScheduledPickupTimeLocal, CultureInfo.InvariantCulture, DateTimeStyles.None, out localPickupTime) &&
                !DateTime.TryParse(request.ScheduledPickupTimeLocal, out localPickupTime))
            {
                throw new ArgumentException("Invalid scheduled pickup time format. Use YYYY-MM-DD HH:mm.");
            }

            DateTime pickupUtc;
            try
            {
                pickupUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localPickupTime, DateTimeKind.Unspecified), IstZone);
            }
            catch
            {
                pickupUtc = DateTime.SpecifyKind(localPickupTime.AddMinutes(-330), DateTimeKind.Utc);
            }

            if (pickupUtc <= DateTime.UtcNow.AddMinutes(1))
            {
                throw new ArgumentException("Scheduled pickup time must be in the future.");
            }

            int lead = request.PreparationLeadMinutes ?? 15;
            if (lead < 1) lead = 15;
            leadMinutes = lead;
            scheduledPickupUtc = pickupUtc;
            scheduledPickupLocal = localPickupTime.ToString("yyyy-MM-dd HH:mm");
            scheduledReleaseUtc = pickupUtc.AddMinutes(-lead);
        }

        if (request.OperationId == Guid.Empty)
        {
            throw new ArgumentException("A valid non-empty Operation ID is required.");
        }

        string operationIdStr = request.OperationId.ToString("N");

        // Validate marketplace delivery fields if delivery provider specified
        string? provider = !string.IsNullOrWhiteSpace(request.DeliveryProvider) ? request.DeliveryProvider.Trim() : null;
        string? externalRef = !string.IsNullOrWhiteSpace(request.ExternalOrderReference) ? request.ExternalOrderReference.Trim() : null;
        string? normalizedRef = externalRef?.ToUpperInvariant();

        if (request.OrderChannel == "Delivery" || provider != null)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new ArgumentException("Delivery provider (Swiggy, Zomato, or Other Delivery) is required for delivery orders.");
            }
            if (provider.Equals("Other", StringComparison.OrdinalIgnoreCase) || provider.Equals("Other Delivery", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Please provide a specific delivery partner name for Other Delivery.");
            }
            if (string.IsNullOrWhiteSpace(externalRef))
            {
                throw new ArgumentException("External order reference from marketplace partner app is required.");
            }
        }

        // Resolve and authorize Outlet
        Guid? resolvedOutletId = null;
        if (request.OutletId.HasValue)
        {
            var outlet = await _db.Outlets.FirstOrDefaultAsync(o => o.RestaurantId == restaurantId && o.Id == request.OutletId.Value);
            if (outlet == null)
            {
                throw new ArgumentException("The specified outlet was not found for this restaurant.");
            }
            resolvedOutletId = outlet.Id;
        }
        else
        {
            var defaultOutlet = await _db.Outlets.FirstOrDefaultAsync(o => o.RestaurantId == restaurantId && !o.IsCentralKitchen)
                ?? await _db.Outlets.FirstOrDefaultAsync(o => o.RestaurantId == restaurantId);
            resolvedOutletId = defaultOutlet?.Id;
        }

        // Canonical request fingerprint covering actor and meaningful order details
        var canonicalObj = new
        {
            actorId,
            Items = request.Items.OrderBy(i => i.MenuItemId).Select(i => new
            {
                i.MenuItemId,
                i.Quantity,
                Instructions = i.SpecialInstructions?.Trim() ?? ""
            }),
            CustomerName = request.CustomerName?.Trim() ?? "",
            CustomerPhone = request.CustomerPhone?.Trim() ?? "",
            WaivePackaging = request.WaivePackaging,
            WaiverReason = request.WaivePackaging ? (request.PackagingWaiverReason?.Trim() ?? "") : "",
            OrderChannel = request.OrderChannel,
            IsScheduled = request.IsScheduled,
            ScheduledPickupTimeLocal = request.ScheduledPickupTimeLocal,
            DeliveryProvider = provider,
            ExternalOrderReference = externalRef,
            MarketplaceCollected = request.MarketplaceCollected
        };
        string fingerprint = BillingService.Hash(JsonSerializer.Serialize(canonicalObj));

        // 1a. Idempotency Check: if order already submitted with this operation ID, return it immediately
        var existingOrder = await _db.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.TableBill)
            .FirstOrDefaultAsync(o => o.RestaurantId == restaurantId && o.SubmissionOperationId == operationIdStr);

        if (existingOrder != null)
        {
            if (!string.IsNullOrEmpty(existingOrder.SubmissionFingerprint) && existingOrder.SubmissionFingerprint != fingerprint)
            {
                throw new ServiceConflictException("This submission operation ID was previously used for different order details.");
            }
            _logger.LogInformation("Returning existing parcel order {OrderId} for operation {OperationId}", existingOrder.Id, operationIdStr);
            var existingBill = existingOrder.TableBill != null ? await _billingService.ViewAsync(existingOrder.TableBill) : new BillDto();
            return MapToParcelResponse(existingOrder, existingBill);
        }

        // 1b. Marketplace External Reference Uniqueness Check per (RestaurantId, OutletId, DeliveryProvider, NormalizedExternalReference)
        if (provider != null && normalizedRef != null)
        {
            var dupOrder = await _db.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.TableBill)
                .FirstOrDefaultAsync(o => o.RestaurantId == restaurantId
                    && o.OutletId == resolvedOutletId
                    && o.DeliveryProvider == provider
                    && o.NormalizedExternalReference == normalizedRef);

            if (dupOrder != null)
            {
                if (dupOrder.SubmissionOperationId == operationIdStr)
                {
                    _logger.LogInformation("Returning existing marketplace order {OrderId} for operation {OperationId}", dupOrder.Id, operationIdStr);
                    var existingBill = dupOrder.TableBill != null ? await _billingService.ViewAsync(dupOrder.TableBill) : new BillDto();
                    return MapToParcelResponse(dupOrder, existingBill);
                }

                throw new ServiceConflictException($"A {provider} order with external reference '{externalRef}' already exists (Token #{dupOrder.DailyTokenNumber:D3}, Order ORD-{dupOrder.OrderNumber:D4}).");
            }
        }

        string businessDate = scheduledPickupLocal != null && scheduledPickupLocal.Length >= 10
            ? scheduledPickupLocal.Substring(0, 10)
            : GetCurrentIstBusinessDate();

        await using var tx = _db.Database.IsRelational() && _db.Database.CurrentTransaction == null
            ? await _db.Database.BeginTransactionAsync()
            : null;

        // 2. Load restaurant settings
        var settings = await _db.RestaurantSettings.FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
        if (settings != null && !settings.IsAcceptingOrders)
        {
            throw new InvalidOperationException("Restaurant is currently not accepting orders.");
        }

        // 3. Resolve & validate menu items
        var itemIds = request.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await _db.MenuItems
            .Include(m => m.Category)
            .Where(m => m.RestaurantId == restaurantId && itemIds.Contains(m.Id) && !m.IsDeleted)
            .ToDictionaryAsync(m => m.Id);

        if (menuItems.Count != itemIds.Count)
        {
            throw new ArgumentException("One or more menu items were not found or are deleted.");
        }

        decimal foodSubtotal = 0m;
        var newlyAddedItems = new List<OrderItem>();

        foreach (var reqItem in request.Items)
        {
            if (reqItem.Quantity <= 0) throw new ArgumentException("Item quantity must be greater than zero.");
            var mi = menuItems[reqItem.MenuItemId];
            if (!mi.IsAvailable) throw new ArgumentException($"Menu item '{mi.Name}' is currently unavailable.");

            decimal itemTotal = mi.Price * reqItem.Quantity;
            foodSubtotal += itemTotal;

            newlyAddedItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                MenuItemId = mi.Id,
                ItemNameSnapshot = mi.Name,
                Quantity = reqItem.Quantity,
                UnitPrice = mi.Price,
                SpecialInstructions = reqItem.SpecialInstructions,
                PackingIntent = reqItem.PackingIntent == "DineIn" ? "DineIn" : "Takeaway"
            });
        }

        // 3b. Verify Recipe & Stock Availability (Locking ingredients in ascending order to prevent overselling)
        var reqMenuItemIds = newlyAddedItems.Select(x => x.MenuItemId).Distinct().ToList();
        var itemRecipes = await _db.RecipeItems
            .Where(r => r.RestaurantId == restaurantId && reqMenuItemIds.Contains(r.MenuItemId))
            .ToListAsync();

        if (itemRecipes.Count > 0)
        {
            var ingIds = itemRecipes.Select(r => r.IngredientId).Distinct().OrderBy(id => id).ToList();

            if (_db.Database.IsRelational())
            {
                // Lock ingredients in consistent ascending order to prevent concurrent last-portion overselling
                foreach (var ingId in ingIds)
                {
                    await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Ingredients\" WHERE \"Id\" = {ingId} AND \"RestaurantId\" = {restaurantId} FOR UPDATE");
                }
            }

            if (settings != null && settings.AutoManageAvailability)
            {
                var onHands = await _db.StockMovements.AsNoTracking()
                    .Where(m => m.RestaurantId == restaurantId && ingIds.Contains(m.IngredientId))
                    .GroupBy(m => m.IngredientId)
                    .Select(g => new { IngredientId = g.Key, Qty = g.Sum(m => m.QuantityChange) })
                    .ToDictionaryAsync(x => x.IngredientId, x => x.Qty);

                var reserved = await _db.StockReservations.AsNoTracking()
                    .Where(r => r.RestaurantId == restaurantId && ingIds.Contains(r.IngredientId) && r.Status == "Active")
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
        }

        // 4. Calculate packaging charges
        string chargeMode = settings?.ParcelPackagingChargeMode ?? "None";
        decimal appliedRate = 0m;
        decimal packagingCharge = 0m;
        bool isWaived = request.WaivePackaging;
        string? waiverReason = null;
        string? waivedBy = null;

        if (isWaived)
        {
            packagingCharge = 0m;
            waiverReason = !string.IsNullOrWhiteSpace(request.PackagingWaiverReason)
                ? request.PackagingWaiverReason.Trim()
                : "Staff waiver";
            waivedBy = actorEmail;
        }
        else
        {
            switch (chargeMode)
            {
                case "PerItem":
                    appliedRate = 0m; // item-specific
                    foreach (var oi in newlyAddedItems)
                    {
                        var mi = menuItems[oi.MenuItemId];
                        decimal itemFee = mi.PackagingFee ?? 0m;
                        packagingCharge += itemFee * oi.Quantity;
                    }
                    break;

                case "FlatPerOrder":
                    appliedRate = settings?.ParcelPackagingFlatRate ?? 0m;
                    packagingCharge = appliedRate;
                    break;

                case "Percentage":
                    appliedRate = settings?.ParcelPackagingPercentage ?? 0m;
                    packagingCharge = Math.Round(foodSubtotal * (appliedRate / 100m), 2, MidpointRounding.AwayFromZero);
                    break;

                case "None":
                default:
                    appliedRate = 0m;
                    packagingCharge = 0m;
                    break;
            }
        }

        packagingCharge = Math.Max(0m, Math.Round(packagingCharge, 2, MidpointRounding.AwayFromZero));

        // 5. Calculate taxes (Default 5% GST on food, packaging taxable if configured)
        decimal foodTaxRate = 0.05m;
        decimal foodTax = Math.Round(foodSubtotal * foodTaxRate, 2, MidpointRounding.AwayFromZero);

        decimal packagingTax = 0m;
        if (!isWaived && packagingCharge > 0 && settings != null && settings.ParcelPackagingTaxable)
        {
            decimal pTaxRate = (settings.ParcelPackagingTaxRate ?? 5.0m) / 100m;
            packagingTax = Math.Round(packagingCharge * pTaxRate, 2, MidpointRounding.AwayFromZero);
        }

        decimal totalTax = foodTax + packagingTax;
        decimal discount = 0m;
        decimal totalAmount = foodSubtotal + packagingCharge + totalTax - discount;

        // 6. Allocate Daily Token Number safely & atomically
        int dailyTokenNumber = await GetNextDailyTokenAsync(restaurantId, businessDate);

        // 7. Get next OrderNumber
        int nextOrderNumber = await _orderRepository.GetNextOrderNumberAsync(restaurantId);

        // 8. Create Order & TableBill
        string channel = provider != null ? "Delivery" : (request.OrderChannel == "CarParking" ? "CarParking" : "ParcelTakeaway");
        string status = request.IsScheduled ? "Scheduled" : "Accepted";

        var order = new Order
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            OutletId = resolvedOutletId,
            OrderNumber = nextOrderNumber,
            OrderChannel = channel,
            DeliveryProvider = provider,
            ExternalOrderReference = externalRef,
            NormalizedExternalReference = normalizedRef,
            MarketplaceCollected = request.MarketplaceCollected,
            MarketplaceEnteredTotal = request.MarketplaceEnteredTotal,
            MarketplaceEnteredTax = request.MarketplaceEnteredTax,
            MarketplaceEnteredDiscount = request.MarketplaceEnteredDiscount,
            MarketplaceEnteredPackaging = request.MarketplaceEnteredPackaging,
            MarketplaceFeeSnapshot = request.MarketplaceFeeSnapshot,
            DailyTokenNumber = dailyTokenNumber,
            BusinessDate = businessDate,
            CustomerName = request.CustomerName?.Trim(),
            CustomerPhone = request.CustomerPhone?.Trim(),
            SubmissionOperationId = operationIdStr,
            Status = status,
            Subtotal = foodSubtotal,
            PackagingCharge = packagingCharge,
            Tax = totalTax,
            Discount = discount,
            TotalAmount = totalAmount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TableId = null,
            DiningGroupId = null,
            GroupLabel = null,
            IsScheduled = request.IsScheduled,
            OrderSource = request.OrderSource ?? (provider != null ? provider : (channel == "CarParking" ? "CarSide" : "Counter")),
            ScheduledPickupTimeUtc = scheduledPickupUtc,
            ScheduledPickupTimeLocal = scheduledPickupLocal,
            ScheduledReleaseTimeUtc = scheduledReleaseUtc,
            PreparationLeadMinutes = leadMinutes,
            VehiclePlate = channel == "CarParking" ? request.VehiclePlate?.Trim().ToUpperInvariant() : null,
            VehicleModel = channel == "CarParking" ? request.VehicleModel?.Trim() : null,
            ParkingBay = channel == "CarParking" ? request.ParkingBay?.Trim() : null
        };

        foreach (var oi in newlyAddedItems)
        {
            oi.OrderId = order.Id;
            order.OrderItems.Add(oi);
        }

        var bill = new TableBill
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            TableId = null,
            TableNumberSnapshot = 0,
            OrderChannel = channel,
            DeliveryProvider = provider,
            ExternalOrderReference = externalRef,
            DailyTokenNumber = dailyTokenNumber,
            BusinessDate = businessDate,
            PackagingCharge = packagingCharge,
            PackagingChargeModeSnapshot = chargeMode,
            PackagingRateSnapshot = appliedRate,
            PackagingWaiverReason = waiverReason,
            PackagingWaivedBy = waivedBy,
            IsPackagingWaived = isWaived,
            VehiclePlate = order.VehiclePlate,
            ParkingBay = order.ParkingBay,
            Subtotal = foodSubtotal,
            Tax = totalTax,
            Discount = discount,
            TotalAmount = totalAmount,
            CreatedAtUtc = DateTime.UtcNow
        };

        order.TableBill = bill;
        order.TableBillId = bill.Id;
        order.SubmissionFingerprint = fingerprint;
        bill.Orders.Add(order);

        _db.TableBills.Add(bill);
        _db.Orders.Add(order);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            if (tx != null) await tx.RollbackAsync();
            var winner = await _db.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.TableBill)
                .FirstOrDefaultAsync(o => o.RestaurantId == restaurantId && o.SubmissionOperationId == operationIdStr);

            if (winner != null)
            {
                if (!string.IsNullOrEmpty(winner.SubmissionFingerprint) && winner.SubmissionFingerprint != fingerprint)
                {
                    throw new ServiceConflictException("This submission operation ID was previously used for different order details.");
                }
                var winBill = winner.TableBill != null ? await _billingService.ViewAsync(winner.TableBill) : new BillDto();
                return MapToParcelResponse(winner, winBill);
            }
            throw;
        }

        // 9. Recipe & Stock Reservation (only for recipe items; packaging never creates food consumption)
        if (itemRecipes.Count > 0)
        {
            foreach (var newItem in newlyAddedItems)
            {
                var rList = itemRecipes.Where(r => r.MenuItemId == newItem.MenuItemId).ToList();
                foreach (var r in rList)
                {
                    var rawQty = UnitConverter.CalculateRawQuantity(r.UsableQuantity, r.YieldPercent) * newItem.Quantity;
                    _db.StockReservations.Add(new StockReservation
                    {
                        RestaurantId = restaurantId,
                        OrderId = order.Id,
                        OrderItemId = newItem.Id,
                        IngredientId = r.IngredientId,
                        ReservedQuantity = rawQty,
                        Status = "Active",
                        ReservedAtUtc = DateTime.UtcNow
                    });
                }
            }
            await _db.SaveChangesAsync();
        }

        // 10. Generate Station KOTs (Only if not scheduled; scheduled orders fire when released)
        if (!request.IsScheduled)
        {
            try
            {
                await _kotRoutingService.ProcessAcceptedOrderItemsAsync(
                    order,
                    newlyAddedItems,
                    actorEmail: actorEmail,
                    trigger: channel == "CarParking" ? "CarSideOrder" : "ParcelStaffOrder");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate KOT jobs for order {OrderId}", order.Id);
                order.KotPrintStatus = "Failed";
                order.KotPrintError = ex.Message;
                await _db.SaveChangesAsync();
            }
        }

        if (tx != null) await tx.CommitAsync();

        // Notify floor and counter
        try
        {
            await _hubContext.Clients.Group($"restaurant_{restaurantId}").SendAsync("NewOrder", order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast SignalR notification for parcel order {OrderId}", order.Id);
        }

        var billDto = await _billingService.ViewAsync(bill);
        return MapToParcelResponse(order, billDto);
    }

    private async Task<int> GetNextDailyTokenAsync(Guid restaurantId, string businessDate)
    {
        if (_db.Database.IsRelational())
        {
            var result = await _db.Database.SqlQueryRaw<int>(
                @"INSERT INTO ""DailyTokenSequences"" (""Id"", ""RestaurantId"", ""BusinessDate"", ""LastTokenNumber"", ""UpdatedAtUtc"")
                  VALUES ({0}, {1}, {2}, 1, {3})
                  ON CONFLICT (""RestaurantId"", ""BusinessDate"")
                  DO UPDATE SET ""LastTokenNumber"" = ""DailyTokenSequences"".""LastTokenNumber"" + 1, ""UpdatedAtUtc"" = {3}
                  RETURNING ""LastTokenNumber""",
                Guid.NewGuid(), restaurantId, businessDate, DateTime.UtcNow
            ).ToListAsync();

            return result[0];
        }
        else
        {
            var seq = await _db.DailyTokenSequences.FirstOrDefaultAsync(s => s.RestaurantId == restaurantId && s.BusinessDate == businessDate);
            if (seq == null)
            {
                seq = new DailyTokenSequence
                {
                    Id = Guid.NewGuid(),
                    RestaurantId = restaurantId,
                    BusinessDate = businessDate,
                    LastTokenNumber = 1,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _db.DailyTokenSequences.Add(seq);
            }
            else
            {
                seq.LastTokenNumber++;
                seq.UpdatedAtUtc = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
            return seq.LastTokenNumber;
        }
    }

    public async Task<ParcelOrderResponseDto> CancelParcelOrderAsync(
        Guid restaurantId,
        Guid orderId,
        Guid actorId,
        string reason)
    {
        await BillingService.RequireActorAsync(_db, restaurantId, actorId);
        var order = await _db.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.TableBill)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId)
            ?? throw new KeyNotFoundException("Parcel order not found.");

        if (order.Status == "Served" || order.Status == "Cancelled" || order.Status == "Rejected")
        {
            throw new ServiceConflictException($"Order cannot be cancelled in status '{order.Status}'.");
        }

        order.Status = "Cancelled";
        order.ExceptionReason = reason;
        order.UpdatedAt = DateTime.UtcNow;

        var activeReservations = await _db.StockReservations
            .Where(r => r.RestaurantId == restaurantId && r.OrderId == order.Id && r.Status == "Active")
            .ToListAsync();

        foreach (var res in activeReservations)
        {
            res.Status = "Released";
            res.ReleasedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        var billDto = order.TableBill != null ? await _billingService.ViewAsync(order.TableBill) : new BillDto();
        return MapToParcelResponse(order, billDto);
    }

    public async Task<BillDto> SettleParcelBillAsync(
        Guid restaurantId,
        Guid orderId,
        Guid actorId,
        SettleParcelBillRequestDto request)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId);
        if (order == null) throw new KeyNotFoundException("Parcel order not found.");
        if (!order.TableBillId.HasValue) throw new InvalidOperationException("Order does not have an attached bill.");

        var settleRequest = new SettleBillRequestDto
        {
            OperationId = request.OperationId,
            ExpectedRevision = request.ExpectedRevision,
            ExpectedTotal = request.ExpectedTotal,
            PaymentMethod = request.PaymentMethod
        };

        var settledBill = await _billingService.SettleAsync(restaurantId, order.TableBillId.Value, actorId, settleRequest);

        // Update order status to Completed/Served upon full settlement (unless advance scheduled or car-side)
        if (!order.IsScheduled && order.OrderChannel != "CarParking")
        {
            order.Status = "Served";
            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return settledBill;
    }

    public async Task<List<ParcelOrderResponseDto>> SearchParcelOrdersAsync(
        Guid restaurantId,
        ParcelSearchFilterDto filter)
    {
        string targetDate = !string.IsNullOrWhiteSpace(filter.BusinessDate)
            ? filter.BusinessDate.Trim()
            : GetCurrentIstBusinessDate();

        var query = _db.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.TableBill)
            .Where(o => o.RestaurantId == restaurantId && (o.OrderChannel == "ParcelTakeaway" || o.OrderChannel == "CarParking" || o.OrderChannel == "Delivery" || o.DeliveryProvider != null))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Channel))
        {
            if (filter.Channel.Equals("Delivery", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(o => o.OrderChannel == "Delivery" || o.DeliveryProvider != null);
            }
            else
            {
                query = query.Where(o => o.OrderChannel == filter.Channel);
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.DeliveryProvider))
        {
            query = query.Where(o => o.DeliveryProvider == filter.DeliveryProvider);
        }

        if (!string.IsNullOrWhiteSpace(filter.ExternalOrderReference))
        {
            string ext = filter.ExternalOrderReference.Trim().ToUpperInvariant();
            query = query.Where(o => o.NormalizedExternalReference == ext || (o.ExternalOrderReference != null && o.ExternalOrderReference.ToUpper().Contains(ext)));
        }

        if (!string.IsNullOrWhiteSpace(filter.BusinessDate))
        {
            query = query.Where(o => o.BusinessDate == filter.BusinessDate.Trim());
        }
        else if (filter.Status != "Scheduled")
        {
            query = query.Where(o => o.BusinessDate == targetDate);
        }

        if (filter.DailyTokenNumber.HasValue)
        {
            query = query.Where(o => o.DailyTokenNumber == filter.DailyTokenNumber.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(o => o.Status == filter.Status);
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerSearch))
        {
            string term = filter.CustomerSearch.Trim().ToLower();
            query = query.Where(o => (o.CustomerName != null && o.CustomerName.ToLower().Contains(term))
                                  || (o.CustomerPhone != null && o.CustomerPhone.Contains(term))
                                  || (o.ExternalOrderReference != null && o.ExternalOrderReference.ToLower().Contains(term))
                                  || (o.DailyTokenNumber != null && o.DailyTokenNumber.ToString() == term)
                                  || ("#" + (o.DailyTokenNumber.HasValue ? o.DailyTokenNumber.Value.ToString("D3") : "")).Contains(term));
        }

        var orders = await query
            .OrderByDescending(o => o.DailyTokenNumber)
            .Take(filter.Limit)
            .ToListAsync();

        var results = new List<ParcelOrderResponseDto>();
        foreach (var order in orders)
        {
            var billDto = order.TableBill != null ? await _billingService.ViewAsync(order.TableBill) : new BillDto();
            results.Add(MapToParcelResponse(order, billDto));
        }

        return results;
    }

    public async Task<ParcelOrderResponseDto?> GetParcelOrderByIdAsync(Guid restaurantId, Guid orderId)
    {
        var order = await _db.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.TableBill)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId);

        if (order == null) return null;

        var billDto = order.TableBill != null ? await _billingService.ViewAsync(order.TableBill) : new BillDto();
        return MapToParcelResponse(order, billDto);
    }

    public async Task<PrintJobDto> PrintBillReceiptAsync(
        Guid restaurantId,
        Guid orderId,
        Guid actorId,
        string actorEmail,
        string? reprintReason = null)
    {
        var order = await _db.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.TableBill)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId);

        if (order == null) throw new KeyNotFoundException("Parcel order not found.");
        if (order.TableBill == null) throw new InvalidOperationException("Order does not have an attached bill.");

        var restaurant = await _db.Restaurants.FindAsync(restaurantId);
        string restName = restaurant?.Name ?? "Restaurant";

        // Find counter printer or default printer
        var printer = await _db.ThermalPrinters
            .Where(p => p.RestaurantId == restaurantId && p.IsActive)
            .OrderBy(p => p.Name.Contains("Counter") || p.Name.Contains("Bill") ? 0 : 1)
            .FirstOrDefaultAsync();

        if (printer == null)
        {
            throw new InvalidOperationException("No active thermal printer configured for bill/receipt printing.");
        }

        var bill = order.TableBill;
        bool isSettled = bill.SettledAtUtc != null;
        bool isReprint = !string.IsNullOrWhiteSpace(reprintReason);

        var itemsList = order.OrderItems.Select(oi => (
            Name: oi.ItemNameSnapshot,
            Quantity: oi.Quantity,
            UnitPrice: oi.UnitPrice,
            Total: oi.Quantity * oi.UnitPrice
        )).ToList();

        var (escPosBytes, readableText) = EscPosFormatter.FormatParcelReceipt(
            restaurantName: restName,
            billNumber: $"BILL-{order.OrderNumber:D4}",
            orderNumber: order.OrderNumber,
            dailyTokenNumber: order.DailyTokenNumber ?? 0,
            businessDate: order.BusinessDate,
            timestampUtc: DateTime.UtcNow,
            customerName: order.CustomerName,
            customerPhone: order.CustomerPhone,
            items: itemsList,
            foodSubtotal: order.Subtotal,
            packagingCharge: order.PackagingCharge,
            packagingMode: bill.PackagingChargeModeSnapshot,
            tax: order.Tax,
            discount: order.Discount,
            totalAmount: order.TotalAmount,
            paymentMethod: bill.PaymentMethod,
            isSettled: isSettled,
            isReprint: isReprint,
            reprintReason: reprintReason,
            paperWidthMm: printer.PaperWidthMm);

        var job = new PrintJob
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            KotTicketId = null,
            PrinterId = printer.Id,
            StationId = null,
            JobType = isSettled ? "Receipt" : "Bill",
            Status = "Pending",
            PayloadEscPosBase64 = Convert.ToBase64String(escPosBytes),
            ReadableText = readableText,
            PaperWidthMm = printer.PaperWidthMm,
            Attempts = 0,
            MaxAttempts = 3,
            CreatedBy = actorEmail,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.PrintJobs.Add(job);
        await _db.SaveChangesAsync();

        return new PrintJobDto
        {
            Id = job.Id,
            KotTicketId = null,
            PrinterId = printer.Id,
            PrinterName = printer.Name,
            StationId = null,
            JobType = job.JobType,
            Status = job.Status,
            PaperWidthMm = job.PaperWidthMm,
            Attempts = job.Attempts,
            CreatedBy = job.CreatedBy,
            CreatedAtUtc = job.CreatedAtUtc,
            ReadableText = job.ReadableText
        };
    }

    public async Task<List<PrintJobDto>> RetryParcelKotAsync(
        Guid restaurantId,
        Guid orderId,
        Guid actorId,
        string actorEmail)
    {
        var order = await _db.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId);

        if (order == null) throw new KeyNotFoundException("Parcel order not found.");

        var tickets = await _kotRoutingService.ProcessAcceptedOrderItemsAsync(
            order,
            order.OrderItems.ToList(),
            actorEmail: actorEmail,
            trigger: "ManualKotRetry");

        order.KotPrintStatus = tickets.Count > 0 ? "Queued" : "Skipped";
        order.KotPrintError = null;
        await _db.SaveChangesAsync();

        var ticketIds = tickets.Select(t => t.Id).ToList();
        var jobs = await _db.PrintJobs
            .Include(j => j.Printer)
            .Where(j => j.KotTicketId.HasValue && ticketIds.Contains(j.KotTicketId.Value))
            .Select(j => new PrintJobDto
            {
                Id = j.Id,
                KotTicketId = j.KotTicketId,
                TicketNumber = j.KotTicket != null ? j.KotTicket.TicketNumber : null,
                PrinterId = j.PrinterId,
                PrinterName = j.Printer.Name,
                StationId = j.StationId,
                JobType = j.JobType,
                Status = j.Status,
                PaperWidthMm = j.PaperWidthMm,
                Attempts = j.Attempts,
                CreatedBy = j.CreatedBy,
                CreatedAtUtc = j.CreatedAtUtc,
                ReadableText = j.ReadableText
            })
            .ToListAsync();

        return jobs;
    }

    public async Task<ParcelSettingsDto> GetParcelSettingsAsync(Guid restaurantId)
    {
        var settings = await _db.RestaurantSettings.FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
        return new ParcelSettingsDto
        {
            ChargeMode = settings?.ParcelPackagingChargeMode ?? "None",
            FlatRate = settings?.ParcelPackagingFlatRate ?? 0m,
            Percentage = settings?.ParcelPackagingPercentage ?? 0m,
            IsTaxable = settings?.ParcelPackagingTaxable ?? false,
            TaxRate = settings?.ParcelPackagingTaxRate ?? 0m
        };
    }

    public async Task<ParcelSettingsDto> UpdateParcelSettingsAsync(Guid restaurantId, UpdateParcelSettingsRequestDto dto)
    {
        var settings = await _db.RestaurantSettings.FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
        if (settings == null)
        {
            settings = new RestaurantSettings
            {
                RestaurantId = restaurantId,
                IsAcceptingOrders = true
            };
            _db.RestaurantSettings.Add(settings);
        }

        settings.ParcelPackagingChargeMode = dto.ChargeMode;
        settings.ParcelPackagingFlatRate = Math.Max(0m, Math.Round(dto.FlatRate, 2));
        settings.ParcelPackagingPercentage = Math.Max(0m, Math.Round(dto.Percentage, 2));
        settings.ParcelPackagingTaxable = dto.IsTaxable;
        settings.ParcelPackagingTaxRate = dto.TaxRate.HasValue ? Math.Max(0m, Math.Round(dto.TaxRate.Value, 2)) : null;

        await _db.SaveChangesAsync();

        return new ParcelSettingsDto
        {
            ChargeMode = settings.ParcelPackagingChargeMode,
            FlatRate = settings.ParcelPackagingFlatRate,
            Percentage = settings.ParcelPackagingPercentage,
            IsTaxable = settings.ParcelPackagingTaxable,
            TaxRate = settings.ParcelPackagingTaxRate
        };
    }

    public async Task<ParcelOrderResponseDto> ReleaseScheduledOrderAsync(
        Guid restaurantId,
        Guid orderId,
        Guid actorId,
        string actorEmail)
    {
        if (actorId != Guid.Empty)
        {
            await BillingService.RequireActorAsync(_db, restaurantId, actorId);
        }

        await using var tx = _db.Database.IsRelational() && _db.Database.CurrentTransaction == null
            ? await _db.Database.BeginTransactionAsync()
            : null;

        var order = await _db.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.TableBill)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId);

        if (order == null) throw new KeyNotFoundException("Order not found.");

        if (order.Status != "Scheduled")
        {
            throw new ServiceConflictException($"Order cannot be released because it is in status '{order.Status}'.");
        }

        order.Status = "Accepted";
        order.UpdatedAt = DateTime.UtcNow;

        // Generate KOTs now upon release
        try
        {
            await _kotRoutingService.ProcessAcceptedOrderItemsAsync(
                order,
                order.OrderItems.ToList(),
                actorEmail: actorEmail,
                trigger: "ScheduledRelease");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate KOT jobs on releasing scheduled order {OrderId}", order.Id);
            order.KotPrintStatus = "Failed";
            order.KotPrintError = ex.Message;
        }

        await _db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();

        // Broadcast notification
        try
        {
            await _hubContext.Clients.Group($"restaurant_{restaurantId}").SendAsync("NewOrder", order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast SignalR notification for released scheduled order {OrderId}", order.Id);
        }

        var billDto = order.TableBill != null ? await _billingService.ViewAsync(order.TableBill) : new BillDto();
        return MapToParcelResponse(order, billDto);
    }

    public async Task<ParcelOrderResponseDto> RescheduleParcelOrderAsync(
        Guid restaurantId,
        Guid orderId,
        Guid actorId,
        RescheduleParcelOrderRequestDto request)
    {
        await BillingService.RequireActorAsync(_db, restaurantId, actorId);

        if (string.IsNullOrWhiteSpace(request.NewPickupTimeLocal))
        {
            throw new ArgumentException("New scheduled pickup time is required.");
        }

        DateTime localPickupTime;
        if (!DateTime.TryParse(request.NewPickupTimeLocal, CultureInfo.InvariantCulture, DateTimeStyles.None, out localPickupTime) &&
            !DateTime.TryParse(request.NewPickupTimeLocal, out localPickupTime))
        {
            throw new ArgumentException("Invalid scheduled pickup time format. Use YYYY-MM-DD HH:mm.");
        }

        DateTime pickupUtc;
        try
        {
            pickupUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localPickupTime, DateTimeKind.Unspecified), IstZone);
        }
        catch
        {
            pickupUtc = DateTime.SpecifyKind(localPickupTime.AddMinutes(-330), DateTimeKind.Utc);
        }

        if (pickupUtc <= DateTime.UtcNow.AddMinutes(1))
        {
            throw new ArgumentException("Scheduled pickup time must be in the future.");
        }

        await using var tx = _db.Database.IsRelational() && _db.Database.CurrentTransaction == null
            ? await _db.Database.BeginTransactionAsync()
            : null;

        var order = await _db.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.TableBill)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId);

        if (order == null) throw new KeyNotFoundException("Order not found.");

        if (order.Status != "Scheduled")
        {
            throw new ServiceConflictException($"Order cannot be rescheduled because it is in status '{order.Status}'.");
        }

        int leadMinutes = request.PreparationLeadMinutes ?? order.PreparationLeadMinutes ?? 15;
        if (leadMinutes < 1) leadMinutes = 15;
        var releaseUtc = pickupUtc.AddMinutes(-leadMinutes);
        string newBusinessDate = localPickupTime.ToString("yyyy-MM-dd");

        if (newBusinessDate != order.BusinessDate)
        {
            int newToken = await GetNextDailyTokenAsync(restaurantId, newBusinessDate);
            order.DailyTokenNumber = newToken;
            order.BusinessDate = newBusinessDate;
            if (order.TableBill != null)
            {
                order.TableBill.DailyTokenNumber = newToken;
                order.TableBill.BusinessDate = newBusinessDate;
            }
        }

        order.ScheduledPickupTimeUtc = pickupUtc;
        order.ScheduledPickupTimeLocal = localPickupTime.ToString("yyyy-MM-dd HH:mm");
        order.PreparationLeadMinutes = leadMinutes;
        order.ScheduledReleaseTimeUtc = releaseUtc;
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();

        var billDto = order.TableBill != null ? await _billingService.ViewAsync(order.TableBill) : new BillDto();
        return MapToParcelResponse(order, billDto);
    }

    public async Task<ParcelOrderResponseDto> DeliverCarOrderAsync(
        Guid restaurantId,
        Guid orderId,
        Guid actorId,
        DeliverCarOrderRequestDto request)
    {
        await BillingService.RequireActorAsync(_db, restaurantId, actorId);

        var order = await _db.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.TableBill)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId);

        if (order == null) throw new KeyNotFoundException("Order not found.");
        if (order.OrderChannel != "CarParking")
        {
            throw new ArgumentException("This order is not a car-side order.");
        }

        if (order.Status == "Cancelled" || order.Status == "Rejected")
        {
            throw new ServiceConflictException($"Cannot deliver order in status '{order.Status}'.");
        }

        order.Status = "Served";
        order.DeliveredAtUtc = DateTime.UtcNow;
        order.DeliveredByEmployeeId = actorId;
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var billDto = order.TableBill != null ? await _billingService.ViewAsync(order.TableBill) : new BillDto();
        return MapToParcelResponse(order, billDto);
    }

    public async Task<BillDto> AddLeftoverPackingChargeAsync(
        Guid restaurantId,
        Guid billId,
        Guid actorId,
        AddLeftoverPackingRequestDto request)
    {
        await BillingService.RequireActorAsync(_db, restaurantId, actorId);

        bool hasHybridCap = await _subscriptions.HasCapabilityAsync(restaurantId, Capabilities.HybridDineInParcel);
        if (!hasHybridCap)
        {
            throw new InvalidOperationException("Hybrid Dine-In / Parcel capability is not enabled in your current subscription plan.");
        }

        if (request.ContainerCount <= 0)
        {
            throw new ArgumentException("Container count must be at least 1.");
        }

        var settings = await _db.RestaurantSettings.FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
        decimal rate = request.ContainerCharge > 0
            ? request.ContainerCharge
            : (settings?.LeftoverContainerCharge ?? 10m);

        decimal addedCharge = Math.Round(rate * request.ContainerCount, 2, MidpointRounding.AwayFromZero);

        await using var tx = _db.Database.IsRelational() && _db.Database.CurrentTransaction == null
            ? await _db.Database.BeginTransactionAsync()
            : null;

        var bill = await _db.TableBills
            .Include(b => b.Orders).ThenInclude(o => o.OrderItems)
            .FirstOrDefaultAsync(b => b.Id == billId && b.RestaurantId == restaurantId);

        if (bill == null) throw new KeyNotFoundException("Bill not found.");

        if (bill.SettledAtUtc.HasValue)
        {
            throw new ServiceConflictException("Cannot add leftover packaging charge to an already settled bill.");
        }
        if (bill.PaymentRevision != null)
        {
            throw new ServiceConflictException("Cannot add leftover packaging charge after payments have started.");
        }

        bill.LeftoverPackingCharge = (bill.LeftoverPackingCharge) + addedCharge;
        bill.TotalAmount = bill.Subtotal + bill.PackagingCharge + bill.LeftoverPackingCharge + bill.Tax - bill.Discount;

        await _db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();

        return await _billingService.ViewAsync(bill);
    }

    public async Task<UpiQrResponseDto> GenerateBillUpiQrAsync(
        Guid restaurantId,
        Guid billId,
        decimal? requestedAmount)
    {
        bool hasUpiCap = await _subscriptions.HasCapabilityAsync(restaurantId, Capabilities.DynamicUpiQr);
        if (!hasUpiCap)
        {
            throw new InvalidOperationException("Dynamic UPI QR Payments capability is not enabled in your current subscription plan.");
        }

        var settings = await _db.RestaurantSettings.FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
        if (settings == null || !settings.UpiQrEnabled || string.IsNullOrWhiteSpace(settings.UpiVpa))
        {
            throw new InvalidOperationException("UPI QR payments are not configured or enabled for this restaurant.");
        }

        var bill = await _db.TableBills
            .Include(b => b.Orders)
            .FirstOrDefaultAsync(b => b.Id == billId && b.RestaurantId == restaurantId);

        if (bill == null) throw new KeyNotFoundException("Bill not found.");

        if (bill.SettledAtUtc.HasValue)
        {
            throw new ServiceConflictException("Bill is already settled.");
        }

        if (bill.SplitAtUtc.HasValue || bill.Orders.Any(o => o.OrderChannel == "Delivery" && o.MarketplaceCollected))
        {
            throw new ServiceConflictException("This bill is not eligible for direct UPI collection.");
        }

        var payments = await _db.BillPayments.AsNoTracking()
            .Where(p => p.RestaurantId == restaurantId && p.TableBillId == billId)
            .SumAsync(p => p.Amount);

        await _db.Entry(bill).Collection(b => b.Orders).Query().Include(o => o.OrderItems).LoadAsync();
        decimal remainingBalance = Math.Max(0m, BillingService.Project(bill).TotalAmount - payments);
        if (remainingBalance <= 0)
        {
            throw new ServiceConflictException("Bill is already fully paid.");
        }

        decimal amount = requestedAmount.HasValue && requestedAmount.Value > 0
            ? Math.Min(requestedAmount.Value, remainingBalance)
            : remainingBalance;

        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        string billRef = bill.Id.ToString("N");

        string note = $"Bill {billRef}";

        var uriBuilder = new StringBuilder("upi://pay?");
        uriBuilder.Append($"pa={Uri.EscapeDataString(settings.UpiVpa.Trim())}");
        uriBuilder.Append($"&pn={Uri.EscapeDataString(settings.UpiPayeeName?.Trim() ?? "Restaurant")}");
        if (!string.IsNullOrWhiteSpace(settings.UpiMerchantCode))
        {
            uriBuilder.Append($"&mc={Uri.EscapeDataString(settings.UpiMerchantCode.Trim())}");
        }
        uriBuilder.Append($"&tr={Uri.EscapeDataString(billRef)}");
        uriBuilder.Append($"&tn={Uri.EscapeDataString(note)}");
        uriBuilder.Append($"&am={amount.ToString("F2", CultureInfo.InvariantCulture)}");
        uriBuilder.Append("&cu=INR");

        return new UpiQrResponseDto
        {
            BillId = bill.Id,
            Amount = amount,
            RemainingBalance = remainingBalance,
            UpiUri = uriBuilder.ToString(),
            PayeeVpa = settings.UpiVpa.Trim(),
            PayeeName = settings.UpiPayeeName?.Trim() ?? "Restaurant",
            Reference = billRef,
            Note = note,
            IsSettled = false
        };
    }

    private static ParcelOrderResponseDto MapToParcelResponse(Order order, BillDto bill)
    {
        return new ParcelOrderResponseDto
        {
            OrderId = order.Id,
            BillId = bill.Id,
            OrderNumber = order.OrderNumber,
            DailyTokenNumber = order.DailyTokenNumber ?? 0,
            BusinessDate = order.BusinessDate ?? string.Empty,
            OrderChannel = order.OrderChannel ?? "ParcelTakeaway",
            Status = order.Status,
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            Subtotal = order.Subtotal,
            PackagingCharge = order.PackagingCharge,
            PackagingChargeMode = bill.PackagingChargeModeSnapshot,
            PackagingRate = bill.PackagingRateSnapshot,
            IsPackagingWaived = bill.IsPackagingWaived,
            PackagingWaiverReason = bill.PackagingWaiverReason,
            Tax = order.Tax,
            Discount = order.Discount,
            TotalAmount = order.TotalAmount,
            CreatedAtUtc = order.CreatedAt,
            CreatedAtIstFormatted = EscPosFormatter.FormatIstDateTime(order.CreatedAt),
            KotPrintStatus = order.KotPrintStatus,
            KotPrintError = order.KotPrintError,
            OrderSource = order.OrderSource,
            IsScheduled = order.IsScheduled,
            ScheduledPickupTimeUtc = order.ScheduledPickupTimeUtc,
            ScheduledPickupTimeLocal = order.ScheduledPickupTimeLocal,
            ScheduledReleaseTimeUtc = order.ScheduledReleaseTimeUtc,
            PreparationLeadMinutes = order.PreparationLeadMinutes,
            VehiclePlate = order.VehiclePlate,
            VehicleModel = order.VehicleModel,
            ParkingBay = order.ParkingBay,
            DeliveredAtUtc = order.DeliveredAtUtc,
            DeliveredByEmployeeId = order.DeliveredByEmployeeId,
            OutletId = order.OutletId,
            DeliveryProvider = order.DeliveryProvider,
            ExternalOrderReference = order.ExternalOrderReference,
            MarketplaceCollected = order.MarketplaceCollected,
            MarketplaceEnteredTotal = order.MarketplaceEnteredTotal,
            MarketplaceEnteredTax = order.MarketplaceEnteredTax,
            MarketplaceEnteredDiscount = order.MarketplaceEnteredDiscount,
            MarketplaceEnteredPackaging = order.MarketplaceEnteredPackaging,
            Items = order.OrderItems.Select(oi => new OrderItemDto
            {
                Id = oi.Id,
                MenuItemId = oi.MenuItemId,
                Name = oi.ItemNameSnapshot,
                ItemNameSnapshot = oi.ItemNameSnapshot,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                PackingIntent = oi.PackingIntent
            }).ToList(),
            Bill = bill
        };
    }

    public async Task<MarketplacePayoutDto> RecordMarketplacePayoutAsync(
        Guid restaurantId,
        Guid actorId,
        RecordMarketplacePayoutDto request)
    {
        await BillingService.RequireActorAsync(_db, restaurantId, actorId);

        if (request.OperationId == Guid.Empty) throw new ArgumentException("OperationId is required.");
        if (string.IsNullOrWhiteSpace(request.PayoutReference)) throw new ArgumentException("Payout reference is required.");
        if (string.IsNullOrWhiteSpace(request.DeliveryProvider)) throw new ArgumentException("Delivery provider is required.");

        string provider = request.DeliveryProvider.Trim();
        string payoutRef = request.PayoutReference.Trim();

        // Idempotency: Check if payout with same restaurant, provider & payoutRef exists
        var existing = await _db.MarketplacePayouts
            .FirstOrDefaultAsync(p => p.RestaurantId == restaurantId
                && p.DeliveryProvider == provider
                && p.PayoutReference == payoutRef);

        if (existing != null)
        {
            _logger.LogInformation("Returning existing marketplace payout {PayoutId} for ref {PayoutRef}", existing.Id, payoutRef);
            return MapToPayoutDto(existing);
        }

        // Resolve outlet if linked orders exist
        Guid? outletId = null;
        string? linkedJson = null;
        if (request.LinkedOrderIds != null && request.LinkedOrderIds.Count > 0)
        {
            var linkedOrders = await _db.Orders
                .Where(o => o.RestaurantId == restaurantId && request.LinkedOrderIds.Contains(o.Id))
                .ToListAsync();
            outletId = linkedOrders.FirstOrDefault(o => o.OutletId.HasValue)?.OutletId;
            linkedJson = JsonSerializer.Serialize(request.LinkedOrderIds.Select(id => id.ToString()).ToList());
        }

        var payout = new MarketplacePayout
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            OutletId = outletId,
            DeliveryProvider = provider,
            PayoutReference = payoutRef,
            PayoutDateUtc = request.PayoutDateUtc ?? DateTime.UtcNow,
            GrossAmount = request.GrossAmount,
            CommissionDeducted = request.CommissionDeducted,
            TcsGstDeducted = request.TcsGstDeducted,
            OtherAdjustments = request.OtherAdjustments,
            NetAmount = request.NetAmount,
            LinkedOrderIdsJson = linkedJson,
            Notes = request.Notes?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            RecordedByEmployeeId = actorId
        };

        _db.MarketplacePayouts.Add(payout);
        await _db.SaveChangesAsync();

        return MapToPayoutDto(payout);
    }

    public async Task<List<MarketplacePayoutDto>> GetMarketplacePayoutsAsync(Guid restaurantId, string? deliveryProvider = null)
    {
        var query = _db.MarketplacePayouts.AsNoTracking()
            .Where(p => p.RestaurantId == restaurantId);

        if (!string.IsNullOrWhiteSpace(deliveryProvider))
        {
            query = query.Where(p => p.DeliveryProvider == deliveryProvider.Trim());
        }

        var list = await query
            .OrderByDescending(p => p.PayoutDateUtc)
            .Take(100)
            .ToListAsync();

        return list.Select(MapToPayoutDto).ToList();
    }

    private static MarketplacePayoutDto MapToPayoutDto(MarketplacePayout p)
    {
        List<string> linked = new();
        if (!string.IsNullOrWhiteSpace(p.LinkedOrderIdsJson))
        {
            try { linked = JsonSerializer.Deserialize<List<string>>(p.LinkedOrderIdsJson) ?? new(); } catch { }
        }
        return new MarketplacePayoutDto
        {
            Id = p.Id,
            RestaurantId = p.RestaurantId,
            DeliveryProvider = p.DeliveryProvider,
            PayoutReference = p.PayoutReference,
            PayoutDateUtc = p.PayoutDateUtc,
            GrossAmount = p.GrossAmount,
            CommissionDeducted = p.CommissionDeducted,
            TcsGstDeducted = p.TcsGstDeducted,
            OtherAdjustments = p.OtherAdjustments,
            NetAmount = p.NetAmount,
            LinkedOrderIds = linked,
            Notes = p.Notes,
            CreatedAtUtc = p.CreatedAtUtc,
            RecordedByEmployeeId = p.RecordedByEmployeeId
        };
    }
}
