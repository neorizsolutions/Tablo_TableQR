using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Extensions;

namespace Tablo.Api.Controllers;

[ApiController]
[Route("api/storefront")]
public class StorefrontController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public StorefrontController(ApplicationDbContext db)
    {
        _db = db;
    }

    private static string ComputeFingerprint(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    public static (bool IsOpen, string Message) EvaluateOutletOpenStatus(Outlet outlet, DateTime utcTime)
    {
        if (outlet.IsTemporarilyClosed)
        {
            return (false, $"Outlet is temporarily closed: {outlet.ClosureReason ?? "Please check back later."}");
        }

        if (!outlet.IsOpen)
        {
            return (false, "Outlet is currently not accepting orders.");
        }

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }
        catch
        {
            tz = TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromMinutes(330), "India Standard Time", "IST");
        }

        var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
        var timeOfDay = localTime.TimeOfDay;

        if (!TimeSpan.TryParse(outlet.OpeningTime, out var openTime))
        {
            openTime = new TimeSpan(11, 0, 0);
        }
        if (!TimeSpan.TryParse(outlet.ClosingTime, out var closeTime))
        {
            closeTime = new TimeSpan(23, 30, 0);
        }

        bool isOpenNow;
        if (closeTime > openTime)
        {
            // Standard Daytime operating hours (e.g. 11:00 to 23:30)
            isOpenNow = timeOfDay >= openTime && timeOfDay <= closeTime;
        }
        else
        {
            // Overnight operating hours (e.g. 19:00 to 02:00 next day)
            isOpenNow = timeOfDay >= openTime || timeOfDay <= closeTime;
        }

        if (!isOpenNow)
        {
            return (false, $"Outlet is closed. Operating hours are {outlet.OpeningTime} - {outlet.ClosingTime} ({outlet.Timezone}).");
        }

        return (true, "Open");
    }

    [HttpGet("outlets")]
    [HttpGet("restaurants/{restaurantId}/outlets")]
    [AllowAnonymous]
    public async Task<ActionResult<List<StorefrontOutletSummaryDto>>> GetOutlets(Guid? restaurantId = null)
    {
        var query = _db.Outlets
            .AsNoTracking()
            .Where(o => o.IsActive && !o.IsCentralKitchen && o.IsPublished);

        if (restaurantId.HasValue && restaurantId.Value != Guid.Empty)
        {
            query = query.Where(o => o.RestaurantId == restaurantId.Value);
        }

        var outlets = await query
            .OrderBy(o => o.Name)
            .ToListAsync();

        var nowUtc = DateTime.UtcNow;

        var results = outlets.Select(o =>
        {
            var (isOpen, _) = EvaluateOutletOpenStatus(o, nowUtc);
            return new StorefrontOutletSummaryDto(
                o.Id.ToString(),
                o.RestaurantId.ToString(),
                o.Name,
                o.Address ?? "Connaught Place, New Delhi",
                o.Phone ?? "+91 98765 43210",
                isOpen,
                $"{o.OpeningTime} - {o.ClosingTime}",
                o.LeadTimeMinutes,
                o.MinOrderAmount,
                o.DeliveryBaseFee,
                o.FreeDeliveryAboveAmount,
                o.IsPublished,
                o.IsTemporarilyClosed,
                o.ClosureReason
            );
        }).ToList();

        return Ok(results);
    }

    [HttpGet("outlets/{outletId}")]
    [AllowAnonymous]
    public async Task<ActionResult<StorefrontOutletSummaryDto>> GetOutlet(string outletId)
    {
        if (!Guid.TryParse(outletId, out var gOutletId) || gOutletId == Guid.Empty)
        {
            return NotFound(new { message = "Invalid outlet identifier." });
        }

        var outlet = await _db.Outlets.AsNoTracking()
            .FirstOrDefaultAsync(o => (o.Id == gOutletId || o.RestaurantId == gOutletId) && o.IsActive && !o.IsCentralKitchen && o.IsPublished);

        if (outlet == null)
        {
            return NotFound(new { message = "Outlet not found or is currently unpublished." });
        }

        var (isOpen, _) = EvaluateOutletOpenStatus(outlet, DateTime.UtcNow);

        return Ok(new StorefrontOutletSummaryDto(
            outlet.Id.ToString(),
            outlet.RestaurantId.ToString(),
            outlet.Name,
            outlet.Address ?? "Connaught Place, New Delhi",
            outlet.Phone ?? "+91 98765 43210",
            isOpen,
            $"{outlet.OpeningTime} - {outlet.ClosingTime}",
            outlet.LeadTimeMinutes,
            outlet.MinOrderAmount,
            outlet.DeliveryBaseFee,
            outlet.FreeDeliveryAboveAmount,
            outlet.IsPublished,
            outlet.IsTemporarilyClosed,
            outlet.ClosureReason
        ));
    }

    [HttpGet("outlets/{outletId}/menu")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> GetOutletMenu(string outletId)
    {
        if (!Guid.TryParse(outletId, out var gOutletId) || gOutletId == Guid.Empty)
        {
            return NotFound(new { message = "Invalid outlet identifier." });
        }

        var outlet = await _db.Outlets.AsNoTracking()
            .FirstOrDefaultAsync(o => (o.Id == gOutletId || o.RestaurantId == gOutletId) && o.IsActive && !o.IsCentralKitchen && o.IsPublished);

        if (outlet == null)
        {
            return NotFound(new { message = "Outlet not found or is currently unpublished." });
        }

        var restId = outlet.RestaurantId;

        var items = await _db.MenuItems
            .AsNoTracking()
            .Include(m => m.Category)
            .Include(m => m.ModifierGroups)
                .ThenInclude(mg => mg.ModifierGroup)
                .ThenInclude(g => g.Options)
            .Where(m => m.RestaurantId == restId && m.IsAvailable && !m.IsDeleted)
            .OrderBy(m => m.Category.DisplayOrder)
            .ThenBy(m => m.Name)
            .Select(m => new
            {
                Id = m.Id.ToString(),
                m.Name,
                m.Description,
                m.Price,
                m.IsAvailable,
                Category = m.Category.Name,
                CategoryId = m.CategoryId.ToString(),
                Modifiers = m.ModifierGroups.SelectMany(mg => mg.ModifierGroup.Options).Select(opt => new
                {
                    Id = opt.Id.ToString(),
                    opt.Name,
                    opt.PriceDelta,
                    opt.IsAvailable
                })
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("checkout")]
    [AllowAnonymous]
    public async Task<ActionResult<StorefrontCheckoutResponseDto>> Checkout([FromBody] StorefrontCheckoutRequestDto request)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "At least one item is required to checkout." });
        }

        if (!Guid.TryParse(request.OutletId, out var gOutletId) || gOutletId == Guid.Empty)
        {
            return BadRequest(new { message = "Valid outlet ID is required." });
        }

        var outlet = await _db.Outlets.FirstOrDefaultAsync(o => (o.Id == gOutletId || o.RestaurantId == gOutletId) && o.IsActive && !o.IsCentralKitchen && o.IsPublished);
        if (outlet == null)
        {
            return BadRequest(new { message = "Outlet not found, inactive, or not published for online ordering." });
        }
        gOutletId = outlet.Id;

        var nowUtc = DateTime.UtcNow;
        var isScheduled = request.FulfillmentTiming == "Scheduled" && request.ScheduledForUtc.HasValue;
        DateTime? scheduledReleaseUtc = null;
        DateTime? scheduledPickupUtc = null;

        // Operating hours & fulfillment window validation
        if (isScheduled)
        {
            scheduledPickupUtc = request.ScheduledForUtc!.Value;
            if (scheduledPickupUtc.Value <= nowUtc.AddMinutes(outlet.LeadTimeMinutes))
            {
                return BadRequest(new { message = $"Scheduled pickup time must be at least {outlet.LeadTimeMinutes} minutes in advance." });
            }

            var (isOpenAtScheduled, scheduledMsg) = EvaluateOutletOpenStatus(outlet, scheduledPickupUtc.Value);
            if (!isOpenAtScheduled)
            {
                return BadRequest(new { message = $"Selected scheduled time is outside operating hours ({outlet.OpeningTime} - {outlet.ClosingTime} {outlet.Timezone})." });
            }

            scheduledReleaseUtc = scheduledPickupUtc.Value.AddMinutes(-outlet.LeadTimeMinutes);
            if (scheduledReleaseUtc.Value <= nowUtc)
            {
                scheduledReleaseUtc = nowUtc;
            }
        }
        else
        {
            // Immediate fulfillment requires outlet to be open now
            var (isOpenNow, openMsg) = EvaluateOutletOpenStatus(outlet, nowUtc);
            if (!isOpenNow)
            {
                return BadRequest(new { message = openMsg });
            }
        }

        var restaurantId = outlet.RestaurantId;
        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : request.IdempotencyKey.Trim();

        // Canonical fingerprint for idempotency verification
        var canonicalDetails = new
        {
            outletId = outlet.Id,
            channel = request.OrderChannel,
            timing = request.FulfillmentTiming,
            phone = request.CustomerPhone?.Trim() ?? "",
            scheduledFor = request.ScheduledForUtc?.ToString("o") ?? "",
            items = request.Items.OrderBy(i => i.MenuItemId).Select(i => new
            {
                i.MenuItemId,
                i.Quantity,
                mods = (i.Modifiers ?? new List<StorefrontModifierSelectionDto>())
                    .OrderBy(m => m.ModifierOptionId)
                    .Select(m => m.ModifierOptionId)
            })
        };
        var fingerprint = ComputeFingerprint(canonicalDetails);

        // 1. Check idempotency: exact retry returns original order; changed request under same ID is rejected with 409 Conflict
        var existingOrder = await _db.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.TableBill)
            .FirstOrDefaultAsync(o => o.RestaurantId == restaurantId && o.SubmissionOperationId == idempotencyKey);

        if (existingOrder != null)
        {
            if (!string.IsNullOrEmpty(existingOrder.SubmissionFingerprint) && existingOrder.SubmissionFingerprint != fingerprint)
            {
                return Conflict(new { message = "This idempotency key was previously used for different order details." });
            }

            return Ok(MapToCheckoutResponse(existingOrder, isReplay: true));
        }

        // 2. Fetch server prices and verify no cross-tenant items
        var guidItemIds = new List<Guid>();
        foreach (var reqItem in request.Items)
        {
            if (Guid.TryParse(reqItem.MenuItemId, out var gid) && gid != Guid.Empty)
            {
                guidItemIds.Add(gid);
            }
        }

        var isCrossTenant = await _db.MenuItems
            .AnyAsync(m => guidItemIds.Contains(m.Id) && m.RestaurantId != restaurantId);
        if (isCrossTenant)
        {
            return BadRequest(new { message = "One or more items do not belong to this outlet's menu." });
        }

        var serverMenuItems = await _db.MenuItems
            .Include(m => m.ModifierGroups)
                .ThenInclude(mg => mg.ModifierGroup)
                .ThenInclude(g => g.Options)
            .Where(m => m.RestaurantId == restaurantId && guidItemIds.Contains(m.Id) && !m.IsDeleted)
            .ToDictionaryAsync(m => m.Id);

        decimal subtotal = 0m;
        var orderItems = new List<OrderItem>();

        foreach (var reqItem in request.Items)
        {
            Guid.TryParse(reqItem.MenuItemId, out var gId);
            serverMenuItems.TryGetValue(gId, out var menuItem);
            if (menuItem == null)
            {
                menuItem = await _db.MenuItems.FirstOrDefaultAsync(m => m.RestaurantId == restaurantId && m.Name.ToLower() == reqItem.Name.Trim().ToLower());
                if (menuItem == null)
                {
                    var defaultCat = await _db.Categories.FirstOrDefaultAsync(c => c.RestaurantId == restaurantId);
                    if (defaultCat == null)
                    {
                        defaultCat = new Category
                        {
                            Id = Guid.NewGuid(),
                            RestaurantId = restaurantId,
                            Name = "Online Storefront",
                            DisplayOrder = 1
                        };
                        _db.Categories.Add(defaultCat);
                        await _db.SaveChangesAsync();
                    }

                    menuItem = new MenuItem
                    {
                        Id = (gId != Guid.Empty && !await _db.MenuItems.AnyAsync(m => m.Id == gId)) ? gId : Guid.NewGuid(),
                        RestaurantId = restaurantId,
                        CategoryId = defaultCat.Id,
                        Name = reqItem.Name,
                        Price = reqItem.UnitPrice,
                        IsAvailable = true,
                        IsDeleted = false,
                        PackagingFee = 0m,
                        IsRefillEligible = false,
                        RefillMaxCount = 0
                    };
                    _db.MenuItems.Add(menuItem);
                    await _db.SaveChangesAsync();
                }
            }
            if (menuItem != null && !menuItem.IsAvailable)
            {
                return BadRequest(new { message = $"Item '{reqItem.Name}' is currently unavailable." });
            }

            // Server-validated pricing for modifiers
            decimal modTotal = 0m;
            if (reqItem.Modifiers != null && reqItem.Modifiers.Count > 0)
            {
                if (menuItem != null && menuItem.ModifierGroups != null)
                {
                    var availableOptions = menuItem.ModifierGroups
                        .SelectMany(mg => mg.ModifierGroup.Options)
                        .ToDictionary(opt => opt.Id.ToString());

                    foreach (var mod in reqItem.Modifiers)
                    {
                        if (availableOptions.TryGetValue(mod.ModifierOptionId, out var opt) && opt.IsAvailable)
                        {
                            modTotal += opt.PriceDelta;
                        }
                        else
                        {
                            modTotal += mod.PriceDelta;
                        }
                    }
                }
                else
                {
                    foreach (var mod in reqItem.Modifiers)
                    {
                        modTotal += mod.PriceDelta;
                    }
                }
            }

            var itemUnitPrice = menuItem != null ? menuItem.Price : reqItem.UnitPrice;
            var effectiveUnitPrice = itemUnitPrice + modTotal;
            subtotal += effectiveUnitPrice * reqItem.Quantity;

            orderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                MenuItemId = menuItem.Id,
                ItemNameSnapshot = menuItem.Name,
                Quantity = reqItem.Quantity,
                UnitPrice = effectiveUnitPrice,
                SpecialInstructions = reqItem.CookingNotes,
                ReleaseState = "Fired",
                PackingIntent = request.OrderChannel == "TakeawayPickup" ? "Takeaway" : "DineIn",
                PackagingFeeApplied = 0m,
                IsPackagingWaived = false,
                IsRefill = false,
                RefillCount = 0,
                CancelledQuantity = 0,
                IsCancelled = false
            });
        }

        // Validate Minimum Order Amount
        if (subtotal < outlet.MinOrderAmount)
        {
            return BadRequest(new { message = $"Minimum order amount is ₹{outlet.MinOrderAmount:F2}. Current subtotal is ₹{subtotal:F2}." });
        }

        // 3. Tax and Fees: 5% GST, persisted Delivery and Packaging fee rules
        var tax = Math.Round(subtotal * 0.05m, 2);
        var deliveryFee = request.OrderChannel == "DirectDelivery"
            ? (subtotal >= outlet.FreeDeliveryAboveAmount ? 0m : outlet.DeliveryBaseFee)
            : 0m;
        var packagingFee = (request.OrderChannel == "TakeawayPickup" || request.OrderChannel == "DirectDelivery") ? 15m : 0m;
        var totalPayable = subtotal + tax + deliveryFee + packagingFee;

        var orderStatus = isScheduled && scheduledReleaseUtc > nowUtc ? "Scheduled" : "Placed";

        // 4. Sequence numbers
        var maxOrderNum = await _db.Orders
            .Where(o => o.RestaurantId == restaurantId)
            .MaxAsync(o => (int?)o.OrderNumber) ?? 1000;
        var orderNumber = maxOrderNum + 1;

        var istNow = nowUtc.AddMinutes(330);
        var businessDate = istNow.ToString("yyyy-MM-dd");

        var maxToken = await _db.Orders
            .Where(o => o.RestaurantId == restaurantId && o.BusinessDate == businessDate)
            .MaxAsync(o => o.DailyTokenNumber) ?? 0;
        var tokenNumber = maxToken + 1;

        var orderId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        // Generate cryptographically secure tracking token
        var trackingToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

        // TableBill
        var bill = new TableBill
        {
            Id = billId,
            RestaurantId = restaurantId,
            OrderChannel = request.OrderChannel == "DirectDelivery" ? "Delivery" : "Parcel",
            DailyTokenNumber = tokenNumber,
            BusinessDate = businessDate,
            Subtotal = subtotal,
            Tax = tax,
            TotalAmount = totalPayable,
            PackagingCharge = packagingFee + deliveryFee,
            CreatedAtUtc = nowUtc
        };

        var order = new Order
        {
            Id = orderId,
            RestaurantId = restaurantId,
            OutletId = outlet.Id,
            OrderNumber = orderNumber,
            DailyTokenNumber = tokenNumber,
            BusinessDate = businessDate,
            OrderChannel = request.OrderChannel == "DirectDelivery" ? "Delivery" : "Parcel",
            Status = orderStatus,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            DeliveryProvider = request.OrderChannel == "DirectDelivery" ? "Direct Storefront" : null,
            IsScheduled = isScheduled,
            ScheduledPickupTimeUtc = scheduledPickupUtc,
            ScheduledReleaseTimeUtc = scheduledReleaseUtc,
            PreparationLeadMinutes = outlet.LeadTimeMinutes,
            SubmissionOperationId = idempotencyKey,
            SubmissionFingerprint = fingerprint,
            TrackingToken = trackingToken,
            Subtotal = subtotal,
            Tax = tax,
            PackagingCharge = packagingFee + deliveryFee,
            TotalAmount = totalPayable,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            TableBillId = billId,
            TableBill = bill,
            OrderItems = orderItems
        };

        _db.TableBills.Add(bill);
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Generate KOTs if releasing immediately
        if (orderStatus != "Scheduled")
        {
            try
            {
                var stations = await _db.KitchenStations
                    .Where(s => s.RestaurantId == restaurantId && s.IsActive)
                    .ToListAsync();

                var foodStation = stations.FirstOrDefault(s => s.Code.Equals("FOOD", StringComparison.OrdinalIgnoreCase)) ?? stations.FirstOrDefault();
                if (foodStation != null)
                {
                    var kotBatch = new KotBatch
                    {
                        Id = Guid.NewGuid(),
                        RestaurantId = restaurantId,
                        OrderId = orderId,
                        BatchSequence = 1,
                        KotNumber = $"KOT-{orderNumber:D4}-1",
                        CreatedAtUtc = nowUtc
                    };
                    _db.KotBatches.Add(kotBatch);

                    var kotTicket = new KotTicket
                    {
                        Id = Guid.NewGuid(),
                        RestaurantId = restaurantId,
                        OrderId = orderId,
                        KotBatchId = kotBatch.Id,
                        KitchenStationId = foodStation.Id,
                        StationNameSnapshot = foodStation.Name,
                        TicketNumber = $"KOT-{orderNumber:D4}-1-{foodStation.Code}",
                        OrderNumber = orderNumber,
                        TicketType = "New",
                        CreatedAtUtc = nowUtc,
                        TimestampUtc = nowUtc,
                        TimestampIstFormatted = istNow.ToString("dd-MMM-yyyy, hh:mm tt") + " IST",
                        ItemsJson = JsonSerializer.Serialize(orderItems.Select(i => new { i.MenuItemId, i.ItemNameSnapshot, i.Quantity, i.UnitPrice }))
                    };
                    _db.KotTickets.Add(kotTicket);
                    await _db.SaveChangesAsync();
                }
            }
            catch
            {
                // Non-fatal if kitchen station routing table is unconfigured
            }
        }

        return Ok(MapToCheckoutResponse(order, isReplay: false));
    }

    [HttpGet("orders/track/{trackingToken}")]
    [AllowAnonymous]
    public async Task<ActionResult<StorefrontOrderTrackingDto>> TrackOrderByToken(string trackingToken)
    {
        if (string.IsNullOrWhiteSpace(trackingToken))
        {
            return NotFound(new { message = "Tracking token is required." });
        }

        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.OrderItems)
            .Include(o => o.TableBill)
            .Include(o => o.Outlet)
            .FirstOrDefaultAsync(o => o.TrackingToken == trackingToken.Trim().ToLower());

        if (order == null)
        {
            return NotFound(new { message = "Order not found." });
        }

        var payeeVpa = "tablo.heritage@icici";
        var refId = $"TABLO-{order.OrderNumber:D4}";
        var upiUri = $"upi://pay?pa={payeeVpa}&pn=Tablo%20Heritage&am={order.TotalAmount:F2}&cu=INR&tr={refId}&tn=Order%20{order.OrderNumber}";

        var packagingFee = (order.OrderChannel == "Parcel" || order.OrderChannel == "Delivery") ? 15m : 0m;
        var deliveryFee = order.OrderChannel == "Delivery" ? Math.Max(0m, order.PackagingCharge - packagingFee) : 0m;

        var estMinutes = order.IsScheduled && order.ScheduledPickupTimeUtc.HasValue
            ? order.ScheduledPickupTimeUtc.Value
            : order.CreatedAt.AddMinutes(order.PreparationLeadMinutes ?? 25);

        var trackingDto = new StorefrontOrderTrackingDto(
            $"ORD-{order.OrderNumber:D4}",
            $"#TK-{order.DailyTokenNumber:D3}",
            order.Status switch
            {
                "Scheduled" => "Placed",
                "Placed" => "Placed",
                "Active" => "Preparing",
                "Ready" => "Ready",
                "Dispatched" => "Dispatched",
                "Served" or "Completed" => "Delivered",
                _ => "Placed"
            },
            order.OrderChannel,
            order.Outlet?.Name ?? "Main Outlet",
            order.CreatedAt,
            estMinutes,
            order.OrderItems.Select(oi => new StorefrontTrackingItemDto(
                oi.ItemNameSnapshot,
                oi.Quantity,
                oi.UnitPrice,
                oi.Quantity * oi.UnitPrice
            )).ToList(),
            order.Subtotal,
            order.Tax,
            deliveryFee,
            packagingFee,
            order.TotalAmount,
            order.TableBill?.SettledAtUtc != null ? "Paid" : "Pending",
            upiUri,
            payeeVpa
        );

        return Ok(trackingDto);
    }

    [HttpGet("orders/{orderId}")]
    [AllowAnonymous]
    public async Task<ActionResult<StorefrontCheckoutResponseDto>> GetOrderStatus(string orderId, [FromQuery] string? token = null)
    {
        if (!Guid.TryParse(orderId, out var gOrderId) || gOrderId == Guid.Empty)
        {
            return NotFound(new { message = "Order not found." });
        }

        var query = _db.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.TableBill)
            .Where(o => o.Id == gOrderId);

        if (!string.IsNullOrWhiteSpace(token))
        {
            query = query.Where(o => o.TrackingToken == token.Trim().ToLower());
        }

        var order = await query.FirstOrDefaultAsync();

        if (order == null)
        {
            return NotFound(new { message = "Order not found." });
        }

        return Ok(MapToCheckoutResponse(order, isReplay: false));
    }

    [HttpPut("admin/outlets/{outletId}")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin")]
    public async Task<ActionResult<StorefrontOutletSummaryDto>> UpdateOutletSettings(string outletId, [FromBody] UpdateOutletStorefrontSettingsDto dto)
    {
        if (!Guid.TryParse(outletId, out var gOutletId) || gOutletId == Guid.Empty)
        {
            return BadRequest(new { message = "Valid outlet ID is required." });
        }

        var actorRestId = User.GetRestaurantId();
        var outlet = await _db.Outlets.FirstOrDefaultAsync(o => o.Id == gOutletId && o.RestaurantId == actorRestId);

        if (outlet == null && !User.IsInRole("SuperAdmin"))
        {
            return Forbid();
        }

        if (outlet == null)
        {
            return NotFound(new { message = "Outlet not found." });
        }

        if (dto.IsOpen.HasValue) outlet.IsOpen = dto.IsOpen.Value;
        if (dto.IsPublished.HasValue) outlet.IsPublished = dto.IsPublished.Value;
        if (!string.IsNullOrWhiteSpace(dto.Timezone)) outlet.Timezone = dto.Timezone.Trim();
        if (!string.IsNullOrWhiteSpace(dto.OpeningTime)) outlet.OpeningTime = dto.OpeningTime.Trim();
        if (!string.IsNullOrWhiteSpace(dto.ClosingTime)) outlet.ClosingTime = dto.ClosingTime.Trim();
        if (dto.IsOvernight.HasValue) outlet.IsOvernight = dto.IsOvernight.Value;
        if (dto.LeadTimeMinutes.HasValue && dto.LeadTimeMinutes.Value > 0) outlet.LeadTimeMinutes = dto.LeadTimeMinutes.Value;
        if (dto.MinOrderAmount.HasValue && dto.MinOrderAmount.Value >= 0) outlet.MinOrderAmount = dto.MinOrderAmount.Value;
        if (dto.DeliveryBaseFee.HasValue && dto.DeliveryBaseFee.Value >= 0) outlet.DeliveryBaseFee = dto.DeliveryBaseFee.Value;
        if (dto.FreeDeliveryAboveAmount.HasValue && dto.FreeDeliveryAboveAmount.Value >= 0) outlet.FreeDeliveryAboveAmount = dto.FreeDeliveryAboveAmount.Value;
        if (!string.IsNullOrWhiteSpace(dto.Phone)) outlet.Phone = dto.Phone.Trim();
        if (dto.IsTemporarilyClosed.HasValue) outlet.IsTemporarilyClosed = dto.IsTemporarilyClosed.Value;
        if (dto.ClosureReason != null) outlet.ClosureReason = dto.ClosureReason.Trim();

        await _db.SaveChangesAsync();

        var (isOpen, _) = EvaluateOutletOpenStatus(outlet, DateTime.UtcNow);

        return Ok(new StorefrontOutletSummaryDto(
            outlet.Id.ToString(),
            outlet.RestaurantId.ToString(),
            outlet.Name,
            outlet.Address ?? "Connaught Place, New Delhi",
            outlet.Phone ?? "+91 98765 43210",
            isOpen,
            $"{outlet.OpeningTime} - {outlet.ClosingTime}",
            outlet.LeadTimeMinutes,
            outlet.MinOrderAmount,
            outlet.DeliveryBaseFee,
            outlet.FreeDeliveryAboveAmount,
            outlet.IsPublished,
            outlet.IsTemporarilyClosed,
            outlet.ClosureReason
        ));
    }

    private static StorefrontCheckoutResponseDto MapToCheckoutResponse(Order order, bool isReplay)
    {
        var refId = $"TABLO-{order.OrderNumber:D4}";
        var payeeVpa = "tablo.heritage@icici";
        var amount = order.TotalAmount;
        var upiUri = $"upi://pay?pa={payeeVpa}&pn=Tablo%20Heritage&am={amount:F2}&cu=INR&tr={refId}&tn=Order%20{order.OrderNumber}";

        var estMinutes = order.IsScheduled && order.ScheduledPickupTimeUtc.HasValue
            ? order.ScheduledPickupTimeUtc.Value
            : order.CreatedAt.AddMinutes(order.PreparationLeadMinutes ?? 25);

        // Compute delivery fee vs packaging fee
        var packagingFee = (order.OrderChannel == "Parcel" || order.OrderChannel == "Delivery") ? 15m : 0m;
        var deliveryFee = order.OrderChannel == "Delivery" ? Math.Max(0m, order.PackagingCharge - packagingFee) : 0m;

        return new StorefrontCheckoutResponseDto(
            order.Id.ToString(),
            $"ORD-{order.OrderNumber:D4}",
            $"#TK-{order.DailyTokenNumber:D3}",
            order.Status switch
            {
                "Scheduled" => "Placed",
                "Placed" => "Placed",
                "Active" => "Preparing",
                "Ready" => "Ready",
                "Dispatched" => "Dispatched",
                "Served" or "Completed" => "Delivered",
                _ => "Placed"
            },
            order.Subtotal,
            order.Tax,
            deliveryFee,
            packagingFee,
            order.TotalAmount,
            order.TableBill?.SettledAtUtc != null ? "Paid" : "Pending",
            upiUri,
            payeeVpa,
            refId,
            estMinutes,
            isReplay,
            order.TrackingToken ?? order.Id.ToString("N")
        );
    }
}
