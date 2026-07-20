using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs
{
    public class OrderItemRequestDto
    {
        [Required]
        public Guid MenuItemId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }
    }
}
