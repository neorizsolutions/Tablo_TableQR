using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs
{
    public class CreateCategoryRequestDto
    {
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
    }
}
