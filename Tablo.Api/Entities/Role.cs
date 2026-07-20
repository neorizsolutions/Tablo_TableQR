using System.ComponentModel.DataAnnotations;

namespace Tablo.Api.Entities
{
    public class Role
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string? Description { get; set; }

        // Navigation Properties
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}
