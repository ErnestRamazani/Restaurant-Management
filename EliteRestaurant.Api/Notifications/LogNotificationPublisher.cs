namespace EliteRestaurant.Api.Notifications;

public sealed class LogNotificationPublisher(ILogger<LogNotificationPublisher> logger) : INotificationPublisher
{
    public Task PublishAsync(string topic, string message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Reservation notification [{Topic}] {Message}", topic, message);
        return Task.CompletedTask;
    }
}
