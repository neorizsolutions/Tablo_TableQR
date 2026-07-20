using System;
using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs
{
    public class CreateMenuItemRequestDto
    {
        [Required]
        public Guid CategoryId { get; set; }
        
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
        public decimal Price { get; set; }
        
        public bool IsAvailable { get; set; } = true;
        
        [MaxLength(2000)]
        public string? ImageUrl { get; set; }
    }
}
