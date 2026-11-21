using System;

namespace Tablo.Api.DTOs
{
    public class MenuItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public bool IsAvailable { get; set; }
        public string? ImageUrl { get; set; }
        public Guid? KitchenStationId { get; set; }
        public string? KitchenStationName { get; set; }
        public bool IsRefillEligible { get; set; }
        public int? RefillMaxCount { get; set; }
        public Guid? RefillKitchenStationId { get; set; }
        public List<ModifierGroupDto> ModifierGroups { get; set; } = new();
    }
}
