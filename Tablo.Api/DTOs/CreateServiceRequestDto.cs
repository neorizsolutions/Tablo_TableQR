using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs
{
    public class CreateServiceRequestDto
    {
        public Guid? DiningGroupId { get; set; }
        [Required]
        public string Type { get; set; } = string.Empty; // "CallWaiter", "RequestBill"
    }
}
