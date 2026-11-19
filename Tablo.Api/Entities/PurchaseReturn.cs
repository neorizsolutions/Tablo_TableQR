using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class PurchaseReturn
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }

    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public Guid PurchaseInvoiceId { get; set; }
    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string ReturnNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string CreditNoteReference { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Reason { get; set; } = string.Empty;

    public DateTime ReturnDateUtc { get; set; } = DateTime.UtcNow;

    public Guid? ProcessedByEmployeeId { get; set; }
    public Employee? ProcessedByEmployee { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCreditAmount { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Guid? OperationId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<PurchaseReturnItem> Items { get; set; } = new List<PurchaseReturnItem>();
}
