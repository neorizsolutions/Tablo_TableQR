using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs
{
    public class UpdateMenuItemAvailabilityRequestDto
    {
        [Required]
        public bool IsAvailable { get; set; }
    }
}
