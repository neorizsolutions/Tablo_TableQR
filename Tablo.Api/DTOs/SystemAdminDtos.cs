using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs
{
    public class SysAdminLoginDto
    {
        [Required]
        public string Username { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class SysAdminOnboardDto
    {
        [Required]
        [MaxLength(100)]
        public string RestaurantName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string AdminFirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string AdminLastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string AdminEmail { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        [MaxLength(100)]
        public string AdminPassword { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? PlanCode { get; set; }
    }
}
