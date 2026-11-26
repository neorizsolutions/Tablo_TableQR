using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities
{
    public class Employee
    {
        public Guid Id { get; set; }
        
        public Guid RestaurantId { get; set; }
        public Restaurant Restaurant { get; set; } = null!;
        
        public Guid RoleId { get; set; }
        public Role Role { get; set; } = null!;
        
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        public string FullName => $"{FirstName} {LastName}".Trim();
        
        [Required]
        [MaxLength(255)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(500)]
        public string PasswordHash { get; set; } = string.Empty;

        public string[]? AllowedMenuIds { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAtUtc { get; set; }
        public Guid? DeletedByEmployeeId { get; set; }

        [MaxLength(100)]
        public string? SecurityStamp { get; set; }

        public string? ResetOtpHash { get; set; }
        public DateTime? ResetOtpExpiration { get; set; }
        public int ResetOtpAttempts { get; set; }
        public string? ResetTokenHash { get; set; }

        [MaxLength(255)]
        public string? ManagerPinHash { get; set; }
        public int FailedPinAttempts { get; set; } = 0;
        public DateTime? PinLockedUntilUtc { get; set; }
    }
}
