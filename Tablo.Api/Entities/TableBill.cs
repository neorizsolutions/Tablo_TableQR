namespace Tablo.Api.Entities;

public class TableBill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Guid? TableId { get; set; }
    public Guid? DiningGroupId { get; set; }
    public string? GroupLabelSnapshot { get; set; }
    public string OrderChannel { get; set; } = "DineIn";
    public string? DeliveryProvider { get; set; }
    public string? ExternalOrderReference { get; set; }
    public int? DailyTokenNumber { get; set; }
    public string? BusinessDate { get; set; }
    public decimal PackagingCharge { get; set; } = 0m;
    public decimal LeftoverPackingCharge { get; set; } = 0m;
    public string? VehiclePlate { get; set; }
    public string? ParkingBay { get; set; }
    public string? PackagingChargeModeSnapshot { get; set; }
    public decimal PackagingRateSnapshot { get; set; } = 0m;
    public string? PackagingWaiverReason { get; set; }
    public string? PackagingWaivedBy { get; set; }
    public bool IsPackagingWaived { get; set; } = false;
    public Guid? ParentBillId { get; set; }
    public DateTime? SplitAtUtc { get; set; }
    public string? AllocationJson { get; set; }
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
    public string? PaymentRevision { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
