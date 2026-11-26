using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Attributes;
using Tablo.Api.Constants;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Interfaces;
using Tablo.Api.Services;

namespace Tablo.Api.Controllers;

[ApiController]
[Route("api/restaurants/{restaurantId:guid}/parcel")]
[Authorize(Roles = "Admin,Owner,SuperAdmin,Waiter,Cashier,Kitchen")]
public class ParcelController : ControllerBase
{
    private readonly IParcelOrderService _parcelService;
    private readonly ApplicationDbContext _db;

    public ParcelController(IParcelOrderService parcelService, ApplicationDbContext db)
    {
        _parcelService = parcelService;
        _db = db;
    }

    private Guid GetActorId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private string GetActorEmail() => User.Identity?.Name ?? "Staff";

    [HttpPost("orders")]
    [RequireCapability(Capabilities.ParcelCounter)]
    public async Task<IActionResult> CreateParcelOrder(Guid restaurantId, [FromBody] CreateParcelOrderRequestDto request)
    {
        var actorId = GetActorId();
        var actorEmail = GetActorEmail();

        try
        {
            var result = await _parcelService.CreateParcelOrderAsync(restaurantId, actorId, actorEmail, request);
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
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("orders/{orderId:guid}/cancel")]
    public async Task<IActionResult> CancelParcelOrder(Guid restaurantId, Guid orderId, [FromBody] CancelParcelOrderRequestDto? dto)
    {
        var actorId = GetActorId();
        string reason = dto?.Reason ?? "Cancelled by staff";
        try
        {
            var result = await _parcelService.CancelParcelOrderAsync(restaurantId, orderId, actorId, reason);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ServiceConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("orders")]
    public async Task<IActionResult> SearchOrders(
        Guid restaurantId,
        [FromQuery] string? businessDate = null,
        [FromQuery] int? token = null,
        [FromQuery] string? status = null,
        [FromQuery] string? customer = null,
        [FromQuery] int limit = 50)
    {
        var filter = new ParcelSearchFilterDto
        {
            BusinessDate = businessDate,
            DailyTokenNumber = token,
            Status = status,
            CustomerSearch = customer,
            Limit = Math.Clamp(limit, 1, 100)
        };

        var orders = await _parcelService.SearchParcelOrdersAsync(restaurantId, filter);
        return Ok(orders);
    }

    [HttpGet("orders/{orderId:guid}")]
    public async Task<IActionResult> GetOrder(Guid restaurantId, Guid orderId)
    {
        var order = await _parcelService.GetParcelOrderByIdAsync(restaurantId, orderId);
        if (order == null) return NotFound(new { message = "Parcel order not found." });
        return Ok(order);
    }

    [HttpPost("orders/{orderId:guid}/settle")]
    public async Task<IActionResult> SettleOrder(
        Guid restaurantId,
        Guid orderId,
        [FromBody] SettleParcelBillRequestDto request)
    {
        var actorId = GetActorId();

        try
        {
            var settledBill = await _parcelService.SettleParcelBillAsync(restaurantId, orderId, actorId, request);
            return Ok(settledBill);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("orders/{orderId:guid}/payments")]
    public async Task<IActionResult> RecordParcelPayment(
        Guid restaurantId,
        Guid orderId,
        [FromBody] RecordBillPayment request)
    {
        var actorId = GetActorId();
        var order = await _parcelService.GetParcelOrderByIdAsync(restaurantId, orderId);
        if (order == null) return NotFound(new { message = "Parcel order not found." });

        try
        {
            var result = await new BillPaymentService(_db).RecordAsync(restaurantId, actorId, order.BillId, request);
            if (result.Remaining == 0)
            {
                var dbOrder = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.RestaurantId == restaurantId);
                if (dbOrder != null && dbOrder.Status != "Served" && !dbOrder.IsScheduled && dbOrder.OrderChannel != "CarParking")
                {
                    dbOrder.Status = "Served";
                    dbOrder.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
            }
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
        catch (Exception ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("orders/{orderId:guid}/print-receipt")]
    public async Task<IActionResult> PrintReceipt(
        Guid restaurantId,
        Guid orderId,
        [FromBody] ReprintTicketRequestDto? reprintDto = null)
    {
        var actorId = GetActorId();
        var actorEmail = GetActorEmail();

        try
        {
            var job = await _parcelService.PrintBillReceiptAsync(
                restaurantId,
                orderId,
                actorId,
                actorEmail,
                reprintDto?.Reason);

            return Ok(job);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("orders/{orderId:guid}/retry-kot")]
    public async Task<IActionResult> RetryKot(Guid restaurantId, Guid orderId)
    {
        var actorId = GetActorId();
        var actorEmail = GetActorEmail();

        try
        {
            var jobs = await _parcelService.RetryParcelKotAsync(restaurantId, orderId, actorId, actorEmail);
            return Ok(new { message = $"{jobs.Count} KOT job(s) queued.", jobs });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("orders/{orderId:guid}/release-now")]
    [RequireCapability(Capabilities.ScheduledPickup)]
    public async Task<IActionResult> ReleaseScheduledOrderNow(Guid restaurantId, Guid orderId)
    {
        var actorId = GetActorId();
        var actorEmail = GetActorEmail();

        try
        {
            var result = await _parcelService.ReleaseScheduledOrderAsync(restaurantId, orderId, actorId, actorEmail);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ServiceConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("orders/{orderId:guid}/reschedule")]
    [RequireCapability(Capabilities.ScheduledPickup)]
    public async Task<IActionResult> RescheduleOrder(Guid restaurantId, Guid orderId, [FromBody] RescheduleParcelOrderRequestDto request)
    {
        var actorId = GetActorId();

        try
        {
            var result = await _parcelService.RescheduleParcelOrderAsync(restaurantId, orderId, actorId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ServiceConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("orders/{orderId:guid}/deliver")]
    [RequireCapability(Capabilities.CarSideOrdering)]
    public async Task<IActionResult> DeliverCarOrder(Guid restaurantId, Guid orderId, [FromBody] DeliverCarOrderRequestDto request)
    {
        var actorId = GetActorId();

        try
        {
            var result = await _parcelService.DeliverCarOrderAsync(restaurantId, orderId, actorId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ServiceConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bills/{billId:guid}/leftover-packing")]
    [RequireCapability(Capabilities.HybridDineInParcel)]
    public async Task<IActionResult> AddLeftoverPacking(Guid restaurantId, Guid billId, [FromBody] AddLeftoverPackingRequestDto request)
    {
        var actorId = GetActorId();

        try
        {
            var result = await _parcelService.AddLeftoverPackingChargeAsync(restaurantId, billId, actorId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ServiceConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("bills/{billId:guid}/upi-qr")]
    [RequireCapability(Capabilities.DynamicUpiQr)]
    public async Task<IActionResult> GetBillUpiQr(Guid restaurantId, Guid billId, [FromQuery] decimal? amount = null)
    {
        try
        {
            var qr = await _parcelService.GenerateBillUpiQrAsync(restaurantId, billId, amount);
            return Ok(qr);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ServiceConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(Guid restaurantId)
    {
        var settings = await _parcelService.GetParcelSettingsAsync(restaurantId);
        return Ok(settings);
    }

    [HttpPut("settings")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin,Waiter,Cashier,Kitchen")]
    public async Task<IActionResult> UpdateSettings(Guid restaurantId, [FromBody] UpdateParcelSettingsRequestDto dto)
    {
        var updated = await _parcelService.UpdateParcelSettingsAsync(restaurantId, dto);
        return Ok(updated);
    }

    [HttpPost("marketplace-payouts")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin,Cashier,Waiter,Kitchen")]
    public async Task<IActionResult> RecordMarketplacePayout(Guid restaurantId, [FromBody] RecordMarketplacePayoutDto request)
    {
        var actorId = GetActorId();
        try
        {
            var result = await _parcelService.RecordMarketplacePayoutAsync(restaurantId, actorId, request);
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
    }

    [HttpGet("marketplace-payouts")]
    [Authorize(Roles = "Admin,Owner,SuperAdmin,Cashier,Waiter,Kitchen")]
    public async Task<IActionResult> GetMarketplacePayouts(Guid restaurantId, [FromQuery] string? deliveryProvider = null)
    {
        var payouts = await _parcelService.GetMarketplacePayoutsAsync(restaurantId, deliveryProvider);
        return Ok(payouts);
    }
}
