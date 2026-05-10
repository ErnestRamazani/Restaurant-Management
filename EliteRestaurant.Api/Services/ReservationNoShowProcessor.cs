using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EliteRestaurant.Api.Services;

public sealed class ReservationNoShowProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<ReservationNoShowProcessor> logger,
    IOptions<ReservationAutomationOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        var delay = TimeSpan.FromSeconds(Math.Max(15, opts.ScannerIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var realtime = scope.ServiceProvider.GetRequiredService<ReservationFloorRealtimePublisher>();
                var snapshots = scope.ServiceProvider.GetRequiredService<FloorSnapshotBuilder>();

                var cutoff = DateTime.UtcNow.AddMinutes(-opts.NoShowGraceMinutes);
                var stale = await db.ReservationEngagements
                    .Include(e => e.PlacementUnit)
                    .Where(e =>
                        e.Status == ReservationEngagementStatuses.Scheduled
                        && e.PlannedStartUtc < cutoff)
                    .ToListAsync(stoppingToken);

                if (stale.Count == 0)
                {
                    await Task.Delay(delay, stoppingToken);
                    continue;
                }

                var now = DateTime.UtcNow;
                foreach (var e in stale)
                {
                    e.Status = ReservationEngagementStatuses.NoShow;
                    e.UpdatedAtUtc = now;

                    if (e.PlacementUnit is not null
                        && string.Equals(e.PlacementUnit.Status, PlacementUnitStatuses.Reserved, StringComparison.OrdinalIgnoreCase))
                    {
                        e.PlacementUnit.Status = PlacementUnitStatuses.Available;
                    }
                }

                await db.SaveChangesAsync(stoppingToken);
                await realtime.PublishFloorAsync(await snapshots.BuildAsync(stoppingToken), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Reservation no-show scan failed.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
