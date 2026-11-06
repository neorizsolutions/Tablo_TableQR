using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs;

public class StaffOrderRequestDto
{
    public Guid OperationId { get; set; }
    public Guid TableId { get; set; }
    [Required, MinLength(1), MaxLength(200)]
    public List<OrderItemRequestDto> Items { get; set; } = new();
}

public class SettleBillRequestDto
{
    public Guid OperationId { get; set; }
    [Required, StringLength(64, MinimumLength = 64)]
    public string ExpectedRevision { get; set; } = string.Empty;
    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal ExpectedTotal { get; set; }
    [Required, RegularExpression("^(Cash|UPI|Card)$")]
    public string PaymentMethod { get; set; } = string.Empty;
}

public class BillDto
{
    public string? RestaurantName { get; set; }
    public Guid? SettledByEmployeeId { get; set; }
    public Guid Id { get; set; }
    public int TableNumber { get; set; }
    public string Status { get; set; } = "Open";
    public string Revision { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SettledAtUtc { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public bool CanSettle { get; set; }
    public List<BillOrderDto> Orders { get; set; } = new();
}

public class BillOrderDto
{
    public Guid Id { get; set; }
    public int OrderNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

public class TableBillReviewDto
{
    public Guid TableId { get; set; }
    public int TableNumber { get; set; }
    public BillDto? OpenBill { get; set; }
    public int UnbilledActiveOrderCount { get; set; }
    public List<BillDto> RecentSettlements { get; set; } = new();
}

public record ServiceTableDto(Guid Id, int TableNumber, bool IsActive);
