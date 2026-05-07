using EliteRestaurant.Contracts.Admin;

namespace EliteRestaurantPro.ApiClients;

public sealed class AdminDashboardApiClient(EliteApiClient api)
{
    public Task<AdminDashboardDto?> GetDashboardAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<AdminDashboardDto>("api/admin/dashboard", cancellationToken);
}
