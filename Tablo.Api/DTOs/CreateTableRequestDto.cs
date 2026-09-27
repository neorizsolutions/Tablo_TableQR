using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs
{
    public class CreateTableRequestDto
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int TableNumber { get; set; }
    }
}
