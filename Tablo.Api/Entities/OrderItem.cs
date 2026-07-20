using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tablo.Api.Entities
{
    public class OrderItem
    {
        public Guid Id { get; set; }
        
        public Guid OrderId { get; set; }
        public Order Order { get; set; } = null!;
        
        public Guid MenuItemId { get; set; }
        public MenuItem MenuItem { get; set; } = null!;
        
        public int Quantity { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }
        
        [MaxLength(500)]
        public string? SpecialInstructions { get; set; }
    }
}
