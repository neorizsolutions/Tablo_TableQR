using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities;

public class PurchaseOrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string PackUnit { get; set; } = "Pack";

    [Column(TypeName = "decimal(18,4)")]
    public decimal OrderedPackQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReceivedPackQuantity { get; set; } = 0m;

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitPricePerPack { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ConversionFactor { get; set; } = 1m;
}
