using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tablo.Api.Data;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services
{
    public class ScheduledOrderReleaseWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ScheduledOrderReleaseWorker> _logger;

        public ScheduledOrderReleaseWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<ScheduledOrderReleaseWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ScheduledOrderReleaseWorker started. Monitoring advance orders for release.");

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessDueScheduledOrdersAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in ScheduledOrderReleaseWorker loop.");
                }
            }

            _logger.LogInformation("ScheduledOrderReleaseWorker stopping.");
        }

        public async Task<int> ProcessDueScheduledOrdersAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var parcelService = scope.ServiceProvider.GetRequiredService<IParcelOrderService>();

            var now = DateTime.UtcNow;
            var dueOrders = await db.Orders.AsNoTracking()
                .Where(o => o.Status == "Scheduled" && o.IsScheduled && o.ScheduledReleaseTimeUtc != null && o.ScheduledReleaseTimeUtc <= now)
                .OrderBy(o => o.ScheduledReleaseTimeUtc)
                .Take(25)
                .Select(o => new { o.RestaurantId, o.Id })
                .ToListAsync(cancellationToken);

            int releasedCount = 0;
            foreach (var due in dueOrders)
            {
                try
                {
                    await parcelService.ReleaseScheduledOrderAsync(
                        due.RestaurantId,
                        due.Id,
                        Guid.Empty,
                        "ScheduledReleaseWorker");
                    releasedCount++;
                    _logger.LogInformation("Auto-released scheduled order {OrderId} for restaurant {RestaurantId} at {TimeUtc}",
                        due.Id, due.RestaurantId, now);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to auto-release scheduled order {OrderId} for restaurant {RestaurantId}",
                        due.Id, due.RestaurantId);
                }
            }

            return releasedCount;
        }
    }
}
