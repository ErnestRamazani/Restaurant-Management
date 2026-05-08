using EliteRestaurant.Contracts.Admin;

namespace EliteRestaurantPro.ApiClients;

public sealed class AdminDashboardApiClient(EliteApiClient? api = null)
{
    private readonly EliteApiClient _api = api ?? new EliteApiClient();

    public Task<AdminDashboardDto?> GetDashboardAsync(CancellationToken cancellationToken = default) =>
        _api.GetAsync<AdminDashboardDto>("api/admin/dashboard", cancellationToken);
}
