using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Attributes;
using Tablo.Api.Constants;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Extensions;
using Tablo.Api.Interfaces;
using Tablo.Api.Services.Printing;

namespace Tablo.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Owner,SuperAdmin")]
[Route("api/restaurants/{restaurantId:guid}/kitchen-stations")]
public class KitchenStationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ISubscriptionService _subscriptions;
    private readonly IKotRoutingService _kotRouting;
    private readonly ILogger<KitchenStationsController> _logger;

    public KitchenStationsController(
        ApplicationDbContext db,
        ISubscriptionService subscriptions,
        IKotRoutingService kotRouting,
        ILogger<KitchenStationsController> logger)
    {
        _db = db;
        _subscriptions = subscriptions;
        _kotRouting = kotRouting;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetStations(Guid restaurantId)
    {
        var stations = await _db.KitchenStations
            .Include(s => s.Printer)
            .Include(s => s.Categories)
            .Include(s => s.MenuItems)
            .Where(s => s.RestaurantId == restaurantId)
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .Select(s => new KitchenStationDto
            {
                Id = s.Id,
                RestaurantId = s.RestaurantId,
                OutletId = s.OutletId,
                Name = s.Name,
                Code = s.Code,
                IsDefault = s.IsDefault,
                IsActive = s.IsActive,
                PrinterId = s.PrinterId,
                PrinterName = s.Printer != null ? s.Printer.Name : null,
                DisplayOrder = s.DisplayOrder,
                AssignedCategoryCount = s.Categories.Count,
                AssignedMenuItemCount = s.MenuItems.Count,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(stations);
    }

    [HttpPost]
    [RequireCapability(Capabilities.ThermalPrinting)]
    public async Task<IActionResult> CreateStation(Guid restaurantId, [FromBody] CreateKitchenStationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Code))
        {
            return BadRequest(new { message = "Station name and code are required." });
        }

        string cleanCode = dto.Code.Trim().ToUpperInvariant();
        bool codeExists = await _db.KitchenStations.AnyAsync(s => s.RestaurantId == restaurantId && s.Code == cleanCode);
        if (codeExists)
        {
            return BadRequest(new { message = $"A station with code '{cleanCode}' already exists." });
        }

        // Check active station limits
        int activeStations = await _db.KitchenStations.CountAsync(s => s.RestaurantId == restaurantId && s.IsActive);
        if (activeStations >= 1)
        {
            bool hasMultiStation = await _subscriptions.HasCapabilityAsync(restaurantId, Capabilities.MultipleKitchenStations);
            if (!hasMultiStation)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    code = "FEATURE_NOT_IN_PLAN",
                    feature = Capabilities.MultipleKitchenStations,
                    message = "Multiple kitchen stations are not included in your current subscription plan. Please upgrade to create additional stations."
                });
            }

            var access = await _subscriptions.GetEffectiveAccessAsync(restaurantId, User.GetUserId());
            int maxStations = access?.Limits?.MaxKitchenStations ?? 1;
            if (activeStations >= maxStations)
            {
                return BadRequest(new
                {
                    code = "STATION_LIMIT_EXCEEDED",
                    message = $"Your plan permits a maximum of {maxStations} active kitchen stations. You currently have {activeStations} active stations."
                });
            }
        }

        // Validate printer if assigned
        if (dto.PrinterId.HasValue)
        {
            bool printerValid = await _db.ThermalPrinters.AnyAsync(p => p.Id == dto.PrinterId.Value && p.RestaurantId == restaurantId);
            if (!printerValid)
            {
                return BadRequest(new { message = "Assigned printer was not found or does not belong to this restaurant." });
            }
        }

        // If this is set as default, unset existing default
        if (dto.IsDefault || activeStations == 0)
        {
            var existingDefaults = await _db.KitchenStations.Where(s => s.RestaurantId == restaurantId && s.IsDefault).ToListAsync();
            foreach (var ed in existingDefaults)
            {
                ed.IsDefault = false;
            }
        }

        var station = new KitchenStation
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            Name = dto.Name.Trim(),
            Code = cleanCode,
            IsDefault = dto.IsDefault || activeStations == 0,
            IsActive = true,
            PrinterId = dto.PrinterId,
            DisplayOrder = dto.DisplayOrder,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.KitchenStations.Add(station);

        // If this is default, update RestaurantSettings
        if (station.IsDefault)
        {
            var settings = await _db.RestaurantSettings.FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
            if (settings != null)
            {
                settings.DefaultKitchenStationId = station.Id;
            }
        }

        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetStations), new { restaurantId }, new KitchenStationDto
        {
            Id = station.Id,
            RestaurantId = station.RestaurantId,
            Name = station.Name,
            Code = station.Code,
            IsDefault = station.IsDefault,
            IsActive = station.IsActive,
            PrinterId = station.PrinterId,
            DisplayOrder = station.DisplayOrder,
            CreatedAtUtc = station.CreatedAtUtc
        });
    }

    [HttpPut("{stationId:guid}")]
    [RequireCapability(Capabilities.ThermalPrinting)]
    public async Task<IActionResult> UpdateStation(Guid restaurantId, Guid stationId, [FromBody] UpdateKitchenStationDto dto)
    {
        var station = await _db.KitchenStations.FirstOrDefaultAsync(s => s.Id == stationId && s.RestaurantId == restaurantId);
        if (station == null)
        {
            return NotFound(new { message = "Kitchen station not found." });
        }

        string cleanCode = dto.Code.Trim().ToUpperInvariant();
        if (cleanCode != station.Code)
        {
            bool codeExists = await _db.KitchenStations.AnyAsync(s => s.RestaurantId == restaurantId && s.Id != stationId && s.Code == cleanCode);
            if (codeExists)
            {
                return BadRequest(new { message = $"A station with code '{cleanCode}' already exists." });
            }
        }

        // If activating an inactive station, check limits
        if (!station.IsActive && dto.IsActive)
        {
            int currentActive = await _db.KitchenStations.CountAsync(s => s.RestaurantId == restaurantId && s.IsActive && s.Id != stationId);
            if (currentActive >= 1)
            {
                bool hasMultiStation = await _subscriptions.HasCapabilityAsync(restaurantId, Capabilities.MultipleKitchenStations);
                if (!hasMultiStation)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        code = "FEATURE_NOT_IN_PLAN",
                        feature = Capabilities.MultipleKitchenStations,
                        message = "Multiple active kitchen stations require the multiple_stations capability in your plan."
                    });
                }

                var access = await _subscriptions.GetEffectiveAccessAsync(restaurantId, User.GetUserId());
                int maxStations = access?.Limits?.MaxKitchenStations ?? 1;
                if (currentActive >= maxStations)
                {
                    return BadRequest(new
                    {
                        code = "STATION_LIMIT_EXCEEDED",
                        message = $"Your plan permits a maximum of {maxStations} active kitchen stations."
                    });
                }
            }
        }

        // If unsetting default when it is the only active default, reject
        if (station.IsDefault && !dto.IsDefault)
        {
            bool otherDefault = await _db.KitchenStations.AnyAsync(s => s.RestaurantId == restaurantId && s.Id != stationId && s.IsDefault && s.IsActive);
            if (!otherDefault)
            {
                return BadRequest(new { message = "A restaurant must have at least one default kitchen station. Set another station as default first." });
            }
        }

        // If setting as default, unset others
        if (dto.IsDefault && !station.IsDefault)
        {
            var otherDefaults = await _db.KitchenStations.Where(s => s.RestaurantId == restaurantId && s.Id != stationId && s.IsDefault).ToListAsync();
            foreach (var od in otherDefaults)
            {
                od.IsDefault = false;
            }

            var settings = await _db.RestaurantSettings.FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
            if (settings != null)
            {
                settings.DefaultKitchenStationId = station.Id;
            }
        }

        station.Name = dto.Name.Trim();
        station.Code = cleanCode;
        station.IsDefault = dto.IsDefault;
        station.IsActive = dto.IsActive;
        station.PrinterId = dto.PrinterId;
        station.DisplayOrder = dto.DisplayOrder;
        station.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new KitchenStationDto
        {
            Id = station.Id,
            RestaurantId = station.RestaurantId,
            Name = station.Name,
            Code = station.Code,
            IsDefault = station.IsDefault,
            IsActive = station.IsActive,
            PrinterId = station.PrinterId,
            DisplayOrder = station.DisplayOrder,
            CreatedAtUtc = station.CreatedAtUtc
        });
    }

    [HttpDelete("{stationId:guid}")]
    [RequireCapability(Capabilities.ThermalPrinting)]
    public async Task<IActionResult> DeactivateStation(Guid restaurantId, Guid stationId)
    {
        var station = await _db.KitchenStations.FirstOrDefaultAsync(s => s.Id == stationId && s.RestaurantId == restaurantId);
        if (station == null)
        {
            return NotFound(new { message = "Kitchen station not found." });
        }

        if (station.IsDefault)
        {
            return BadRequest(new { message = "Cannot delete or deactivate the default kitchen station. Set another station as default first." });
        }

        station.IsActive = false;
        station.UpdatedAtUtc = DateTime.UtcNow;

        // Reassign any categories or menu items targeting this station to null (falls back to default)
        var categories = await _db.Categories.Where(c => c.RestaurantId == restaurantId && c.KitchenStationId == stationId).ToListAsync();
        foreach (var c in categories) c.KitchenStationId = null;

        var menuItems = await _db.MenuItems.Where(m => m.RestaurantId == restaurantId && m.KitchenStationId == stationId).ToListAsync();
        foreach (var m in menuItems) m.KitchenStationId = null;

        await _db.SaveChangesAsync();

        return Ok(new { message = $"Station '{station.Name}' deactivated. Items re-routed to default station." });
    }

    [HttpPost("consolidate")]
    public async Task<IActionResult> ConsolidateStations(Guid restaurantId, [FromBody] ConsolidateStationsRequestDto dto)
    {
        var targetStation = await _db.KitchenStations.FirstOrDefaultAsync(s => s.Id == dto.TargetDefaultStationId && s.RestaurantId == restaurantId);
        if (targetStation == null)
        {
            return NotFound(new { message = "Target default station not found." });
        }

        targetStation.IsActive = true;
        targetStation.IsDefault = true;
        targetStation.UpdatedAtUtc = DateTime.UtcNow;

        // Unset default on all other stations and deactivate them
        var otherStations = await _db.KitchenStations.Where(s => s.RestaurantId == restaurantId && s.Id != targetStation.Id).ToListAsync();
        int deactivatedCount = 0;
        foreach (var s in otherStations)
        {
            s.IsDefault = false;
            if (s.IsActive)
            {
                s.IsActive = false;
                deactivatedCount++;
            }
            s.UpdatedAtUtc = DateTime.UtcNow;
        }

        // Reassign all categories and items
        var categories = await _db.Categories.Where(c => c.RestaurantId == restaurantId && c.KitchenStationId != null).ToListAsync();
        int catCount = categories.Count;
        foreach (var c in categories) c.KitchenStationId = null;

        var menuItems = await _db.MenuItems.Where(m => m.RestaurantId == restaurantId && m.KitchenStationId != null).ToListAsync();
        int itemCount = menuItems.Count;
        foreach (var m in menuItems) m.KitchenStationId = null;

        // Update restaurant settings
        var settings = await _db.RestaurantSettings.FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
        if (settings != null)
        {
            settings.KitchenSetupMode = "SingleKitchen";
            settings.DefaultKitchenStationId = targetStation.Id;
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Restaurant {RestaurantId} consolidated stations to {DefaultStation}. Reason: {Reason}",
            restaurantId, targetStation.Name, dto.Reason);

        return Ok(new ConsolidateStationsResultDto
        {
            DefaultStationId = targetStation.Id,
            DefaultStationName = targetStation.Name,
            DeactivatedStationsCount = deactivatedCount,
            ReassignedCategoriesCount = catCount,
            ReassignedMenuItemsCount = itemCount,
            Message = $"Successfully consolidated all routing to default station '{targetStation.Name}'. Existing tickets and print history are fully preserved."
        });
    }

    [HttpPost("routing-preview")]
    public async Task<IActionResult> PreviewRouting(Guid restaurantId, [FromBody] RoutingPreviewRequestDto request)
    {
        var preview = await _kotRouting.PreviewRoutingAsync(restaurantId, request.Items);
        return Ok(preview);
    }
}
