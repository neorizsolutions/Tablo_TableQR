using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Microsoft.AspNetCore.SignalR;
using Tablo.Api.Hubs;

namespace Tablo.Api.Services;

public class BillingService(ApplicationDbContext db, IHubContext<OrderHub>? hub = null, ILogger<BillingService>? logger = null)
{
    public async Task<BillDto> ReceiptAsync(Guid restaurantId, Guid billId)
    {
        var json = await db.TableBills.AsNoTracking()
            .Where(b=>b.RestaurantId==restaurantId && b.Id==billId && b.SettledAtUtc!=null)
            .Select(b=>b.ReceiptJson).SingleOrDefaultAsync();
        return json == null ? throw new KeyNotFoundException("Receipt not found.")
            : JsonSerializer.Deserialize<BillDto>(json) ?? throw new InvalidOperationException("Receipt snapshot is invalid.");
    }
    internal static bool Chargeable(Order o) => o.Status is not ("Cancelled" or "Rejected");

    internal static async Task RequireActorAsync(ApplicationDbContext db, Guid restaurantId, Guid actorId)
    {
        if (restaurantId == Guid.Empty || actorId == Guid.Empty || !await db.Employees.AnyAsync(e =>
            e.Id == actorId && e.RestaurantId == restaurantId && !e.IsDeleted && e.IsActive &&
            (e.Role.Name == "Admin" || e.Role.Name == "Owner" || e.Role.Name == "Cashier" || e.Role.Name == "Waiter" || e.Role.Name == "Kitchen")))
            throw new UnauthorizedAccessException("An active restaurant billing account is required.");
    }

    internal static async Task<TableBill> AttachAsync(ApplicationDbContext db, RestaurantTable table, Order order)
    {
        if (order.TableBillId.HasValue)
        {
            var attached = await db.TableBills.SingleAsync(b => b.Id == order.TableBillId && b.RestaurantId == table.RestaurantId && b.TableId == table.Id);
            if (attached.SettledAtUtc.HasValue) throw new ServiceConflictException("A settled order cannot accept more items.");
            if (attached.SplitAtUtc != null) throw new ServiceConflictException("This bill has been split. Start a new order.");
            if (attached.PaymentRevision != null) throw new ServiceConflictException("This bill is frozen because payments have started.");
            return attached;
        }
        var bill = db.TableBills.Local.FirstOrDefault(b => b.TableId == table.Id && b.RestaurantId == table.RestaurantId && b.DiningGroupId == order.DiningGroupId && b.SettledAtUtc == null && b.SplitAtUtc == null && b.PaymentRevision == null && b.ParentBillId == null)
            ?? await db.TableBills.SingleOrDefaultAsync(b => b.TableId == table.Id && b.RestaurantId == table.RestaurantId && b.DiningGroupId == order.DiningGroupId && b.SettledAtUtc == null && b.SplitAtUtc == null && b.PaymentRevision == null && b.ParentBillId == null);
        if (bill == null)
        {
            bill = new TableBill { RestaurantId = table.RestaurantId, TableId = table.Id, TableNumberSnapshot = table.TableNumber, DiningGroupId=order.DiningGroupId, GroupLabelSnapshot=order.GroupLabel };
            db.TableBills.Add(bill);
        }
        order.TableBill = bill;
        order.TableBillId = bill.Id;
        return bill;
    }

