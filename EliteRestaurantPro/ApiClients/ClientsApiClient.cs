using System.Net.Http;
using EliteRestaurant.Contracts.Clients;

namespace EliteRestaurantPro.ApiClients;

public sealed class ClientsApiClient(EliteApiClient? apiClient = null)
{
    private readonly EliteApiClient _api = apiClient ?? new EliteApiClient();

    public Task<IReadOnlyList<RestaurantClientListItemDto>?> ListAsync(CancellationToken cancellationToken = default) =>
        _api.GetAsync<IReadOnlyList<RestaurantClientListItemDto>>("api/clients", cancellationToken);

    public Task<IReadOnlyList<RestaurantClientSearchResultDto>?> SearchAsync(string? q, CancellationToken cancellationToken = default) =>
        _api.GetAsync<IReadOnlyList<RestaurantClientSearchResultDto>>(
            "api/clients/search?q=" + Uri.EscapeDataString(q ?? string.Empty),
            cancellationToken);

    public Task<RestaurantClientProfileDto?> GetProfileAsync(int id, CancellationToken cancellationToken = default) =>
        _api.GetAsync<RestaurantClientProfileDto>($"api/clients/{id}", cancellationToken);

    public async Task<(RestaurantClientListItemDto? Client, string? Error)> CreateAsync(
        CreateRestaurantClientRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var created = await _api.PostAsync<CreateRestaurantClientRequest, RestaurantClientListItemDto>(
                "api/clients",
                request,
                cancellationToken);
            return (created, null);
        }
        catch (HttpRequestException ex)
        {
            return (null, ex.Message);
        }
    }

    public Task<bool> UpdateAsync(int id, UpdateRestaurantClientRequest request, CancellationToken cancellationToken = default) =>
        _api.PutAsync($"api/clients/{id}", request, cancellationToken);

    public Task<SettleClientDebtResponse?> SettleDebtAsync(int id, SettleClientDebtRequest request, CancellationToken cancellationToken = default) =>
        _api.PostAsync<SettleClientDebtRequest, SettleClientDebtResponse>(
            $"api/clients/{id}/settle-debt",
            request,
            cancellationToken);

    public async Task<bool> LinkOrderAsync(int orderId, int restaurantClientId, CancellationToken cancellationToken = default)
    {
        await _api.PostAsync<LinkOrderToClientRequest, object>(
            $"api/clients/orders/{orderId}/link",
            new LinkOrderToClientRequest(restaurantClientId),
            cancellationToken);
        return true;
    }

    public Task<OrderClientLinkDto?> GetOrderLinkInfoAsync(int orderId, CancellationToken cancellationToken = default) =>
        _api.GetAsync<OrderClientLinkDto>($"api/clients/orders/{orderId}/link-info", cancellationToken);
}
