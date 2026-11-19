using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities;

public class StockTransfer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string TransferNumber { get; set; } = string.Empty;

    public Guid SourceOutletId { get; set; }
    public Outlet SourceOutlet { get; set; } = null!;

    public Guid DestinationOutletId { get; set; }
    public Outlet DestinationOutlet { get; set; } = null!;

    /// <summary>
    /// "Requested", "Approved", "Dispatched", "Received", "Cancelled"
    /// </summary>
    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "Requested";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DispatchedAtUtc { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }

    public Guid? CreatedByEmployeeId { get; set; }
    public Employee? CreatedByEmployee { get; set; }

    public Guid? DispatchedByEmployeeId { get; set; }
    public Employee? DispatchedByEmployee { get; set; }

    public Guid? ReceivedByEmployeeId { get; set; }
    public Employee? ReceivedByEmployee { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Guid? OperationId { get; set; }

    public ICollection<StockTransferItem> Items { get; set; } = new List<StockTransferItem>();
}
