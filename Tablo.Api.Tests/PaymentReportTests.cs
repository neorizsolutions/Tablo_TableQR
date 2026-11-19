using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Services;

namespace Tablo.Api.Tests;

public class PaymentReportTests : IDisposable
{
    private readonly ApplicationDbContext db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Guid tenant = Guid.NewGuid();
    private readonly Guid table = Guid.NewGuid();
    private TableBill Add(decimal total, string method, string? at, Guid? restaurant = null)
    {
        var b = new TableBill { RestaurantId = restaurant ?? tenant, TableId = table,
            TableNumberSnapshot = 4, TotalAmount = total, PaymentMethod = method,
            SettledAtUtc = at == null ? null : DateTime.Parse(at).ToUniversalTime() };
        b.ReceiptJson = JsonSerializer.Serialize(new BillDto { Id = b.Id, Status = "Settled", TotalAmount = total,
            TableNumber = 4, SettledAtUtc = b.SettledAtUtc, PaymentMethod = method });
        db.TableBills.Add(b); db.SaveChanges(); return b;
    }

    [Fact]
    public async Task SummaryAndHistory_UseSettlementDayInIndiaAndExcludeOpenAndForeignBills()
    {
        Add(100, "Cash", "2026-11-02T18:30:00Z");
        Add(20, "UPI", "2026-11-03T18:29:59Z");
        Add(0, "Card", "2026-11-03T10:00:00Z");
        Add(999, "Cash", "2026-11-02T18:29:59Z");
        Add(999, "Cash", "2026-11-03T18:30:00Z");
        Add(999, "Cash", null);
        Add(999, "Cash", "2026-11-03T10:00:00Z", Guid.NewGuid());
        var report = await new PaymentReportService(db).GetAsync(tenant, new PaymentReportFilter {
            StartDate = new(2026,11,3), EndDate = new(2026,11,3), PageSize = 2 });
        Assert.Equal(120, report.TotalCollected);
        Assert.Equal(100, report.Cash); Assert.Equal(20, report.UPI); Assert.Equal(0, report.Card);
        Assert.Equal(3, report.TotalCount); Assert.Equal(2, report.Items.Count); Assert.Equal(2, report.TotalPages);
        Assert.True(report.Items[0].SettledAtUtc >= report.Items[1].SettledAtUtc);
    }

    [Fact]
    public async Task FiltersAndExport_ReconcileAndKeepStablePagination()
    {
        for(var i=0;i<3;i++) Add(10+i, "UPI", "2026-11-03T10:00:00Z");
        Add(50,"Cash","2026-11-03T10:00:00Z");
        var service = new PaymentReportService(db);
        var filter = new PaymentReportFilter { TableId=table, PaymentMethod="UPI", PageSize=2 };
        var first = await service.GetAsync(tenant, filter);
        filter.Page=2;
        var second = await service.GetAsync(tenant, filter);
        Assert.Equal(33, first.TotalCollected); Assert.Equal(33, second.TotalCollected);
        Assert.Empty(first.Items.Select(x=>x.Id).Intersect(second.Items.Select(x=>x.Id)));
        using var stream = new MemoryStream(await service.ExportAsync(tenant, filter));
        using var workbook = new XLWorkbook(stream);
        Assert.Equal(4, workbook.Worksheet("Payments").LastRowUsed()!.RowNumber());
        Assert.Equal(33, workbook.Worksheet("Payments").RowsUsed().Skip(1).Sum(r => r.Cell(6).GetValue<decimal>()));
        filter.TableId=Guid.NewGuid();
        Assert.Equal(0, (await service.GetAsync(tenant, filter)).TotalCount);
    }

    [Fact]
    public async Task RejectsInvalidFiltersAndOversizedExport()
    {
        var service = new PaymentReportService(db);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.GetAsync(tenant,new() { StartDate=new(2026,11,4), EndDate=new(2026,11,3) }));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.GetAsync(tenant,new() { PaymentMethod="Crypto" }));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.GetAsync(Guid.Empty,new()));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.GetAsync(tenant,new() { Page=0 }));
        db.TableBills.AddRange(Enumerable.Range(0, PaymentReportService.ExportLimit+1).Select(_=>new TableBill {
            RestaurantId=tenant, TableId=table, SettledAtUtc=DateTime.UtcNow, PaymentMethod="Cash" }));
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(()=>service.ExportAsync(tenant,new()));
    }

    [Fact]
    public async Task ReceiptLookup_IsTenantScopedAndUsesSnapshotRatherThanCurrentBillValues()
    {
        var bill=Add(42,"Card","2026-11-03T10:00:00Z");
        bill.TotalAmount=999; await db.SaveChangesAsync();
        var service=new BillingService(db);
        Assert.Equal(42,(await service.ReceiptAsync(tenant,bill.Id)).TotalAmount);
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>service.ReceiptAsync(Guid.NewGuid(),bill.Id));
        var open=Add(1,"Cash",null);
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>service.ReceiptAsync(tenant,open.Id));
    }

    [Fact]
    public async Task TopItemAmounts_ArePreTaxWithoutInventedFivePercent()
    {
        db.Orders.Add(new Order { RestaurantId=tenant, TableId=table, Status="Served", TotalAmount=3,
            OrderItems=[new OrderItem { ItemNameSnapshot="Coffee", Quantity=1, UnitPrice=3 }] });
        await db.SaveChangesAsync();
        Assert.Equal(3,(await new ReportService(db).GetTopItemsAsync(tenant,null,null,null,null)).Single().Revenue);
    }
    public void Dispose()=>db.Dispose();

    [Fact]
    public async Task LedgerCollections_IncludePartialPaymentsAndNeverDoubleCountFinalSettlement()
    {
        var bill=Add(105,"Mixed","2026-11-04T10:00:00Z");
        db.BillPayments.AddRange(new BillPayment{RestaurantId=tenant,TableBillId=bill.Id,TableIdSnapshot=table,TableNumberSnapshot=4,Method="Cash",Amount=40,CollectedAtUtc=DateTime.Parse("2026-11-03T10:00:00Z").ToUniversalTime()},
            new BillPayment{RestaurantId=tenant,TableBillId=bill.Id,TableIdSnapshot=table,TableNumberSnapshot=4,Method="UPI",Amount=65,CollectedAtUtc=DateTime.Parse("2026-11-04T10:00:00Z").ToUniversalTime()});
        await db.SaveChangesAsync();var service=new PaymentReportService(db);
        var first=await service.GetAsync(tenant,new(){StartDate=new(2026,11,3),EndDate=new(2026,11,3)});
        Assert.Equal(40,first.TotalCollected);Assert.Equal(40,first.Cash);Assert.Equal(0,first.UPI);
        var all=await service.GetAsync(tenant,new());Assert.Equal(105,all.TotalCollected);Assert.Equal(2,all.TotalCount);Assert.Equal(1,all.SettledBillCount);
        Assert.All(all.Items,p=>Assert.Equal(bill.Id,p.BillId));
    }
}
