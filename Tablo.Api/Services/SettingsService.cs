using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ISubscriptionService _subscriptions;

        public SettingsService(ApplicationDbContext context, ISubscriptionService subscriptions)
        {
            _context = context;
            _subscriptions = subscriptions;
        }

        public async Task<RestaurantSettingsDto> GetSettingsAsync(Guid restaurantId)
        {
            var settings = await _context.RestaurantSettings
                .Include(s => s.DefaultKitchenStation)
                .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);

            if (settings == null)
            {
                return new RestaurantSettingsDto
                {
                    IsAcceptingOrders = true,
                    CostingMethod = "WeightedAverage",
                    AutoManageAvailability = false,
                    KotPrintingEnabled = false,
                    KitchenSetupMode = "SingleKitchen"
                };
            }

            return new RestaurantSettingsDto
            {
                IsAcceptingOrders = settings.IsAcceptingOrders,
                CostingMethod = settings.CostingMethod,
                AutoManageAvailability = settings.AutoManageAvailability,
                KotPrintingEnabled = settings.KotPrintingEnabled,
                KitchenSetupMode = settings.KitchenSetupMode,
                DefaultKitchenStationId = settings.DefaultKitchenStationId,
                DefaultKitchenStationName = settings.DefaultKitchenStation?.Name,
                ScheduledPickupEnabled = settings.ScheduledPickupEnabled,
                DefaultPreparationLeadMinutes = settings.DefaultPreparationLeadMinutes,
                CarSideOrderingEnabled = settings.CarSideOrderingEnabled,
                HybridDineInParcelEnabled = settings.HybridDineInParcelEnabled,
                LeftoverContainerCharge = settings.LeftoverContainerCharge,
                UpiQrEnabled = settings.UpiQrEnabled,
                UpiVpa = settings.UpiVpa,
                UpiPayeeName = settings.UpiPayeeName,
                UpiMerchantCode = settings.UpiMerchantCode
            };
        }

        public async Task<RestaurantSettingsDto> UpdateAcceptingOrdersAsync(Guid restaurantId, bool isAcceptingOrders)
        {
            var settings = await _context.RestaurantSettings
                .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);

            if (settings == null)
            {
                settings = new RestaurantSettings
                {
                    RestaurantId = restaurantId,
                    IsAcceptingOrders = isAcceptingOrders
                };
                _context.RestaurantSettings.Add(settings);
            }
            else
            {
                settings.IsAcceptingOrders = isAcceptingOrders;
                _context.RestaurantSettings.Update(settings);
            }

            await _context.SaveChangesAsync();
            return await GetSettingsAsync(restaurantId);
        }

        public async Task<RestaurantSettingsDto> UpdateKitchenPrintingSettingsAsync(Guid restaurantId, UpdateKitchenPrintingSettingsDto request)
        {
            var settings = await _context.RestaurantSettings
                .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);

            if (settings == null)
            {
                settings = new RestaurantSettings
                {
                    RestaurantId = restaurantId,
                    IsAcceptingOrders = true
                };
                _context.RestaurantSettings.Add(settings);
            }

            if (request.KotPrintingEnabled)
            {
                // Enforce subscription capability
                bool hasPrinting = await _subscriptions.HasCapabilityAsync(restaurantId, Constants.Capabilities.ThermalPrinting);
                if (!hasPrinting)
                {
                    throw new InvalidOperationException("Thermal KOT printing is not included in your current subscription plan. Please upgrade to enable this feature.");
                }

                // Check stations exist
                var activeStations = await _context.KitchenStations
                    .Include(s => s.Printer)
                    .Where(s => s.RestaurantId == restaurantId && s.IsActive)
                    .ToListAsync();

                if (activeStations.Count == 0)
                {
                    throw new InvalidOperationException("Cannot enable automatic KOT printing without at least one active kitchen station.");
                }

                if (request.KitchenSetupMode != "SingleKitchen")
                {
                    bool hasMulti = await _subscriptions.HasCapabilityAsync(restaurantId, Constants.Capabilities.MultipleKitchenStations);
                    if (!hasMulti)
                    {
                        throw new InvalidOperationException("Separate kitchen stations require the multiple_stations capability in your plan.");
                    }
                }

                // Verify default station
                Guid targetDefId = request.DefaultKitchenStationId ?? settings.DefaultKitchenStationId ?? activeStations.FirstOrDefault(s => s.IsDefault)?.Id ?? activeStations[0].Id;
                var defStation = activeStations.FirstOrDefault(s => s.Id == targetDefId);
                if (defStation == null)
                {
                    throw new InvalidOperationException("Selected default kitchen station does not exist or is inactive.");
                }

                if (defStation.PrinterId == null || defStation.Printer == null || !defStation.Printer.IsActive)
                {
                    throw new InvalidOperationException($"Default kitchen station '{defStation.Name}' does not have an active printer assigned. Assign a printer before enabling printing.");
                }

                settings.DefaultKitchenStationId = targetDefId;
            }
            else if (request.DefaultKitchenStationId.HasValue)
            {
                settings.DefaultKitchenStationId = request.DefaultKitchenStationId.Value;
            }

            settings.KotPrintingEnabled = request.KotPrintingEnabled;
            settings.KitchenSetupMode = request.KitchenSetupMode;

            await _context.SaveChangesAsync();
            return await GetSettingsAsync(restaurantId);
        }

        public async Task<RestaurantSettingsDto> UpdateUpiQrSettingsAsync(Guid restaurantId, UpdateUpiQrSettingsDto request)
        {
            var settings = await _context.RestaurantSettings
                .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
            if (settings == null)
            {
                settings = new RestaurantSettings { RestaurantId = restaurantId };
                _context.RestaurantSettings.Add(settings);
            }

            if (request.UpiQrEnabled)
            {
                bool hasCap = await _subscriptions.HasCapabilityAsync(restaurantId, Constants.Capabilities.DynamicUpiQr);
                if (!hasCap)
                {
                    throw new InvalidOperationException("Dynamic UPI QR payment feature is not included in your subscription plan.");
                }

                if (string.IsNullOrWhiteSpace(request.UpiVpa) || !request.UpiVpa.Contains("@"))
                {
                    throw new ArgumentException("A valid Payee VPA (e.g. name@bank) is required to enable UPI QR.");
                }
                if (string.IsNullOrWhiteSpace(request.UpiPayeeName))
                {
                    throw new ArgumentException("Payee display name is required to enable UPI QR.");
                }
            }

            settings.UpiQrEnabled = request.UpiQrEnabled;
            settings.UpiVpa = request.UpiVpa?.Trim();
            settings.UpiPayeeName = request.UpiPayeeName?.Trim();
            settings.UpiMerchantCode = request.UpiMerchantCode?.Trim();

            await _context.SaveChangesAsync();
            return await GetSettingsAsync(restaurantId);
        }

        public async Task<RestaurantSettingsDto> UpdateScheduledPickupSettingsAsync(Guid restaurantId, UpdateScheduledPickupSettingsDto request)
        {
            var settings = await _context.RestaurantSettings
                .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
            if (settings == null)
            {
                settings = new RestaurantSettings { RestaurantId = restaurantId };
                _context.RestaurantSettings.Add(settings);
            }

            if (request.ScheduledPickupEnabled)
            {
                bool hasCap = await _subscriptions.HasCapabilityAsync(restaurantId, Constants.Capabilities.ScheduledPickup);
                if (!hasCap)
                {
                    throw new InvalidOperationException("Scheduled advance parcel pickup is not included in your subscription plan.");
                }
            }

            if (request.DefaultPreparationLeadMinutes < 1)
            {
                throw new ArgumentException("Default preparation lead time must be at least 1 minute.");
            }

            settings.ScheduledPickupEnabled = request.ScheduledPickupEnabled;
            settings.DefaultPreparationLeadMinutes = request.DefaultPreparationLeadMinutes;

            await _context.SaveChangesAsync();
            return await GetSettingsAsync(restaurantId);
        }

        public async Task<RestaurantSettingsDto> UpdateCarSideSettingsAsync(Guid restaurantId, UpdateCarSideSettingsDto request)
        {
            var settings = await _context.RestaurantSettings
                .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
            if (settings == null)
            {
                settings = new RestaurantSettings { RestaurantId = restaurantId };
                _context.RestaurantSettings.Add(settings);
            }

            if (request.CarSideOrderingEnabled)
            {
                bool hasCap = await _subscriptions.HasCapabilityAsync(restaurantId, Constants.Capabilities.CarSideOrdering);
                if (!hasCap)
                {
                    throw new InvalidOperationException("Car-side parking ordering is not included in your subscription plan.");
                }
            }

            settings.CarSideOrderingEnabled = request.CarSideOrderingEnabled;

            await _context.SaveChangesAsync();
            return await GetSettingsAsync(restaurantId);
        }

        public async Task<RestaurantSettingsDto> UpdateHybridPackagingSettingsAsync(Guid restaurantId, UpdateHybridPackagingSettingsDto request)
        {
            var settings = await _context.RestaurantSettings
                .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
            if (settings == null)
            {
                settings = new RestaurantSettings { RestaurantId = restaurantId };
                _context.RestaurantSettings.Add(settings);
            }

            if (request.HybridDineInParcelEnabled)
            {
                bool hasCap = await _subscriptions.HasCapabilityAsync(restaurantId, Constants.Capabilities.HybridDineInParcel);
                if (!hasCap)
                {
                    throw new InvalidOperationException("Hybrid dine-in and parcel ordering is not included in your subscription plan.");
                }
            }

            settings.HybridDineInParcelEnabled = request.HybridDineInParcelEnabled;
            settings.LeftoverContainerCharge = Math.Max(0m, request.LeftoverContainerCharge);

            await _context.SaveChangesAsync();
            return await GetSettingsAsync(restaurantId);
        }
    }
}
