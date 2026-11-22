using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Constants;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services.Printing;

public class KotRoutingService : IKotRoutingService
{
    private readonly ApplicationDbContext _db;
    private readonly ISubscriptionService _subscriptions;
    private readonly ILogger<KotRoutingService> _logger;

    public KotRoutingService(
        ApplicationDbContext db,
        ISubscriptionService subscriptions,
        ILogger<KotRoutingService> logger)
    {
        _db = db;
        _subscriptions = subscriptions;
        _logger = logger;
    }

    public async Task<List<KotTicket>> ProcessAcceptedOrderItemsAsync(
        Order order,
        List<OrderItem> newlyAcceptedItems,
        string actorEmail = "Staff",
        string trigger = "StaffAccepted")
    {
        if (newlyAcceptedItems == null || newlyAcceptedItems.Count == 0)
        {
            return new List<KotTicket>();
        }

        // 1. Check Restaurant Settings
        var settings = await _db.RestaurantSettings
            .Include(s => s.DefaultKitchenStation)
            .FirstOrDefaultAsync(s => s.RestaurantId == order.RestaurantId);

        if (settings == null || !settings.KotPrintingEnabled)
        {
            _logger.LogInformation("KOT printing is disabled for restaurant {RestaurantId}. Skipping KOT generation.", order.RestaurantId);
            return new List<KotTicket>();
        }

        // 2. Check Subscription Capability
        bool hasPrintingCap = await _subscriptions.HasCapabilityAsync(order.RestaurantId, Capabilities.ThermalPrinting);
        if (!hasPrintingCap)
        {
            _logger.LogWarning("Restaurant {RestaurantId} has KOT printing configured but lacks thermal_printing entitlement. Skipping KOT generation.", order.RestaurantId);
            return new List<KotTicket>();
        }

        bool hasMultiStationCap = await _subscriptions.HasCapabilityAsync(order.RestaurantId, Capabilities.MultipleKitchenStations);

        // 3. Load active stations
        var stations = await _db.KitchenStations
            .Include(s => s.Printer)
            .Where(s => s.RestaurantId == order.RestaurantId && s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync();

        if (stations.Count == 0)
        {
            _logger.LogWarning("Restaurant {RestaurantId} has no active kitchen stations. Skipping KOT generation.", order.RestaurantId);
            return new List<KotTicket>();
        }

        var defaultStation = stations.FirstOrDefault(s => s.IsDefault)
                             ?? stations.FirstOrDefault(s => s.Id == settings.DefaultKitchenStationId)
                             ?? stations[0];

        // 4. Resolve menu items and categories for station routing
        var menuItemIds = newlyAcceptedItems.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await _db.MenuItems
            .Include(m => m.Category)
            .Where(m => m.RestaurantId == order.RestaurantId && menuItemIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        // 5. Group items by station
        // If single kitchen setup OR multi-station not entitled OR only 1 station, route all to default station
        bool isMultiStationActive = hasMultiStationCap && settings.KitchenSetupMode != "SingleKitchen" && stations.Count > 1;

        var itemsByStation = new Dictionary<KitchenStation, List<OrderItem>>();

        foreach (var item in newlyAcceptedItems)
        {
            KitchenStation resolvedStation = defaultStation;

            if (isMultiStationActive && menuItems.TryGetValue(item.MenuItemId, out var mi))
            {
                if (mi.KitchenStationId.HasValue)
                {
                    var explicitStation = stations.FirstOrDefault(s => s.Id == mi.KitchenStationId.Value);
                    if (explicitStation != null) resolvedStation = explicitStation;
                }
                else if (mi.Category != null && mi.Category.KitchenStationId.HasValue)
                {
                    var catStation = stations.FirstOrDefault(s => s.Id == mi.Category.KitchenStationId.Value);
                    if (catStation != null) resolvedStation = catStation;
                }
            }

            if (!itemsByStation.TryGetValue(resolvedStation, out var list))
            {
                list = new List<OrderItem>();
                itemsByStation[resolvedStation] = list;
            }
            list.Add(item);
        }

        // 6. Create KotBatch
        int existingBatchCount = await _db.KotBatches.CountAsync(b => b.RestaurantId == order.RestaurantId && b.OrderId == order.Id);
        int batchSeq = existingBatchCount + 1;
        string kotNumber = $"KOT-{order.OrderNumber:D4}-{batchSeq}";

        var batch = new KotBatch
        {
            Id = Guid.NewGuid(),
            RestaurantId = order.RestaurantId,
            OrderId = order.Id,
            BatchSequence = batchSeq,
            KotNumber = kotNumber,
            TriggeredBy = trigger,
            ActorEmail = actorEmail,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.KotBatches.Add(batch);

        // 7. Load Restaurant & Table details for snapshot
        var restaurant = await _db.Restaurants.FindAsync(order.RestaurantId);
        string restName = restaurant?.Name ?? "Restaurant";

        int tableNum = order.Table?.TableNumber ?? 0;
        if (tableNum == 0)
        {
            var tbl = await _db.RestaurantTables.FindAsync(order.TableId);
            tableNum = tbl?.TableNumber ?? 0;
        }

        string ticketType;
        if (trigger == "CourseFire") ticketType = "CourseFire";
        else if (trigger == "Refill") ticketType = "Refill";
        else ticketType = batchSeq == 1 ? "New" : "Incremental";

        var createdTickets = new List<KotTicket>();

        foreach (var (station, items) in itemsByStation)
        {
            string stationCode = !string.IsNullOrWhiteSpace(station.Code) ? station.Code : "MAIN";
            string ticketNumber = $"KOT-{order.OrderNumber:D4}-{batchSeq}-{stationCode}";

            var itemSnapshots = items.Select(i => new KotTicketItemSnapshotDto
            {
                OrderItemId = i.Id,
                MenuItemId = i.MenuItemId,
                Name = !string.IsNullOrEmpty(i.ItemNameSnapshot)
                    ? i.ItemNameSnapshot
                    : (menuItems.TryGetValue(i.MenuItemId, out var mi) ? mi.Name : "Item"),
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                SpecialInstructions = i.SpecialInstructions,
                ModifiersJson = i.ModifiersJson,
                CourseName = i.CourseName,
                PackingIntent = i.PackingIntent ?? "DineIn",
                IsRefill = i.IsRefill
            }).ToList();

            var ticket = new KotTicket
            {
                Id = Guid.NewGuid(),
                RestaurantId = order.RestaurantId,
                OrderId = order.Id,
                KotBatchId = batch.Id,
                KitchenStationId = station.Id,
                CourseName = items.FirstOrDefault()?.CourseName,
                StationNameSnapshot = station.Name,
                PrinterId = station.PrinterId,
                TicketNumber = ticketNumber,
                OrderNumber = order.OrderNumber,
                TableId = order.TableId,
                TableNumber = tableNum,
                DiningGroupId = order.DiningGroupId,
                GroupLabelSnapshot = order.GroupLabel,
                TicketType = ticketType,
                TimestampUtc = DateTime.UtcNow,
                TimestampIstFormatted = EscPosFormatter.FormatIstDateTime(DateTime.UtcNow),
                ItemsJson = JsonSerializer.Serialize(itemSnapshots),
                Notes = null,
                IsReprint = false,
                ReprintCount = 0,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.KotTickets.Add(ticket);
            createdTickets.Add(ticket);

            // 8. Create PrintJob if station has an assigned active printer
            if (station.Printer != null && station.Printer.IsActive)
            {
                var printer = station.Printer;
                var (escPosBytes, readableText) = EscPosFormatter.FormatKotTicket(
                    restaurantName: restName,
                    stationName: station.Name,
                    ticketNumber: ticketNumber,
                    orderNumber: order.OrderNumber,
                    tableNumber: tableNum,
                    groupLabel: order.GroupLabel,
                    ticketType: ticketType,
                    isReprint: false,
                    reprintCount: 0,
                    reprintReason: null,
                    timestampUtc: ticket.TimestampUtc,
                    items: itemSnapshots,
                    notes: null,
                    paperWidthMm: printer.PaperWidthMm,
                    orderChannel: order.OrderChannel ?? "DineIn",
                    dailyTokenNumber: order.DailyTokenNumber,
                    customerName: order.CustomerName,
                    customerPhone: order.CustomerPhone,
                    vehiclePlate: order.VehiclePlate,
                    vehicleModel: order.VehicleModel,
                    parkingBay: order.ParkingBay,
                    isScheduled: order.IsScheduled,
                    scheduledPickupTimeUtc: order.ScheduledPickupTimeUtc,
                    deliveryProvider: order.DeliveryProvider,
                    externalOrderReference: order.ExternalOrderReference);

                var job = new PrintJob
                {
                    Id = Guid.NewGuid(),
                    RestaurantId = order.RestaurantId,
                    KotTicketId = ticket.Id,
                    PrinterId = printer.Id,
                    StationId = station.Id,
                    JobType = "KOT",
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
            }
        }

        order.KotPrintStatus = createdTickets.Count > 0 ? "Queued" : "Skipped";
        order.KotPrintError = null;

        await _db.SaveChangesAsync();
        return createdTickets;
    }

    public async Task<KotTicket> ReprintTicketAsync(
        Guid restaurantId,
        Guid ticketId,
        string actorEmail,
        string reason)
    {
        var original = await _db.KotTickets
            .Include(t => t.KitchenStation)
                .ThenInclude(s => s.Printer)
            .Include(t => t.Restaurant)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.RestaurantId == restaurantId);

        if (original == null)
        {
            throw new KeyNotFoundException("KOT ticket not found or does not belong to this restaurant.");
        }

        bool hasPrintingCap = await _subscriptions.HasCapabilityAsync(restaurantId, Capabilities.ThermalPrinting);
        if (!hasPrintingCap)
        {
            throw new InvalidOperationException("Restaurant does not have thermal_printing entitlement.");
        }

        var items = JsonSerializer.Deserialize<List<KotTicketItemSnapshotDto>>(original.ItemsJson) ?? new();
        int newReprintCount = original.ReprintCount + 1;
        original.ReprintCount = newReprintCount;

        string reprintTicketNumber = $"{original.TicketNumber}-R{newReprintCount}";

        var reprintTicket = new KotTicket
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            OrderId = original.OrderId,
            KotBatchId = original.KotBatchId,
            KitchenStationId = original.KitchenStationId,
            StationNameSnapshot = original.StationNameSnapshot,
            PrinterId = original.PrinterId,
            TicketNumber = reprintTicketNumber,
            OrderNumber = original.OrderNumber,
            TableId = original.TableId,
            TableNumber = original.TableNumber,
            DiningGroupId = original.DiningGroupId,
            GroupLabelSnapshot = original.GroupLabelSnapshot,
            TicketType = "Reprint",
            TimestampUtc = DateTime.UtcNow,
            TimestampIstFormatted = EscPosFormatter.FormatIstDateTime(DateTime.UtcNow),
            ItemsJson = original.ItemsJson,
            Notes = original.Notes,
            IsReprint = true,
            ReprintCount = newReprintCount,
            OriginalTicketId = original.Id,
            ReprintReason = reason,
            ReprinterEmail = actorEmail,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.KotTickets.Add(reprintTicket);

        // Assign printer: from station or printer table
        var printer = original.Printer ?? original.KitchenStation?.Printer;
        if (printer != null && printer.IsActive)
        {
            var (escPosBytes, readableText) = EscPosFormatter.FormatKotTicket(
                restaurantName: original.Restaurant.Name,
                stationName: original.StationNameSnapshot,
                ticketNumber: reprintTicketNumber,
                orderNumber: original.OrderNumber,
                tableNumber: original.TableNumber,
                groupLabel: original.GroupLabelSnapshot,
                ticketType: "Reprint",
                isReprint: true,
                reprintCount: newReprintCount,
                reprintReason: reason,
                timestampUtc: reprintTicket.TimestampUtc,
                items: items,
                notes: original.Notes,
                paperWidthMm: printer.PaperWidthMm);

            var job = new PrintJob
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurantId,
                KotTicketId = reprintTicket.Id,
                PrinterId = printer.Id,
                StationId = original.KitchenStationId,
                JobType = "Reprint",
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
        }

        await _db.SaveChangesAsync();
        return reprintTicket;
    }

    public async Task RecordTableTransferNoticeAsync(
        Guid restaurantId,
        Guid orderId,
        int oldTable,
        int newTable,
        string? oldGroup,
        string? newGroup)
    {
        var settings = await _db.RestaurantSettings.FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
        if (settings == null || !settings.KotPrintingEnabled) return;

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId);
        if (order == null) return;

        // Find tickets previously issued for this order
        var tickets = await _db.KotTickets
            .Include(t => t.KitchenStation)
                .ThenInclude(s => s.Printer)
            .Include(t => t.Restaurant)
            .Where(t => t.RestaurantId == restaurantId && t.OrderId == orderId)
            .ToListAsync();

        if (tickets.Count == 0) return;

        // Group by distinct station so each station that got tickets gets a transfer notice
        var distinctStations = tickets.Select(t => t.KitchenStation).Distinct().ToList();

        foreach (var station in distinctStations)
        {
            if (station == null || station.Printer == null || !station.Printer.IsActive) continue;

            string oldLoc = $"Table {oldTable}" + (!string.IsNullOrWhiteSpace(oldGroup) ? $" ({oldGroup})" : "");
            string newLoc = $"Table {newTable}" + (!string.IsNullOrWhiteSpace(newGroup) ? $" ({newGroup})" : "");
            string noticeText = $"TABLE TRANSFER: {oldLoc} -> {newLoc}";

            var noticeItem = new List<KotTicketItemSnapshotDto>
            {
                new()
                {
                    Name = noticeText,
                    Quantity = 1,
                    SpecialInstructions = $"Order ORD-{order.OrderNumber:D4} relocated"
                }
            };

            var (bytes, readable) = EscPosFormatter.FormatKotTicket(
                restaurantName: tickets[0].Restaurant.Name,
                stationName: station.Name,
                ticketNumber: $"TRANSFER-ORD-{order.OrderNumber:D4}",
                orderNumber: order.OrderNumber,
                tableNumber: newTable,
                groupLabel: newGroup,
                ticketType: "TransferNotice",
                isReprint: false,
                reprintCount: 0,
                reprintReason: null,
                timestampUtc: DateTime.UtcNow,
                items: noticeItem,
                notes: noticeText,
                paperWidthMm: station.Printer.PaperWidthMm);

            var job = new PrintJob
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurantId,
                KotTicketId = null,
                PrinterId = station.Printer.Id,
                StationId = station.Id,
                JobType = "Notice",
                Status = "Pending",
                PayloadEscPosBase64 = Convert.ToBase64String(bytes),
                ReadableText = readable,
                PaperWidthMm = station.Printer.PaperWidthMm,
                Attempts = 0,
                MaxAttempts = 3,
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.PrintJobs.Add(job);
        }

        await _db.SaveChangesAsync();
    }

    public async Task RecordOrderCancellationNoticeAsync(Guid restaurantId, Guid orderId, string reason)
    {
        var settings = await _db.RestaurantSettings.FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
        if (settings == null || !settings.KotPrintingEnabled) return;

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId);
        if (order == null) return;

        // Cancel any pending print jobs for this order
        var pendingJobs = await _db.PrintJobs
            .Include(j => j.KotTicket)
            .Where(j => j.RestaurantId == restaurantId && j.KotTicket != null && j.KotTicket.OrderId == orderId && j.Status == "Pending")
            .ToListAsync();

        foreach (var job in pendingJobs)
        {
            job.Status = "Cancelled";
            job.LastError = $"Order was cancelled: {reason}";
            job.UpdatedAtUtc = DateTime.UtcNow;
        }

        // Send cancellation notice to stations that already printed tickets
        var printedTickets = await _db.KotTickets
            .Include(t => t.KitchenStation)
                .ThenInclude(s => s.Printer)
            .Include(t => t.Restaurant)
            .Where(t => t.RestaurantId == restaurantId && t.OrderId == orderId)
            .ToListAsync();

        var distinctStations = printedTickets.Select(t => t.KitchenStation).Distinct().ToList();
        foreach (var station in distinctStations)
        {
            if (station == null || station.Printer == null || !station.Printer.IsActive) continue;

            var cancelItem = new List<KotTicketItemSnapshotDto>
            {
                new()
                {
                    Name = $"ORDER CANCELLED: ORD-{order.OrderNumber:D4}",
                    Quantity = 1,
                    SpecialInstructions = reason
                }
            };

            var (bytes, readable) = EscPosFormatter.FormatKotTicket(
                restaurantName: printedTickets[0].Restaurant.Name,
                stationName: station.Name,
                ticketNumber: $"CANCEL-ORD-{order.OrderNumber:D4}",
                orderNumber: order.OrderNumber,
                tableNumber: order.Table?.TableNumber ?? 0,
                groupLabel: order.GroupLabel,
                ticketType: "CancellationNotice",
                isReprint: false,
                reprintCount: 0,
                reprintReason: reason,
                timestampUtc: DateTime.UtcNow,
                items: cancelItem,
                notes: $"Reason: {reason}",
                paperWidthMm: station.Printer.PaperWidthMm);

            var job = new PrintJob
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurantId,
                KotTicketId = null,
                PrinterId = station.Printer.Id,
                StationId = station.Id,
                JobType = "Notice",
                Status = "Pending",
                PayloadEscPosBase64 = Convert.ToBase64String(bytes),
                ReadableText = readable,
                PaperWidthMm = station.Printer.PaperWidthMm,
                Attempts = 0,
                MaxAttempts = 3,
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.PrintJobs.Add(job);
        }

        await _db.SaveChangesAsync();
    }

    public async Task<KotTicket?> RecordItemCancellationNoticeAsync(
        Guid restaurantId,
        Guid orderId,
        OrderItem item,
        int cancelledQuantity,
        string outcome,
        string reason,
        string? approverName)
    {
        var settings = await _db.RestaurantSettings.FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
        if (settings == null || !settings.KotPrintingEnabled) return null;

        var order = await _db.Orders.Include(o => o.Table).FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId);
        if (order == null) return null;

        var menuItem = await _db.MenuItems.Include(m => m.Category).FirstOrDefaultAsync(m => m.Id == item.MenuItemId);
        var stations = await _db.KitchenStations.Include(s => s.Printer).Where(s => s.RestaurantId == restaurantId && s.IsActive).ToListAsync();
        if (stations.Count == 0) return null;

        var defaultStation = stations.FirstOrDefault(s => s.IsDefault) ?? stations[0];
        KitchenStation targetStation = defaultStation;

        if (menuItem?.KitchenStationId.HasValue == true)
        {
            var explicitStation = stations.FirstOrDefault(s => s.Id == menuItem.KitchenStationId.Value);
            if (explicitStation != null) targetStation = explicitStation;
        }
        else if (menuItem?.Category?.KitchenStationId.HasValue == true)
        {
            var catStation = stations.FirstOrDefault(s => s.Id == menuItem.Category.KitchenStationId.Value);
            if (catStation != null) targetStation = catStation;
        }

        if (targetStation.Printer == null || !targetStation.Printer.IsActive) return null;

        var cancelItem = new List<KotTicketItemSnapshotDto>
        {
            new()
            {
                OrderItemId = item.Id,
                MenuItemId = item.MenuItemId,
                Name = $"{item.ItemNameSnapshot} [CANCELLED - {outcome.ToUpperInvariant()}]",
                Quantity = cancelledQuantity,
                SpecialInstructions = $"Reason: {reason}" + (!string.IsNullOrWhiteSpace(approverName) ? $" | Approved by: {approverName}" : "")
            }
        };

        var restaurant = await _db.Restaurants.FindAsync(restaurantId);
        string restName = restaurant?.Name ?? "Restaurant";

        var (bytes, readable) = EscPosFormatter.FormatKotTicket(
            restaurantName: restName,
            stationName: targetStation.Name,
            ticketNumber: $"CANCEL-ORD-{order.OrderNumber:D4}",
            orderNumber: order.OrderNumber,
            tableNumber: order.Table?.TableNumber ?? 0,
            groupLabel: order.GroupLabel,
            ticketType: "CancellationNotice",
            isReprint: false,
            reprintCount: 0,
            reprintReason: reason,
            timestampUtc: DateTime.UtcNow,
            items: cancelItem,
            notes: $"Reason: {reason} | Outcome: {outcome}" + (!string.IsNullOrWhiteSpace(approverName) ? $" | Approved by: {approverName}" : ""),
            paperWidthMm: targetStation.Printer.PaperWidthMm,
            orderChannel: order.OrderChannel,
            dailyTokenNumber: order.DailyTokenNumber,
            customerName: order.CustomerName,
            customerPhone: order.CustomerPhone);

        var noticeTicket = new KotTicket
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            OrderId = orderId,
            KotBatchId = Guid.NewGuid(),
            KitchenStationId = targetStation.Id,
            StationNameSnapshot = targetStation.Name,
            PrinterId = targetStation.Printer.Id,
            TicketNumber = $"CANCEL-ORD-{order.OrderNumber:D4}",
            OrderNumber = order.OrderNumber,
            TableId = order.TableId,
            TableNumber = order.Table?.TableNumber ?? 0,
            DiningGroupId = order.DiningGroupId,
            GroupLabelSnapshot = order.GroupLabel,
            TicketType = "CancellationNotice",
            TimestampUtc = DateTime.UtcNow,
            TimestampIstFormatted = EscPosFormatter.FormatIstDateTime(DateTime.UtcNow),
            ItemsJson = JsonSerializer.Serialize(cancelItem),
            Notes = $"Cancelled: {cancelledQuantity}x {item.ItemNameSnapshot} ({outcome}) Reason: {reason}",
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.KotTickets.Add(noticeTicket);

        var job = new PrintJob
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            KotTicketId = noticeTicket.Id,
            PrinterId = targetStation.Printer.Id,
            StationId = targetStation.Id,
            JobType = "CancellationNotice",
            Status = "Pending",
            PayloadEscPosBase64 = Convert.ToBase64String(bytes),
            ReadableText = readable,
            PaperWidthMm = targetStation.Printer.PaperWidthMm,
            Attempts = 0,
            MaxAttempts = 3,
            CreatedBy = approverName ?? "Manager",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.PrintJobs.Add(job);
        await _db.SaveChangesAsync();

        return noticeTicket;
    }

    public async Task<RoutingPreviewResponseDto> PreviewRoutingAsync(
        Guid restaurantId,
        List<RoutingPreviewItemRequestDto> items)
    {
        var settings = await _db.RestaurantSettings
            .Include(s => s.DefaultKitchenStation)
            .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);

        string mode = settings?.KitchenSetupMode ?? "SingleKitchen";

        var stations = await _db.KitchenStations
            .Include(s => s.Printer)
            .Where(s => s.RestaurantId == restaurantId && s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync();

        var defaultStation = stations.FirstOrDefault(s => s.IsDefault)
                             ?? stations.FirstOrDefault(s => s.Id == settings?.DefaultKitchenStationId)
                             ?? stations.FirstOrDefault();

        var response = new RoutingPreviewResponseDto
        {
            KitchenSetupMode = mode
        };

        if (stations.Count == 0 || defaultStation == null)
        {
            return response;
        }

        var itemIds = items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await _db.MenuItems
            .Include(m => m.Category)
            .Where(m => m.RestaurantId == restaurantId && itemIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        bool isMulti = mode != "SingleKitchen" && stations.Count > 1;

        var grouped = new Dictionary<KitchenStation, List<StationRoutingPreviewItemDto>>();

        foreach (var reqItem in items)
        {
            KitchenStation targetStation = defaultStation;
            string resolvedBy = "DefaultStation";

            if (menuItems.TryGetValue(reqItem.MenuItemId, out var mi))
            {
                if (isMulti && mi.KitchenStationId.HasValue)
                {
                    var st = stations.FirstOrDefault(s => s.Id == mi.KitchenStationId.Value);
                    if (st != null)
                    {
                        targetStation = st;
                        resolvedBy = "ItemOverride";
                    }
                }
                else if (isMulti && mi.Category != null && mi.Category.KitchenStationId.HasValue)
                {
                    var st = stations.FirstOrDefault(s => s.Id == mi.Category.KitchenStationId.Value);
                    if (st != null)
                    {
                        targetStation = st;
                        resolvedBy = "CategoryDefault";
                    }
                }

                if (!grouped.TryGetValue(targetStation, out var list))
                {
                    list = new List<StationRoutingPreviewItemDto>();
                    grouped[targetStation] = list;
                }

                list.Add(new StationRoutingPreviewItemDto
                {
                    MenuItemId = mi.Id,
                    ItemName = mi.Name,
                    CategoryName = mi.Category?.Name ?? "Uncategorized",
                    Quantity = reqItem.Quantity,
                    ResolvedBy = resolvedBy
                });
            }
        }

        foreach (var (st, previewItems) in grouped)
        {
            response.StationTickets.Add(new StationRoutingPreviewDto
            {
                StationId = st.Id,
                StationName = st.Name,
                StationCode = st.Code,
                AssignedPrinterName = st.Printer?.Name,
                Items = previewItems
            });
        }

        return response;
    }
}
