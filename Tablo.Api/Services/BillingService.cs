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
            e.Id == actorId && e.RestaurantId == restaurantId && e.IsActive && (e.Role.Name == "Admin" || e.Role.Name == "Waiter")))
            throw new UnauthorizedAccessException("An active restaurant admin or waiter account is required.");
    }

    internal static async Task<TableBill> AttachAsync(ApplicationDbContext db, RestaurantTable table, Order order)
    {
        if (order.TableBillId.HasValue)
        {
            var attached = await db.TableBills.SingleAsync(b => b.Id == order.TableBillId && b.RestaurantId == table.RestaurantId && b.TableId == table.Id);
            if (attached.SettledAtUtc.HasValue) throw new ServiceConflictException("A settled order cannot accept more items.");
            return attached;
        }
        var bill = db.TableBills.Local.FirstOrDefault(b => b.TableId == table.Id && b.RestaurantId == table.RestaurantId && b.SettledAtUtc == null)
            ?? await db.TableBills.SingleOrDefaultAsync(b => b.TableId == table.Id && b.RestaurantId == table.RestaurantId && b.SettledAtUtc == null);
        if (bill == null)
        {
            bill = new TableBill { RestaurantId = table.RestaurantId, TableId = table.Id, TableNumberSnapshot = table.TableNumber };
            db.TableBills.Add(bill);
        }
        order.TableBill = bill;
        order.TableBillId = bill.Id;
        return bill;
    }

    public async Task<TableBillReviewDto> ReviewAsync(Guid restaurantId, Guid tableId)
    {
        var table = await db.RestaurantTables.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tableId && t.RestaurantId == restaurantId)
            ?? throw new KeyNotFoundException("Table not found.");
        var open = await db.TableBills.AsNoTracking().Include(b => b.Orders).ThenInclude(o => o.OrderItems)
            .SingleOrDefaultAsync(b => b.RestaurantId == restaurantId && b.TableId == tableId && b.SettledAtUtc == null);
        var receipts = await db.TableBills.AsNoTracking().Where(b => b.RestaurantId == restaurantId && b.TableId == tableId && b.SettledAtUtc != null)
            .OrderByDescending(b => b.SettledAtUtc).Take(10).ToListAsync();
        return new TableBillReviewDto
        {
            TableId = tableId, TableNumber = table.TableNumber, OpenBill = open == null ? null : Project(open),
            UnbilledActiveOrderCount = await db.Orders.CountAsync(o => o.RestaurantId == restaurantId && o.TableId == tableId && o.TableBillId == null
                && o.Status != "Served" && o.Status != "Cancelled" && o.Status != "Rejected"),
            RecentSettlements = receipts.Select(b => JsonSerializer.Deserialize<BillDto>(b.ReceiptJson!)!).ToList()
        };
    }

    public async Task<BillDto> OpenAsync(Guid restaurantId, Guid tableId, Guid actorId)
    {
        await RequireActorAsync(db, restaurantId, actorId);
        await using var tx = await TableOrderTransaction.BeginAsync(db);
        await TableOrderTransaction.LockAsync(db, restaurantId, tableId);
        var table = await db.RestaurantTables.SingleOrDefaultAsync(t => t.Id == tableId && t.RestaurantId == restaurantId)
            ?? throw new KeyNotFoundException("Table not found.");
        var active = await db.Orders.Where(o => o.RestaurantId == restaurantId && o.TableId == tableId && o.TableBillId == null
            && o.Status != "Served" && o.Status != "Cancelled" && o.Status != "Rejected").ToListAsync();
        foreach (var order in active) await AttachAsync(db, table, order);
        await db.SaveChangesAsync();
        var bill = await db.TableBills.Include(b => b.Orders).ThenInclude(o => o.OrderItems)
            .SingleOrDefaultAsync(b => b.RestaurantId == restaurantId && b.TableId == tableId && b.SettledAtUtc == null)
            ?? throw new ServiceConflictException("There are no active orders to start a bill for.");
        if (tx != null) await tx.CommitAsync();
        return Project(bill);
    }

    public async Task<BillDto> SettleAsync(Guid restaurantId, Guid billId, Guid actorId, SettleBillRequestDto request)
    {
        var tableId = await db.TableBills.Where(b => b.Id == billId && b.RestaurantId == restaurantId)
            .Select(b => (Guid?)b.TableId).SingleOrDefaultAsync() ?? throw new KeyNotFoundException("Bill not found.");
        await RequireActorAsync(db, restaurantId, actorId);
        if (request.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(request.ExpectedRevision) || request.PaymentMethod is not ("Cash" or "UPI" or "Card"))
            throw new ArgumentException("A valid operation ID, reviewed bill and payment method are required.");
        await using var tx = await TableOrderTransaction.BeginAsync(db);
        await TableOrderTransaction.LockAsync(db, restaurantId, tableId);
        var bill = await db.TableBills.Include(b => b.Orders).ThenInclude(o => o.OrderItems)
            .SingleAsync(b => b.Id == billId && b.RestaurantId == restaurantId);
        var hash = Hash(JsonSerializer.Serialize(new { billId, actorId, request.ExpectedRevision, ExpectedTotal = Amount(request.ExpectedTotal), request.PaymentMethod }));
        if (bill.SettledAtUtc.HasValue)
        {
            if (bill.SettlementOperationId != request.OperationId || bill.SettlementHash != hash)
                throw new ServiceConflictException("This bill is already settled. Refresh its receipt.");
            return JsonSerializer.Deserialize<BillDto>(bill.ReceiptJson!)!;
        }
        if (await db.TableBills.AnyAsync(b => b.RestaurantId == restaurantId && b.SettlementOperationId == request.OperationId))
            throw new ServiceConflictException("This operation ID has already been used for another bill.");
        // Do not settle an incomplete legacy attachment or food still being prepared.
        if (await db.Orders.AnyAsync(o => o.RestaurantId == restaurantId && o.TableId == tableId && o.TableBillId == null
            && o.Status != "Served" && o.Status != "Cancelled" && o.Status != "Rejected"))
            throw new ServiceConflictException("Attach the table's active orders before settling.");
        var reviewed = Project(bill);
        if (!reviewed.CanSettle) throw new ServiceConflictException("All chargeable orders must be served before settlement.");
        if (reviewed.Revision != request.ExpectedRevision || reviewed.TotalAmount != request.ExpectedTotal)
            throw new ServiceConflictException("The bill changed. Review the latest items and total before settling.");
        bill.SettledAtUtc = DateTime.UtcNow;
        bill.SettledByEmployeeId = actorId;
        bill.SettlementOperationId = request.OperationId;
        bill.SettlementHash = hash;
        bill.PaymentMethod = request.PaymentMethod;
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
        var requests = await db.ServiceRequests.Where(s => s.RestaurantId == restaurantId && s.TableId == tableId
            && s.Type == "RequestBill" && (s.Status == "Pending" || s.Status == "Acknowledged")).ToListAsync();
        foreach (var sr in requests) { sr.Status = "Completed"; sr.UpdatedAt = bill.SettledAtUtc.Value; }
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

    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Amount(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);

    internal static BillDto Project(TableBill bill)
    {
        var orders = bill.Orders.OrderBy(o => o.CreatedAt).ThenBy(o => o.Id).ToList();
        var chargeable = orders.Where(Chargeable).ToList();
        return new BillDto
        {
            Id = bill.Id, TableNumber = bill.TableNumberSnapshot, CreatedAtUtc = bill.CreatedAtUtc,
            Status = bill.SettledAtUtc == null ? "Open" : "Settled", SettledAtUtc = bill.SettledAtUtc, PaymentMethod = bill.PaymentMethod,
            // PostgreSQL stores microseconds/numeric scale; canonicalize so tracked and reloaded values agree.
            Revision = Hash(JsonSerializer.Serialize(orders.Select(o => new { o.Id, o.Status, UpdatedAtMicroseconds = o.UpdatedAt.Ticks / 10,
                Subtotal = Amount(o.Subtotal), Tax = Amount(o.Tax), Discount = Amount(o.Discount), Total = Amount(o.TotalAmount),
                Items = o.OrderItems.OrderBy(i => i.Id).Select(i => new { i.Id, i.Quantity, UnitPrice = Amount(i.UnitPrice), i.ItemNameSnapshot }) }))),
            Subtotal = chargeable.Sum(o => o.Subtotal), Tax = chargeable.Sum(o => o.Tax), Discount = chargeable.Sum(o => o.Discount),
            TotalAmount = chargeable.Sum(o => o.TotalAmount), CanSettle = orders.Count > 0 && chargeable.All(o => o.Status == "Served"),
            Orders = orders.Select(o => new BillOrderDto { Id = o.Id, OrderNumber = o.OrderNumber, Status = o.Status, TotalAmount = o.TotalAmount,
                Items = o.OrderItems.OrderBy(i => i.Id).Select(i => new OrderItemDto { Id = i.Id, MenuItemId = i.MenuItemId,
                    Name = i.ItemNameSnapshot, ItemNameSnapshot = i.ItemNameSnapshot, Quantity = i.Quantity, UnitPrice = i.UnitPrice }).ToList() }).ToList()
        };
    }
}
