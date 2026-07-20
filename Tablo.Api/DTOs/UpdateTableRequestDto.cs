using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs
{
    public class UpdateTableRequestDto
    {
        [Required]
        [MaxLength(100)]
        public string NameOrNumber { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
