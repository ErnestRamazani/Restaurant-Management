using EliteRestaurant.Api.Services;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EliteRestaurant.Api.Services;

public sealed class ReservationLifecycleProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<ReservationLifecycleProcessor> logger,
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

                var now = DateTime.UtcNow;
                var turn = TimeSpan.FromMinutes(Math.Max(30, opts.DefaultTurnMinutes));

                var overstays = await db.ReservationEngagements
                    .Where(e =>
                        e.Status == ReservationEngagementStatuses.CheckedIn
                        && e.ActualStartUtc != null
                        && !e.RotationOrOverstayFlag
                        && e.ActualStartUtc.Value + turn < now)
                    .ToListAsync(stoppingToken);

                if (overstays.Count > 0)
                {
                    foreach (var e in overstays)
                    {
                        e.RotationOrOverstayFlag = true;
                        e.UpdatedAtUtc = now;
                    }

                    await db.SaveChangesAsync(stoppingToken);
                    await realtime.PublishFloorAsync(await snapshots.BuildAsync(stoppingToken), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Reservation lifecycle scan failed.");
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
