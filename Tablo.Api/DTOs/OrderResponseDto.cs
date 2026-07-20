namespace Tablo.Api.DTOs
{
    public class OrderResponseDto
    {
        public Guid OrderId { get; set; }
        public int OrderNumber { get; set; }
        public string Status { get; set; } = null!;
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
