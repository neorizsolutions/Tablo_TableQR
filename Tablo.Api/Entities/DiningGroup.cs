namespace Tablo.Api.Entities;

public class DiningGroup
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Guid TableId { get; set; }
    public string Label { get; set; }=string.Empty;
    public Guid CreatedByEmployeeId { get; set; }
    public Guid CreationOperationId { get; set; }
    public string CreationHash { get; set; }=string.Empty;
    public DateTime CreatedAtUtc { get; set; }=DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }
}
