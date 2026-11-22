using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs
{
    public class CreateOrderRequestDto
    {
        public Guid? DiningGroupId { get; set; }
        [Required]
        public string QRToken { get; set; } = string.Empty;

        [Required]
        [MinLength(1, ErrorMessage = "At least one item is required")]
        public List<OrderItemRequestDto> Items { get; set; } = new List<OrderItemRequestDto>();
    }
}
