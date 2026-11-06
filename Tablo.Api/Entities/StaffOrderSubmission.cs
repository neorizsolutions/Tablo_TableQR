namespace Tablo.Api.Entities;

public class StaffOrderSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Guid TableId { get; set; }
    public Guid ActorEmployeeId { get; set; }
    public Guid OperationId { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public string OriginalResponseJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
