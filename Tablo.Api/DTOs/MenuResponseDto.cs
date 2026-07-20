using System;
using System.Collections.Generic;

namespace Tablo.Api.DTOs
{
    public class MenuResponseDto
    {
        public Guid RestaurantId { get; set; }
        public List<CategoryDto> Categories { get; set; } = new();
    }
}
