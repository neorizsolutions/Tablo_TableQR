using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class PurchaseInvoice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }

    public Guid? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    /// <summary>
    /// Idempotent per (RestaurantId, SupplierId, InvoiceNumber).
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? ReceivedByEmployeeId { get; set; }
    public Employee? ReceivedByEmployee { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ShippingCharges { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrandTotal { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Guid? OperationId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<PurchaseInvoiceItem> Items { get; set; } = new List<PurchaseInvoiceItem>();
    public ICollection<PurchaseReturn> Returns { get; set; } = new List<PurchaseReturn>();
}
