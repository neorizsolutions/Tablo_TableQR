using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs
{
    public class UpdateCategoryRequestDto
    {
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        public Guid? KitchenStationId { get; set; }
    }
}
