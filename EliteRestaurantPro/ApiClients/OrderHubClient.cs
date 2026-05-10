using System.Text.Json;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Microsoft.AspNetCore.SignalR.Client;

namespace EliteRestaurantPro.ApiClients;

public sealed class OrderHubClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions HubJson = new() { PropertyNameCaseInsensitive = true };

    private HubConnection? _connection;

    public event Action<string>? CustomerDraftArrived;
    public event Action<OrderReadyNotification>? OrderReady;

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

        _connection.On<JsonElement>("OrderReady", el =>
        {
            var n = JsonSerializer.Deserialize<OrderReadyNotification>(el.GetRawText(), HubJson);
            if (n is not null)
                OrderReady?.Invoke(n);
        });

        _connection.Reconnected += async _ =>
        {
            if (_connection is null) return;
            await _connection.InvokeAsync("JoinServer", cancellationToken);
            await _connection.InvokeAsync("JoinCashierDashboard", cancellationToken);
        };

        await _connection.StartAsync(cancellationToken);
        await _connection.InvokeAsync("JoinServer", cancellationToken);
        await _connection.InvokeAsync("JoinCashierDashboard", cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
