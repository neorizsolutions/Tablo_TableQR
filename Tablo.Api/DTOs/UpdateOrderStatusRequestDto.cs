using System;
using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs
{
    public class UpdateOrderStatusRequestDto
    {
        [Required]
        public Guid RestaurantId { get; set; }

        [Required]
        public string Status { get; set; } = string.Empty;

        public string? ExceptionReason { get; set; }
    }
}
