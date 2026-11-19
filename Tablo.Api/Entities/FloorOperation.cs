namespace Tablo.Api.Entities;

public class FloorOperation
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Guid ActorEmployeeId { get; set; }
    public Guid OperationId { get; set; }
    public string Kind { get; set; }=string.Empty;
    public string Hash { get; set; }=string.Empty;
    public string ResultJson { get; set; }=string.Empty;
    public DateTime CreatedAtUtc { get; set; }=DateTime.UtcNow;
}
