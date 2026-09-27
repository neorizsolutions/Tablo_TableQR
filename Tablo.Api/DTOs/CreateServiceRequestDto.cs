using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs
{
    public class CreateServiceRequestDto
    {
        [Required]
        public string Type { get; set; } = string.Empty; // "CallWaiter", "RequestBill"
    }
}