    public async Task<IReadOnlyList<ServiceTableDto>> ListServiceTablesAsync(Guid restaurantId)
    {
        var tables = await db.RestaurantTables.AsNoTracking().Where(t => t.RestaurantId == restaurantId)
            .OrderBy(t => t.TableNumber).Select(t => new { t.Id, t.TableNumber, t.IsActive }).ToListAsync();
        var activeOrders = await db.Orders.AsNoTracking()
            .Where(o => o.RestaurantId == restaurantId && o.TableId != null && o.OrderChannel == "DineIn"
                && o.Status != "Served" && o.Status != "Cancelled" && o.Status != "Rejected")
            .GroupBy(o => o.TableId!.Value).Select(g => new { TableId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.TableId, g => g.Count);
        var openBills = await db.TableBills.AsNoTracking().Include(b => b.Orders).ThenInclude(o => o.OrderItems)
            .AsSplitQuery()
            .Where(b => b.RestaurantId == restaurantId && b.TableId != null && b.OrderChannel == "DineIn"
                && b.SettledAtUtc == null && b.SplitAtUtc == null).ToListAsync();
        var billGroups = openBills.Select(b => new { TableId = b.TableId!.Value, Bill = Project(b) })
            .Where(b => b.Bill.TotalAmount > 0).ToLookup(b => b.TableId, b => b.Bill);

        return tables.Select(t =>
        {
            var activeCount = activeOrders.GetValueOrDefault(t.Id);
            var bills = billGroups[t.Id];
            var readyGroups = bills.Where(b => b.CanSettle)
                .Select(b => new BillReadyGroupDto(b.DiningGroupId, b.GroupLabel))
                .Distinct().OrderBy(g => g.GroupLabel).ToList();
            var status = readyGroups.Count > 0 ? "Bill Ready" : activeCount > 0 || bills.Any() ? "Occupied" : "Available";
            return new ServiceTableDto(t.Id, t.TableNumber, t.IsActive, status, activeCount, readyGroups);
        }).ToList();
    }

    public async Task<TableBillReviewDto> ReviewAsync(Guid restaurantId, Guid tableId, Guid? groupId=null)
    {
        var table = await db.RestaurantTables.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tableId && t.RestaurantId == restaurantId)
            ?? throw new KeyNotFoundException("Table not found.");
        var open = await db.TableBills.AsNoTracking().Include(b => b.Orders).ThenInclude(o => o.OrderItems)
            .Where(b => b.RestaurantId == restaurantId && b.TableId == tableId && b.DiningGroupId == groupId && b.SettledAtUtc == null && b.SplitAtUtc == null).OrderBy(b=>b.CreatedAtUtc).ThenBy(b=>b.Id).ToListAsync();
        var projected = new List<BillDto>();
        foreach(var b in open) projected.Add(await ViewAsync(b));
        var receipts = await db.TableBills.AsNoTracking().Where(b => b.RestaurantId == restaurantId && b.TableId == tableId && b.DiningGroupId == groupId && b.SettledAtUtc != null)
            .OrderByDescending(b => b.SettledAtUtc).Take(10).ToListAsync();
        return new TableBillReviewDto
        {
            TableId = tableId, TableNumber = table.TableNumber, OpenBill = projected.FirstOrDefault(), OpenBills=projected,
            UnbilledActiveOrderCount = await db.Orders.CountAsync(o => o.RestaurantId == restaurantId && o.TableId == tableId && o.DiningGroupId == groupId && o.TableBillId == null
                && o.Status != "Served" && o.Status != "Cancelled" && o.Status != "Rejected"),
            RecentSettlements = receipts.Select(b => JsonSerializer.Deserialize<BillDto>(b.ReceiptJson!)!).ToList()
        };
    }

    public async Task<BillDto> OpenAsync(Guid restaurantId, Guid tableId, Guid actorId, Guid? groupId=null)
    {
        await RequireActorAsync(db, restaurantId, actorId);
        await using var tx = await TableOrderTransaction.BeginAsync(db);
        await TableOrderTransaction.LockAsync(db, restaurantId, tableId);
        var table = await db.RestaurantTables.SingleOrDefaultAsync(t => t.Id == tableId && t.RestaurantId == restaurantId)
            ?? throw new KeyNotFoundException("Table not found.");
        var active = await db.Orders.Where(o => o.RestaurantId == restaurantId && o.TableId == tableId && o.DiningGroupId==groupId && o.TableBillId == null
            && o.Status != "Served" && o.Status != "Cancelled" && o.Status != "Rejected").ToListAsync();
        foreach (var order in active) await AttachAsync(db, table, order);
        await db.SaveChangesAsync();
        var bill = await db.TableBills.Include(b => b.Orders).ThenInclude(o => o.OrderItems)
            .SingleOrDefaultAsync(b => b.RestaurantId == restaurantId && b.TableId == tableId && b.DiningGroupId==groupId && b.SettledAtUtc == null && b.SplitAtUtc==null && b.PaymentRevision==null && b.ParentBillId==null)
            ?? throw new ServiceConflictException("There are no active orders to start a bill for.");
        if (tx != null) await tx.CommitAsync();
        return Project(bill);
    }

