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

        [MinLength(6)]
        public string? NewPassword { get; set; }
    }

    public class UpdateEmployeeStatusDto
    {
        public bool IsActive { get; set; }
    }

    public class ResetPasswordDto
    {
        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class EmployeeStatusCountsDto
    {
        public int Active { get; set; }
        public int Inactive { get; set; }
        public int All { get; set; }
    }

    public class DeleteEmployeeResponseDto
    {
        public string Message { get; set; } = string.Empty;
        public Guid EmployeeId { get; set; }
        public bool AlreadyDeleted { get; set; }
        public DateTime? DeletedAtUtc { get; set; }
    }
}
