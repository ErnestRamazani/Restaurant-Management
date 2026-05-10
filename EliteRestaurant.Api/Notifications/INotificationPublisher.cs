namespace EliteRestaurant.Api.Notifications;

public interface INotificationPublisher
{
    Task PublishAsync(string topic, string message, CancellationToken cancellationToken = default);
}
