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

        [MaxLength(255)]
        public string? SpecialInstructions { get; set; }

        public string ReleaseState { get; set; } = "Fired"; // Fired, Held

        public string PackingIntent { get; set; } = "DineIn"; // DineIn, Takeaway

        public bool WaivePackaging { get; set; } = false;
        public string? PackagingWaiverReason { get; set; }

        public string? CourseName { get; set; } // Starters, Mains, Desserts, Beverages

        public List<SelectedModifierOptionRequestDto>? SelectedModifiers { get; set; }
    }
}