    public async Task<BillDto> SettleAsync(Guid restaurantId, Guid billId, Guid actorId, SettleBillRequestDto request)
    {
        var billMeta = await db.TableBills.Where(b => b.Id == billId && b.RestaurantId == restaurantId)
            .Select(b => new { b.TableId, b.OrderChannel }).SingleOrDefaultAsync() ?? throw new KeyNotFoundException("Bill not found.");
        await RequireActorAsync(db, restaurantId, actorId);
        if (request.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(request.ExpectedRevision) || request.PaymentMethod is not ("Cash" or "UPI" or "Card" or "MarketplaceCollected" or "Marketplace"))
            throw new ArgumentException("A valid operation ID, reviewed bill and payment method are required.");
        await using var tx = await TableOrderTransaction.BeginAsync(db);
        if (billMeta.TableId.HasValue)
        {
            await TableOrderTransaction.LockAsync(db, restaurantId, billMeta.TableId.Value);
        }
        await TableOrderTransaction.LockBillAsync(db, restaurantId, billId);
        var bill = await db.TableBills.Include(b => b.Orders).ThenInclude(o => o.OrderItems)
            .SingleAsync(b => b.Id == billId && b.RestaurantId == restaurantId);
        if(bill.TableId != billMeta.TableId) throw new ServiceConflictException("This bill moved. Refresh before paying.");
        var hash = Hash(JsonSerializer.Serialize(new { billId, actorId, request.ExpectedRevision, ExpectedTotal = Amount(request.ExpectedTotal), request.PaymentMethod }));
        if (bill.SettledAtUtc.HasValue)
        {
            if (bill.SettlementOperationId != request.OperationId || bill.SettlementHash != hash)
                throw new ServiceConflictException("This bill is already settled. Refresh its receipt.");
            return JsonSerializer.Deserialize<BillDto>(bill.ReceiptJson!)!;
        }
        if (await db.TableBills.AnyAsync(b => b.RestaurantId == restaurantId && b.SettlementOperationId == request.OperationId))
            throw new ServiceConflictException("This operation ID has already been used for another bill.");
        if (bill.PaymentRevision != null || await db.BillPayments.AnyAsync(p=>p.RestaurantId==restaurantId && p.TableBillId==billId))
            throw new ServiceConflictException("This bill has recorded payments. Use its remaining balance instead of full settlement.");
        // Do not settle an incomplete legacy attachment or food still being prepared.
        if (bill.TableId.HasValue && await db.Orders.AnyAsync(o => o.RestaurantId == restaurantId && o.TableId == bill.TableId.Value && o.DiningGroupId == bill.DiningGroupId && o.TableBillId == null
            && o.Status != "Served" && o.Status != "Cancelled" && o.Status != "Rejected"))
            throw new ServiceConflictException("Attach the table's active orders before settling.");
        if(bill.SplitAtUtc!=null) throw new ServiceConflictException("This bill has been split.");
        var reviewed = Project(bill);
        if (!reviewed.CanSettle)
        {
            bool hasHeld = bill.Orders.Where(Chargeable).Any(o => o.OrderItems.Any(i => !i.IsCancelled && (i.Quantity - i.CancelledQuantity) > 0 && i.ReleaseState == "Held"));
            bool isParcelLike = bill.OrderChannel is "ParcelTakeaway" or "CarParking" or "Delivery" || bill.Orders.Any(o => o.OrderChannel is "ParcelTakeaway" or "CarParking" or "Delivery" || o.IsScheduled);
            throw new ServiceConflictException(hasHeld ? "Cannot settle bill with unresolved held food items." : (isParcelLike ? "Parcel order cannot be settled in its current state." : "All chargeable orders must be ready or served before settlement."));
        }
        if (reviewed.Revision != request.ExpectedRevision || reviewed.TotalAmount != request.ExpectedTotal)
            throw new ServiceConflictException("The bill changed. Review the latest items and total before settling.");
        bill.SettledAtUtc = DateTime.UtcNow;
        bill.SettledByEmployeeId = actorId;
        bill.SettlementOperationId = request.OperationId;
        bill.SettlementHash = hash;
        bill.PaymentMethod = request.PaymentMethod;
        bill.PackagingCharge = reviewed.PackagingCharge;
        bill.Subtotal = reviewed.Subtotal;
        bill.Tax = reviewed.Tax;
        bill.Discount = reviewed.Discount;
        bill.TotalAmount = reviewed.TotalAmount;
        reviewed.Status = "Settled";
        reviewed.SettledAtUtc = bill.SettledAtUtc;
        reviewed.PaymentMethod = bill.PaymentMethod;
        reviewed.CanSettle = false;
        reviewed.RestaurantName = await db.Restaurants.Where(r=>r.Id==restaurantId).Select(r=>r.Name).SingleAsync();
        reviewed.SettledByEmployeeId = actorId;
        bill.ReceiptJson = JsonSerializer.Serialize(reviewed);
        // Complete bill requests only; never mark a kitchen order served here.
        var requests = await OutstandingBillRequestsAsync(db,bill);
        foreach (var sr in requests) { sr.Status = "Completed"; sr.UpdatedAt = bill.SettledAtUtc.Value; }
        await new InventoryService(db).DeductForSettlementAsync(db, restaurantId, billId, actorId, request.OperationId);
        await db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        if (hub != null && (tx != null || !db.Database.IsRelational()))
        {
            foreach (var sr in requests)
            {
                var dto = new ServiceRequestDto { Id = sr.Id, RestaurantId = sr.RestaurantId, TableId = sr.TableId,
                    TableName = $"Table {bill.TableNumberSnapshot}", Type = sr.Type, Status = sr.Status,
                    CreatedAt = sr.CreatedAt, UpdatedAt = sr.UpdatedAt };
                try { await hub.Clients.Group($"restaurant_{restaurantId}").SendAsync("ServiceRequestUpdated", dto); }
                catch (Exception ex) { logger?.LogWarning(ex, "Settlement committed but request notification failed for {RequestId}", sr.Id); }
            }
        }
        return reviewed;
    }

