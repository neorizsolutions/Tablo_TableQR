using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs
{
    public class CreateTableRequestDto
    {
        [Required]
        [MaxLength(100)]
        public string NameOrNumber { get; set; } = string.Empty;
    }
}
