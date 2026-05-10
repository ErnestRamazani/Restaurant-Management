using EliteRestaurant.Api.Notifications;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EliteRestaurant.Api.Services;

public sealed class ReservationReminderProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<ReservationReminderProcessor> logger,
    INotificationPublisher notifications,
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

                var now = DateTime.UtcNow;
                var horizon = TimeSpan.FromHours(opts.ReminderHoursBefore);
                var pending = await db.ReservationEngagements
                    .Where(e =>
                        e.Status == ReservationEngagementStatuses.Scheduled
                        && e.ReminderTwoHoursBeforeSentAtUtc == null
                        && e.PlannedStartUtc > now
                        && e.PlannedStartUtc <= now + horizon)
                    .ToListAsync(stoppingToken);

                foreach (var e in pending)
                {
                    e.ReminderTwoHoursBeforeSentAtUtc = now;
                    e.UpdatedAtUtc = now;
                    await notifications.PublishAsync(
                        "reservation.reminder",
                        $"Engagement #{e.Id}: {e.GuestName} at {e.PlannedStartUtc:O}, party {e.PartySize}.",
                        stoppingToken);
                }

                if (pending.Count > 0)
                    await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Reservation reminder scan failed.");
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
