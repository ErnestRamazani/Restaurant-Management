using EliteRestaurant.Core.Utils;
using Microsoft.AspNetCore.SignalR.Client;

namespace EliteRestaurantPro.ApiClients;

public sealed class OrderHubClient : IAsyncDisposable
{
    private HubConnection? _connection;

    public event Action<string>? CustomerDraftArrived;
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var settings = SettingsManager.Load().CloudApi;
        var baseUrl = CloudEndpoints.NormalizeApiBaseUrl(settings.BaseUrl);

        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/order", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(SettingsManager.Load().CloudApi.AccessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<object>("CustomerDraftArrived", payload =>
        {
            CustomerDraftArrived?.Invoke(payload?.ToString() ?? "Customer draft arrived.");
        });

        await _connection.StartAsync(cancellationToken);
        await _connection.InvokeAsync("JoinServer", cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
