using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs
{
    public class UpdateTableRequestDto
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int TableNumber { get; set; }

        public bool IsActive { get; set; }
    }
}
