using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs;

public class PaymentReportFilter
{
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Guid? TableId { get; set; }
    public string? PaymentMethod { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public class PaymentReportRow
{
    public Guid BillId { get; set; }
    public string? GroupLabel { get; set; }
    public Guid Id { get; set; }
    public Guid? TableId { get; set; }
    public int TableNumber { get; set; }
    public DateTime SettledAtUtc { get; set; }
    public Guid? SettledByEmployeeId { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal TotalAmount { get; set; }
}

public class PaymentReportDto
{
    public int SettledBillCount { get; set; }
    public decimal OutstandingBalance { get; set; }
    public string TimeZone { get; set; } = "Asia/Kolkata";
    public decimal TotalCollected { get; set; }
    public decimal Cash { get; set; }
    public decimal UPI { get; set; }
    public decimal Card { get; set; }
    public decimal Marketplace { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<PaymentReportRow> Items { get; set; } = new();
}
