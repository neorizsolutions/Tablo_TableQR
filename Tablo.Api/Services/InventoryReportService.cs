using System.Data;
using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;

namespace Tablo.Api.Services;

public class InventoryReportService(ApplicationDbContext db)
{
    private static readonly TimeZoneInfo India = GetIndiaTimeZone();

    private static TimeZoneInfo GetIndiaTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
            catch { return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "India Standard Time", "IST"); }
        }
    }

    public static DateTime ToIst(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, India);

    public static DateTime IstStartToUtc(DateOnly date)
    {
        var istDateTime = date.ToDateTime(TimeOnly.MinValue);
        return TimeZoneInfo.ConvertTimeToUtc(istDateTime, India);
    }

    public static DateTime IstEndToUtc(DateOnly date)
    {
        var istDateTime = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return TimeZoneInfo.ConvertTimeToUtc(istDateTime, India);
    }

    public async Task<StockReportDto> GetStockReportAsync(Guid restaurantId, StockReportFilter filter)
    {
        var todayIst = DateOnly.FromDateTime(ToIst(DateTime.UtcNow));
        var startDate = filter.StartDate ?? todayIst;
        var endDate = filter.EndDate ?? todayIst;

        if (startDate > endDate)
            throw new ArgumentException("Start date cannot be after end date.");

        var startUtc = IstStartToUtc(startDate);
        var endUtc = IstEndToUtc(endDate);

        var ingredientsQuery = db.Ingredients.AsNoTracking()
            .Where(i => i.RestaurantId == restaurantId);

        if (filter.IngredientId.HasValue && filter.IngredientId.Value != Guid.Empty)
            ingredientsQuery = ingredientsQuery.Where(i => i.Id == filter.IngredientId.Value);

        var ingredients = await ingredientsQuery.OrderBy(i => i.Name).ToListAsync();

        var allMovements = await db.StockMovements.AsNoTracking()
            .Include(m => m.ActorEmployee)
            .Where(m => m.RestaurantId == restaurantId && m.TimestampUtc < endUtc)
            .OrderBy(m => m.TimestampUtc)
            .ToListAsync();

        var rows = new List<StockReportRowDto>();

        foreach (var ing in ingredients)
        {
            var ingMovements = allMovements.Where(m => m.IngredientId == ing.Id).ToList();

            // Opening stock as of startUtc
            var openingStock = ingMovements
                .Where(m => m.TimestampUtc < startUtc)
                .Sum(m => m.QuantityChange);

            // Period movements
            var periodMovements = ingMovements
                .Where(m => m.TimestampUtc >= startUtc && m.TimestampUtc < endUtc)
                .ToList();

            if (!string.IsNullOrEmpty(filter.Reason))
                periodMovements = periodMovements.Where(m => m.Reason == filter.Reason).ToList();

            var receipts = periodMovements
                .Where(m => m.Reason is "Restock" or "OpeningStock" or "Purchase" or "PreparationOutput" or "TransferIn")
                .Sum(m => m.QuantityChange);

            var sales = periodMovements
                .Where(m => m.Reason is "OrderDeduction")
                .Sum(m => m.QuantityChange); // negative

            var waste = periodMovements
                .Where(m => m.Reason is "Waste" or "WriteOff" or "PurchaseReturn" or "PreparationInput" or "TransferOut" or "TransferDiscrepancyLoss")
                .Sum(m => m.QuantityChange); // negative

            var adjustments = periodMovements
                .Where(m => m.Reason is "StocktakeAdjustment" or "ManualCorrection")
                .Sum(m => m.QuantityChange);

            var closingStock = openingStock + receipts + sales + waste + adjustments;
            var isLow = ing.LowStockThreshold.HasValue && closingStock <= ing.LowStockThreshold.Value;

            if (filter.LowStockOnly == true && !isLow)
                continue;

            var unitCost = ing.CurrentAverageCost;
            var closingValue = closingStock > 0 ? Math.Round(closingStock * unitCost, 2) : 0m;

            var movementDtos = periodMovements.Select(m => new StockReportMovementDto(
                m.Id,
                m.TimestampUtc,
                ToIst(m.TimestampUtc).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                m.Reason,
                m.QuantityChange,
                m.UnitSnapshot,
                m.UnitCostSnapshot,
                m.TotalCost,
                m.ReferenceType,
                m.ReferenceId,
                m.ActorEmployee != null ? $"{m.ActorEmployee.FirstName} {m.ActorEmployee.LastName}" : "System",
                m.Notes
            )).ToList();

            rows.Add(new StockReportRowDto(
                ing.Id,
                ing.Name,
                ing.BaseUnit,
                ing.LowStockThreshold,
                isLow,
                openingStock,
                receipts,
                sales,
                waste,
                adjustments,
                closingStock,
                unitCost,
                closingValue,
                movementDtos
            ));
        }

        var totalClosingValue = rows.Sum(r => r.ClosingStockValue);
        var lowStockCount = rows.Count(r => r.IsLowStock);

        return new StockReportDto(
            startDate,
            endDate,
            totalClosingValue,
            rows.Count,
            lowStockCount,
            rows
        );
    }

    public async Task<byte[]> ExportStockReportAsync(Guid restaurantId, StockReportFilter filter)
    {
        var report = await GetStockReportAsync(restaurantId, filter);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Stock Report");

        // Header info
        sheet.Cell(1, 1).Value = "TABLO Stock Valuation & Movement Report";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        sheet.Cell(2, 1).Value = $"Reporting Period (IST): {report.StartDate:yyyy-MM-dd} to {report.EndDate:yyyy-MM-dd}";
        sheet.Cell(2, 1).Style.Font.Italic = true;

        sheet.Cell(3, 1).Value = $"Total Closing Stock Value: INR {report.TotalClosingStockValue:N2}";
        sheet.Cell(3, 1).Style.Font.Bold = true;

        var startRow = 5;
        var headers = new[]
        {
            "Ingredient",
            "Base Unit",
            "Opening Stock",
            "Receipts / Purchases",
            "Sales Deductions",
            "Waste",
            "Adjustments",
            "Closing Stock",
            "Unit Cost (INR)",
            "Closing Value (INR)",
            "Low Stock Status"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = sheet.Cell(startRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(240, 240, 240);
        }

        var r = startRow + 1;
        foreach (var row in report.Rows)
        {
            sheet.Cell(r, 1).Value = row.IngredientName;
            sheet.Cell(r, 2).Value = row.BaseUnit;
            sheet.Cell(r, 3).Value = row.OpeningQuantity;
            sheet.Cell(r, 4).Value = row.ReceiptsQuantity;
            sheet.Cell(r, 5).Value = row.SalesQuantity;
            sheet.Cell(r, 6).Value = row.WasteQuantity;
            sheet.Cell(r, 7).Value = row.AdjustmentsQuantity;
            sheet.Cell(r, 8).Value = row.ClosingQuantity;
            sheet.Cell(r, 9).Value = row.CostPerBaseUnit;
            sheet.Cell(r, 10).Value = row.ClosingStockValue;
            sheet.Cell(r, 11).Value = row.IsLowStock ? "LOW STOCK" : "OK";

            if (row.IsLowStock)
            {
                sheet.Cell(r, 11).Style.Font.FontColor = XLColor.Red;
                sheet.Cell(r, 11).Style.Font.Bold = true;
            }

            sheet.Cell(r, 3).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(r, 4).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(r, 5).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(r, 6).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(r, 7).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(r, 8).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(r, 9).Style.NumberFormat.Format = "#,##0.0000";
            sheet.Cell(r, 10).Style.NumberFormat.Format = "#,##0.00";

            r++;
        }

        // Total row
        sheet.Cell(r, 1).Value = "Total";
        sheet.Cell(r, 1).Style.Font.Bold = true;
        sheet.Cell(r, 10).Value = report.TotalClosingStockValue;
        sheet.Cell(r, 10).Style.Font.Bold = true;
        sheet.Cell(r, 10).Style.NumberFormat.Format = "#,##0.00";

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(startRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<SalesGrossProfitDto> GetSalesGrossProfitReportAsync(Guid restaurantId, SalesGrossProfitFilter filter)
    {
        var todayIst = DateOnly.FromDateTime(ToIst(DateTime.UtcNow));
        var startDate = filter.StartDate ?? todayIst;
        var endDate = filter.EndDate ?? todayIst;

        if (startDate > endDate)
            throw new ArgumentException("Start date cannot be after end date.");

        var startUtc = IstStartToUtc(startDate);
        var endUtc = IstEndToUtc(endDate);

        var query = db.SettledBillItemCosts.AsNoTracking()
            .Include(c => c.IngredientCosts)
            .Include(c => c.TableBill)
            .Where(c => c.RestaurantId == restaurantId && c.SettledAtUtc >= startUtc && c.SettledAtUtc < endUtc);

        if (filter.MenuItemId.HasValue && filter.MenuItemId.Value != Guid.Empty)
            query = query.Where(c => c.MenuItemId == filter.MenuItemId.Value);

        var settledItems = await query.OrderBy(c => c.SettledAtUtc).ToListAsync();

        var groupedByMenu = settledItems.GroupBy(c => c.MenuItemId).ToList();
        var rows = new List<SalesGrossProfitRowDto>();
        var incompleteItems = new List<IncompleteCostingItemDto>();

        foreach (var group in groupedByMenu)
        {
            var first = group.First();
            var totalQty = group.Sum(c => c.Quantity);
            var grossSales = group.Sum(c => c.GrossRevenue);
            var discount = group.Sum(c => c.AllocatedDiscount);
            var netSales = group.Sum(c => c.NetRevenue);
            var tax = group.Sum(c => c.AllocatedTax);
            var cogs = group.Sum(c => c.TotalIngredientCost);
            var grossProfit = netSales - cogs;
            var marginPercent = netSales > 0 ? Math.Round((grossProfit / netSales) * 100m, 2) : 0m;
            var isComplete = group.All(c => c.IsCostComplete);
            var incompleteReason = group.FirstOrDefault(c => !c.IsCostComplete)?.IncompleteReason;

            if (!isComplete)
            {
                incompleteItems.Add(new IncompleteCostingItemDto(
                    first.MenuItemId,
                    first.MenuItemNameSnapshot,
                    totalQty,
                    netSales,
                    incompleteReason ?? "Missing ingredient cost basis or recipe"
                ));
            }

            var drillDown = group.Select(item => new SalesGrossProfitDrillDownDto(
                item.TableBillId,
                item.TableBill?.TableNumberSnapshot ?? 0,
                item.TableBill?.GroupLabelSnapshot,
                item.SettledAtUtc,
                ToIst(item.SettledAtUtc).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                item.Quantity,
                item.GrossRevenue,
                item.AllocatedDiscount,
                item.NetRevenue,
                item.AllocatedTax,
                item.TotalIngredientCost,
                item.GrossProfit,
                item.GrossMarginPercent,
                item.IsCostComplete,
                item.IncompleteReason,
                item.IngredientCosts.Select(ic => new BillItemIngredientCostDto(
                    ic.IngredientId,
                    ic.IngredientNameSnapshot,
                    ic.BaseUnitSnapshot,
                    ic.RawQuantityPerPortion,
                    ic.TotalRawQuantity,
                    ic.UnitCostSnapshot,
                    ic.TotalCost
                )).ToList()
            )).ToList();

            rows.Add(new SalesGrossProfitRowDto(
                first.MenuItemId,
                first.MenuItemNameSnapshot,
                first.CategoryNameSnapshot,
                totalQty,
                grossSales,
                discount,
                netSales,
                tax,
                cogs,
                grossProfit,
                marginPercent,
                isComplete,
                incompleteReason,
                drillDown
            ));
        }

        var totalGrossSales = rows.Sum(r => r.GrossSales);
        var totalDiscounts = rows.Sum(r => r.TotalDiscount);
        var totalNetSales = rows.Sum(r => r.NetSalesExcludingTax);
        var totalTax = rows.Sum(r => r.TotalTax);
        var totalCogs = rows.Sum(r => r.TotalCogs);
        var totalGrossProfit = totalNetSales - totalCogs;
        var overallMargin = totalNetSales > 0 ? Math.Round((totalGrossProfit / totalNetSales) * 100m, 2) : 0m;
        var totalBills = settledItems.Select(c => c.TableBillId).Distinct().Count();
        var totalPortions = rows.Sum(r => r.QuantitySold);
        var incompleteSales = incompleteItems.Sum(i => i.NetSales);

        return new SalesGrossProfitDto(
            startDate,
            endDate,
            totalGrossSales,
            totalDiscounts,
            totalNetSales,
            totalTax,
            totalCogs,
            totalGrossProfit,
            overallMargin,
            totalBills,
            totalPortions,
            incompleteItems.Count,
            incompleteSales,
            incompleteItems,
            rows
        );
    }

    public async Task<byte[]> ExportSalesGrossProfitReportAsync(Guid restaurantId, SalesGrossProfitFilter filter)
    {
        var report = await GetSalesGrossProfitReportAsync(restaurantId, filter);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Gross Profit Report");

        sheet.Cell(1, 1).Value = "TABLO Sales & Gross Profit Report";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        sheet.Cell(2, 1).Value = $"Reporting Period (IST): {report.StartDate:yyyy-MM-dd} to {report.EndDate:yyyy-MM-dd}";
        sheet.Cell(2, 1).Style.Font.Italic = true;

        sheet.Cell(3, 1).Value = $"Net Sales (Excl. Tax): INR {report.TotalNetSalesExcludingTax:N2} | Total COGS: INR {report.TotalCogs:N2} | Gross Profit: INR {report.TotalGrossProfit:N2} ({report.OverallGrossMarginPercent:N2}%)";
        sheet.Cell(3, 1).Style.Font.Bold = true;

        var startRow = 5;
        var headers = new[]
        {
            "Menu Item",
            "Category",
            "Portions Sold",
            "Gross Sales (INR)",
            "Discounts (INR)",
            "Net Sales (Excl. Tax)",
            "Tax (INR)",
            "Ingredient COGS (INR)",
            "Gross Profit (INR)",
            "Gross Margin (%)",
            "Costing Status"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = sheet.Cell(startRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(240, 240, 240);
        }

        var r = startRow + 1;
        foreach (var row in report.Rows)
        {
            sheet.Cell(r, 1).Value = row.MenuItemName;
            sheet.Cell(r, 2).Value = row.CategoryName;
            sheet.Cell(r, 3).Value = row.QuantitySold;
            sheet.Cell(r, 4).Value = row.GrossSales;
            sheet.Cell(r, 5).Value = row.TotalDiscount;
            sheet.Cell(r, 6).Value = row.NetSalesExcludingTax;
            sheet.Cell(r, 7).Value = row.TotalTax;
            sheet.Cell(r, 8).Value = row.TotalCogs;
            sheet.Cell(r, 9).Value = row.GrossProfit;
            sheet.Cell(r, 10).Value = row.GrossMarginPercent / 100m;
            sheet.Cell(r, 11).Value = row.IsCostComplete ? "COMPLETE" : $"INCOMPLETE: {row.IncompleteReason}";

            sheet.Cell(r, 4).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(r, 5).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(r, 6).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(r, 7).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(r, 8).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(r, 9).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(r, 10).Style.NumberFormat.Format = "0.00%";

            if (!row.IsCostComplete)
            {
                sheet.Cell(r, 11).Style.Font.FontColor = XLColor.DarkOrange;
            }

            r++;
        }

        // Totals row
        sheet.Cell(r, 1).Value = "Total";
        sheet.Cell(r, 1).Style.Font.Bold = true;
        sheet.Cell(r, 3).Value = report.TotalPortionsSold;
        sheet.Cell(r, 3).Style.Font.Bold = true;
        sheet.Cell(r, 4).Value = report.TotalGrossSales;
        sheet.Cell(r, 4).Style.Font.Bold = true;
        sheet.Cell(r, 4).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(r, 5).Value = report.TotalDiscounts;
        sheet.Cell(r, 5).Style.Font.Bold = true;
        sheet.Cell(r, 5).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(r, 6).Value = report.TotalNetSalesExcludingTax;
        sheet.Cell(r, 6).Style.Font.Bold = true;
        sheet.Cell(r, 6).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(r, 7).Value = report.TotalTax;
        sheet.Cell(r, 7).Style.Font.Bold = true;
        sheet.Cell(r, 7).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(r, 8).Value = report.TotalCogs;
        sheet.Cell(r, 8).Style.Font.Bold = true;
        sheet.Cell(r, 8).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(r, 9).Value = report.TotalGrossProfit;
        sheet.Cell(r, 9).Style.Font.Bold = true;
        sheet.Cell(r, 9).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(r, 10).Value = report.OverallGrossMarginPercent / 100m;
        sheet.Cell(r, 10).Style.Font.Bold = true;
        sheet.Cell(r, 10).Style.NumberFormat.Format = "0.00%";

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(startRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