    internal static async Task<List<ServiceRequest>> OutstandingBillRequestsAsync(ApplicationDbContext db, TableBill settling)
    {
        // The current settlement has not been saved yet. Ignore it explicitly and
        // retain the visit's request until every other payable bill is closed.
        if (await db.TableBills.AnyAsync(b => b.RestaurantId == settling.RestaurantId
            && b.TableId == settling.TableId && b.DiningGroupId == settling.DiningGroupId
            && b.Id != settling.Id && b.SettledAtUtc == null && b.SplitAtUtc == null)) return [];
        return await db.ServiceRequests.Where(s => s.RestaurantId == settling.RestaurantId
            && s.TableId == settling.TableId && s.DiningGroupId == settling.DiningGroupId
            && s.Type == "RequestBill" && (s.Status == "Pending" || s.Status == "Acknowledged")).ToListAsync();
    }

    public async Task<BillDto> ViewAsync(TableBill bill)
    {
        var view=Project(bill);
        view.Payments=await db.BillPayments.AsNoTracking().Where(p=>p.RestaurantId==bill.RestaurantId && p.TableBillId==bill.Id).OrderBy(p=>p.CollectedAtUtc).Select(p=>new BillPaymentDto(p.Id,p.Method,p.Amount,p.CollectedAtUtc,p.ActorEmployeeId)).ToListAsync();
        view.PaidAmount=view.Payments.Sum(p=>p.Amount);view.RemainingAmount=view.TotalAmount-view.PaidAmount;
        return view;
    }

    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Amount(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);

