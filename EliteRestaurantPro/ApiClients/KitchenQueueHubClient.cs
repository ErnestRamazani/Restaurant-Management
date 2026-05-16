using EliteRestaurant.Core.Utils;
using Microsoft.AspNetCore.SignalR.Client;

namespace EliteRestaurantPro.ApiClients;

/// <summary>Live kitchen queue updates — same hub group as <c>wwwroot/kitchen</c>.</summary>
public sealed class KitchenQueueHubClient : IAsyncDisposable
{
    private HubConnection? _connection;

    public event Action? QueueChanged;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var settings = SettingsManager.Load().CloudApi;
        var baseUrl = CloudEndpoints.NormalizeApiBaseUrl(settings.BaseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
            return;

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/order", options =>
            {
                options.AccessTokenProvider = () =>
                    Task.FromResult<string?>(SettingsManager.Load().CloudApi.AccessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<object>("KitchenQueueChanged", _ => QueueChanged?.Invoke());

        _connection.Reconnected += async _ =>
        {
            if (_connection is null)
                return;
            try
            {
                await _connection.InvokeAsync("JoinKitchen", cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                /* hub optional */
            }
        };

        await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
        await _connection.InvokeAsync("JoinKitchen", cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
