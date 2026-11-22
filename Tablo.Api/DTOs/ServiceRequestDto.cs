using System;

namespace Tablo.Api.DTOs
{
    public class ServiceRequestDto
    {
        public Guid? DiningGroupId { get; set; }
        public Guid Id { get; set; }
        public Guid RestaurantId { get; set; }
        public Guid TableId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