    internal static BillDto Project(TableBill bill)
    {
        if(bill.AllocationJson!=null)
        {
            var allocation=JsonSerializer.Deserialize<BillDto>(bill.AllocationJson)!;
            allocation.Id=bill.Id;allocation.TableNumber=bill.TableNumberSnapshot;allocation.DiningGroupId=bill.DiningGroupId;allocation.GroupLabel=bill.GroupLabelSnapshot;allocation.ParentBillId=bill.ParentBillId;
            allocation.Status=bill.SettledAtUtc==null?"Open":"Settled";allocation.SettledAtUtc=bill.SettledAtUtc;allocation.PaymentMethod=bill.PaymentMethod;
            return allocation;
        }
        var orders = bill.Orders.OrderBy(o => o.CreatedAt).ThenBy(o => o.Id).ToList();
        var chargeable = orders.Where(Chargeable).ToList();
        bool isParcel = bill.OrderChannel == "ParcelTakeaway" || bill.OrderChannel == "CarParking" || bill.Orders.Any(o => o.OrderChannel == "ParcelTakeaway" || o.OrderChannel == "CarParking" || o.IsScheduled);
        bool hasUnresolvedHeldFood = chargeable.Any(o => o.OrderItems.Any(i => !i.IsCancelled && (i.Quantity - i.CancelledQuantity) > 0 && i.ReleaseState == "Held"));
        bool canSettle = orders.Count > 0 && !hasUnresolvedHeldFood && (isParcel 
            ? chargeable.All(o => o.Status != "Cancelled" && o.Status != "Rejected")
            : chargeable.All(o => o.Status == "Served"));

        return new BillDto
        {
            Id = bill.Id, DiningGroupId=bill.DiningGroupId, GroupLabel=bill.GroupLabelSnapshot, ParentBillId=bill.ParentBillId, TableNumber = bill.TableNumberSnapshot, CreatedAtUtc = bill.CreatedAtUtc,
            Status = bill.SettledAtUtc == null ? "Open" : "Settled", SettledAtUtc = bill.SettledAtUtc, PaymentMethod = bill.PaymentMethod,
            OrderChannel = bill.OrderChannel ?? "DineIn",
            DailyTokenNumber = bill.DailyTokenNumber,
            BusinessDate = bill.BusinessDate,
            PackagingCharge = bill.PackagingCharge,
            LeftoverPackingCharge = bill.LeftoverPackingCharge,
            VehiclePlate = bill.VehiclePlate,
            ParkingBay = bill.ParkingBay,
            PackagingChargeModeSnapshot = bill.PackagingChargeModeSnapshot,
            PackagingRateSnapshot = bill.PackagingRateSnapshot,
            PackagingWaiverReason = bill.PackagingWaiverReason,
            PackagingWaivedBy = bill.PackagingWaivedBy,
            IsPackagingWaived = bill.IsPackagingWaived,
            // PostgreSQL stores microseconds/numeric scale; canonicalize so tracked and reloaded values agree.
            Revision = Hash(JsonSerializer.Serialize(orders.Select(o => new { o.Id, o.Status, UpdatedAtMicroseconds = o.UpdatedAt.Ticks / 10,
                Subtotal = Amount(o.Subtotal), PackagingCharge = Amount(o.PackagingCharge), Tax = Amount(o.Tax), Discount = Amount(o.Discount), Total = Amount(o.TotalAmount),
                Items = o.OrderItems.OrderBy(i => i.Id).Select(i => new { i.Id, i.Quantity, UnitPrice = Amount(i.UnitPrice), i.ItemNameSnapshot, i.ReleaseState, i.CancelledQuantity, i.IsCancelled, i.PackingIntent }) }))),
            Subtotal = chargeable.Sum(o => o.Subtotal), 
            Tax = chargeable.Sum(o => o.Tax), 
            Discount = chargeable.Sum(o => o.Discount),
            TotalAmount = chargeable.Sum(o => o.TotalAmount) + (bill.LeftoverPackingCharge), 
            CanSettle = canSettle,
            Orders = orders.Select(o => new BillOrderDto { Id = o.Id, OrderNumber = o.OrderNumber, Status = o.Status, TotalAmount = o.TotalAmount,
                Items = o.OrderItems.OrderBy(i => i.Id).Select(i => new OrderItemDto {
                    Id = i.Id,
                    MenuItemId = i.MenuItemId,
                    Name = i.ItemNameSnapshot,
                    ItemNameSnapshot = i.ItemNameSnapshot,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    SpecialInstructions = i.SpecialInstructions,
                    ReleaseState = i.ReleaseState,
                    CourseName = i.CourseName,
                    FiredAtUtc = i.FiredAtUtc,
                    ModifiersJson = i.ModifiersJson,
                    IsRefill = i.IsRefill,
                    OriginalOrderItemId = i.OriginalOrderItemId,
                    RefillCount = i.RefillCount,
                    CancelledQuantity = i.CancelledQuantity,
                    IsCancelled = i.IsCancelled
                }).ToList() }).ToList()
        };
    }
}
