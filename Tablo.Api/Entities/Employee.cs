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
        
        [Required]
        [MaxLength(255)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(500)]
        public string PasswordHash { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
