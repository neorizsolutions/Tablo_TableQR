using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.DTOs
{
    public class CreateEmployeeDto
    {
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string RoleName { get; set; } = string.Empty;
    }

    public class UpdateEmployeeDto
    {
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string RoleName { get; set; } = string.Empty;
    }

    public class UpdateEmployeeStatusDto
    {
        public bool IsActive { get; set; }
    }
}
