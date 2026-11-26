using Microsoft.AspNetCore.Mvc;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Tablo.Api.Extensions;
using Microsoft.AspNetCore.RateLimiting;
using Tablo.Api.Services;

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
        [EnableRateLimiting("PublicApi")]
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

        [Authorize(Roles = "Admin,Owner,Kitchen,Waiter,Cashier")]
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

        [HttpGet("/api/qr/{qrToken}/active-order")]
        public async Task<ActionResult<OrderDto>> GetActiveOrderForTableByQr(string qrToken, [FromServices] IQRService qrService, [FromQuery] Guid? diningGroupId=null)
        {
            try
            {
                var qrResult = await qrService.ResolveQRTokenAsync(qrToken);
                if (qrResult == null) return NotFound(new { message = "Invalid QR token." });
                if(qrResult.DiningGroups.Count>0 && !qrResult.DiningGroups.Any(g=>g.Id==diningGroupId))return BadRequest(new{message="Select an active dining group."});

                var orders = await _orderService.GetOrdersAsync(qrResult.RestaurantId);
                var activeOrder = orders
                    .Where(o => o.TableId == qrResult.TableId && o.DiningGroupId==diningGroupId && !string.Equals(o.Status,"served",StringComparison.OrdinalIgnoreCase) && !string.Equals(o.Status,"cancelled",StringComparison.OrdinalIgnoreCase) && !string.Equals(o.Status,"rejected",StringComparison.OrdinalIgnoreCase))
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

        [Authorize(Roles = "Admin,Owner,Kitchen,Waiter,Cashier")]
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

        [Authorize(Roles = "Admin,Owner,Kitchen,Waiter,Cashier")]
        [HttpPost("{orderId}/fire-items")]
        [HttpPost("/api/restaurants/{restaurantId}/orders/{orderId}/fire-items")]
        public async Task<ActionResult<FiredItemsResponseDto>> FireOrderItems(Guid orderId, [FromBody] FireOrderItemsRequestDto request, Guid? restaurantId = null)
        {
            var resId = restaurantId ?? User.GetRestaurantId();
            if (User.GetRestaurantId() != resId) return Forbid();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var callerId = User.GetUserId();
                if (request.FiredByEmployeeId.HasValue && request.FiredByEmployeeId.Value != callerId)
                {
                    return BadRequest(new { message = "Cannot act on behalf of another employee." });
                }
                request.FiredByEmployeeId = callerId;
                var result = await _orderService.FireOrderItemsAsync(resId, orderId, request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ServiceConflictException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred firing items.", details = ex.Message });
            }
        }

        [Authorize(Roles = "Admin,Owner,Waiter,Kitchen,Cashier")]
        [HttpPost("{orderId}/items/{orderItemId}/refill")]
        [HttpPost("/api/restaurants/{restaurantId}/orders/{orderId}/items/{orderItemId}/refill")]
        public async Task<ActionResult<RefillResponseDto>> CreateRefill(Guid orderId, Guid orderItemId, [FromBody] CreateRefillRequestDto request, Guid? restaurantId = null)
        {
            var resId = restaurantId ?? User.GetRestaurantId();
            if (User.GetRestaurantId() != resId) return Forbid();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var callerId = User.GetUserId();
                if (request.ActorEmployeeId.HasValue && request.ActorEmployeeId.Value != callerId)
                {
                    return BadRequest(new { message = "Cannot act on behalf of another employee." });
                }
                request.ActorEmployeeId = callerId;
                var result = await _orderService.CreateRefillAsync(resId, orderId, orderItemId, request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ServiceConflictException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred creating refill.", details = ex.Message });
            }
        }

        [Authorize(Roles = "Admin,Owner,Kitchen,Waiter,Cashier")]
        [HttpPost("{orderId}/items/{orderItemId}/cancel")]
        [HttpPost("/api/restaurants/{restaurantId}/orders/{orderId}/items/{orderItemId}/cancel")]
        public async Task<ActionResult<ItemCancellationResponseDto>> CancelOrderItem(Guid orderId, Guid orderItemId, [FromBody] CancelOrderItemRequestDto request, Guid? restaurantId = null)
        {
            var resId = restaurantId ?? User.GetRestaurantId();
            if (User.GetRestaurantId() != resId) return Forbid();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var callerId = User.GetUserId();
                if (request.RequestedByEmployeeId.HasValue && request.RequestedByEmployeeId.Value != callerId)
                {
                    return BadRequest(new { message = "Cannot act on behalf of another employee." });
                }
                request.RequestedByEmployeeId = callerId;
                var result = await _orderService.CancelOrderItemAsync(resId, orderId, orderItemId, request);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(401, new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ServiceConflictException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred cancelling item.", details = ex.Message });
            }
        }
    }
}
