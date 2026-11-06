namespace Tablo.Api.Entities;

public class TableBill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Guid TableId { get; set; }
    public int TableNumberSnapshot { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SettledAtUtc { get; set; }
    public Guid? SettledByEmployeeId { get; set; }
    public Guid? SettlementOperationId { get; set; }
    public string? SettlementHash { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? ReceiptJson { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
