namespace Tablo.Api.Entities;

public class BillPayment
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Guid? TableIdSnapshot { get; set; }
    public int TableNumberSnapshot { get; set; }
    public string? GroupLabelSnapshot { get; set; }
    public Guid TableBillId { get; set; }
    public Guid ActorEmployeeId { get; set; }
    public Guid OperationId { get; set; }
    public string PayloadHash { get; set; }=string.Empty;
    public string Method { get; set; }=string.Empty;
    public decimal Amount { get; set; }
    public DateTime CollectedAtUtc { get; set; }=DateTime.UtcNow;
    public string AcknowledgementJson { get; set; }=string.Empty;
}
