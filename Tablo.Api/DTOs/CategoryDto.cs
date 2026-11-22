using System;
using System.Collections.Generic;

namespace Tablo.Api.DTOs
{
    public class CategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public Guid? KitchenStationId { get; set; }
        public string? KitchenStationName { get; set; }
        public List<MenuItemDto> MenuItems { get; set; } = new();
    }
}
