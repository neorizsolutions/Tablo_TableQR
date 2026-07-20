using Microsoft.AspNetCore.Mvc;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Tablo.Api.Extensions;

namespace Tablo.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpPost]
        public async Task<ActionResult<OrderResponseDto>> CreateOrder([FromBody] CreateOrderRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var response = await _orderService.CreateOrderAsync(request);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // In a real production app we'd log this properly
                return StatusCode(500, new { message = "An error occurred processing your order.", details = ex.Message });
            }
        }

        [Authorize(Roles = "Admin,Kitchen,Waiter")]
        [HttpGet("/api/restaurants/{restaurantId}/orders")]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetOrders(Guid restaurantId, [FromQuery] string? status)
        {
            if (User.GetRestaurantId() != restaurantId) return Forbid();

            try
            {
                var orders = await _orderService.GetOrdersAsync(restaurantId, status);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred fetching orders.", details = ex.Message });
            }
        }

        [HttpGet("/api/restaurants/{restaurantId}/tables/{tableId}/active-order")]
        public async Task<ActionResult<OrderDto>> GetActiveOrderForTable(Guid restaurantId, Guid tableId)
        {
            try
            {
                var orders = await _orderService.GetOrdersAsync(restaurantId);
                var activeOrder = orders
                    .Where(o => o.TableId == tableId && o.Status != "served" && o.Status != "cancelled")
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefault();

                if (activeOrder == null) return NotFound(new { message = "No active order found." });
                return Ok(activeOrder);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred fetching the order.", details = ex.Message });
            }
        }

        [HttpGet("/api/restaurants/{restaurantId}/orders/{orderId}")]
        public async Task<ActionResult<OrderDto>> GetOrder(Guid restaurantId, Guid orderId)
        {
            try
            {
                var order = await _orderService.GetOrderByIdAsync(restaurantId, orderId);
                return Ok(order);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred fetching the order.", details = ex.Message });
            }
        }

        [Authorize(Roles = "Admin,Kitchen,Waiter")]
        [HttpPatch("{orderId}/status")]
        public async Task<ActionResult<OrderDto>> UpdateOrderStatus(Guid orderId, [FromBody] UpdateOrderStatusRequestDto request)
        {
            if (User.GetRestaurantId() != request.RestaurantId) return Forbid();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var response = await _orderService.UpdateOrderStatusAsync(request.RestaurantId, orderId, request);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(new { message = ex.Message });
                }
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred updating the order status.", details = ex.Message });
            }
        }
    }
}
