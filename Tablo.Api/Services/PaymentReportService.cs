using System.Data;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;

namespace Tablo.Api.Services;

public class PaymentReportService(ApplicationDbContext db)
{
    public const int ExportLimit = 10000;
    private static readonly TimeZoneInfo India = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    private IQueryable<TableBill> Query(Guid tenant, PaymentReportFilter filter)
    {
        if (tenant == Guid.Empty) throw new ArgumentException("Restaurant is required.");
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || (long)(filter.Page-1)*filter.PageSize > int.MaxValue)
            throw new ArgumentException("Use a valid page and a page size between 1 and 100.");
        if (filter.StartDate > filter.EndDate) throw new ArgumentException("Start date cannot be after end date.");
        if (filter.EndDate == DateOnly.MaxValue) throw new ArgumentException("End date is outside the supported range.");
        if (!string.IsNullOrEmpty(filter.PaymentMethod) && filter.PaymentMethod is not ("Cash" or "UPI" or "Card"))
            throw new ArgumentException("Payment method must be Cash, UPI or Card.");
        var query = db.TableBills.AsNoTracking().Where(b=>b.RestaurantId==tenant && b.SettledAtUtc!=null);
        if (filter.StartDate is {} start)
        {
            var utc = TimeZoneInfo.ConvertTimeToUtc(start.ToDateTime(TimeOnly.MinValue), India);
            query = query.Where(b=>b.SettledAtUtc>=utc);
        }
        if (filter.EndDate is {} end)
        {
            var utc = TimeZoneInfo.ConvertTimeToUtc(end.AddDays(1).ToDateTime(TimeOnly.MinValue), India);
            query = query.Where(b=>b.SettledAtUtc<utc);
        }
        if (filter.TableId is {} table) query=query.Where(b=>b.TableId==table);
        if (!string.IsNullOrEmpty(filter.PaymentMethod)) query=query.Where(b=>b.PaymentMethod==filter.PaymentMethod);
        return query;
    }

    private static IQueryable<PaymentReportRow> Rows(IQueryable<TableBill> query) => query
        .OrderByDescending(b=>b.SettledAtUtc).ThenByDescending(b=>b.Id)
        .Select(b=>new PaymentReportRow { Id=b.Id, TableId=b.TableId, TableNumber=b.TableNumberSnapshot,
            SettledAtUtc=b.SettledAtUtc!.Value, SettledByEmployeeId=b.SettledByEmployeeId,
            PaymentMethod=b.PaymentMethod, TotalAmount=b.TotalAmount });

    public async Task<PaymentReportDto> GetAsync(Guid tenant, PaymentReportFilter filter)
    {
        var query=Query(tenant,filter);
        // Count, method totals and this page must describe one database snapshot.
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction==null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead) : null;
        var totals=await query.GroupBy(b=>b.PaymentMethod).Select(g=>new { Method=g.Key, Count=g.Count(), Amount=g.Sum(b=>b.TotalAmount) }).ToListAsync();
        var count=totals.Sum(t=>t.Count);
        var result=new PaymentReportDto { Page=filter.Page, PageSize=filter.PageSize, TotalCount=count,
            TotalPages=(int)Math.Ceiling((double)count/filter.PageSize), TotalCollected=totals.Sum(t=>t.Amount),
            Cash=totals.Where(t=>t.Method=="Cash").Sum(t=>t.Amount), UPI=totals.Where(t=>t.Method=="UPI").Sum(t=>t.Amount),
            Card=totals.Where(t=>t.Method=="Card").Sum(t=>t.Amount),
            Items=await Rows(query).Skip((filter.Page-1)*filter.PageSize).Take(filter.PageSize).ToListAsync() };
        if(tx!=null) await tx.CommitAsync();
        return result;
    }

    public async Task<byte[]> ExportAsync(Guid tenant, PaymentReportFilter filter)
    {
        var rows=await Rows(Query(tenant,filter)).Take(ExportLimit+1).ToListAsync();
        if(rows.Count>ExportLimit) throw new ArgumentException($"Export supports up to {ExportLimit:N0} bills. Narrow the date or table filters and retry.");
        using var workbook=new XLWorkbook();
        var sheet=workbook.Worksheets.Add("Payments");
        var headers=new[] { "Receipt ID", "Table (snapshot)", "Settled at (Asia/Kolkata)", "Recorded employee ID", "Method", "Collected (INR)" };
        for(var c=0;c<headers.Length;c++) sheet.Cell(1,c+1).Value=headers[c];
        for(var i=0;i<rows.Count;i++)
        {
            var b=rows[i]; var r=i+2;
            sheet.Cell(r,1).Value=b.Id.ToString(); sheet.Cell(r,2).Value=b.TableNumber;
            sheet.Cell(r,3).Value=TimeZoneInfo.ConvertTimeFromUtc(b.SettledAtUtc,India).ToString("yyyy-MM-dd HH:mm:ss");
            sheet.Cell(r,4).Value=b.SettledByEmployeeId?.ToString()??"Unavailable";
            sheet.Cell(r,5).Value=b.PaymentMethod??"Unavailable"; sheet.Cell(r,6).Value=b.TotalAmount;
        }
        sheet.Row(1).Style.Font.Bold=true; sheet.SheetView.FreezeRows(1);
        sheet.Column(6).Style.NumberFormat.Format="#,##0.00"; sheet.Columns().AdjustToContents();
        using var stream=new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
    }
}
