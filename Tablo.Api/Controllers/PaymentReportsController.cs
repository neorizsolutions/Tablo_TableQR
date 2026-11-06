using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Extensions;
using Tablo.Api.Services;

namespace Tablo.Api.Controllers;

[ApiController]
[Authorize(Roles="Admin")]
[Route("api/restaurants/{restaurantId:guid}/payment-reports")]
public class PaymentReportsController(ApplicationDbContext db, PaymentReportService reports, BillingService billing) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Get(Guid restaurantId,[FromQuery] PaymentReportFilter filter) => Execute(restaurantId,
        async()=>Ok(await reports.GetAsync(restaurantId,filter)));

    [HttpGet("export")]
    public Task<IActionResult> Export(Guid restaurantId,[FromQuery] PaymentReportFilter filter) => Execute(restaurantId,
        async()=>File(await reports.ExportAsync(restaurantId,filter),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","Tablo_Payments_IST.xlsx"));

    [HttpGet("{billId:guid}/receipt")]
    public Task<IActionResult> Receipt(Guid restaurantId,Guid billId) => Execute(restaurantId,
        async()=>Ok(await billing.ReceiptAsync(restaurantId,billId)));

    private async Task<IActionResult> Execute(Guid tenant,Func<Task<IActionResult>> action)
    {
        if(tenant==Guid.Empty || User.GetRestaurantId()!=tenant || User.GetUserId()==Guid.Empty) return Forbid();
        if(!await db.Employees.AnyAsync(e=>e.Id==User.GetUserId() && e.RestaurantId==tenant && e.IsActive && e.Role.Name=="Admin")) return Forbid();
        try { return await action(); }
        catch(ArgumentException ex) { return BadRequest(new { message=ex.Message }); }
        catch(KeyNotFoundException) { return NotFound(new { message="Receipt not found." }); }
    }
}
