using EliteRestaurant.Contracts.Auth;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.ApiClients;

public sealed class AuthApiClient(EliteApiClient api)
{
    public async Task<CloudLoginResponse?> LoginAsync(
        string staffId,
        string pin,
        string portal = "Admin",
        CancellationToken cancellationToken = default)
    {
        var response = await api.PostAsync<CloudLoginRequest, CloudLoginResponse>(
            "api/auth/login",
            new CloudLoginRequest(staffId, pin, portal),
            cancellationToken);

        if (response is not null)
        {
            var settings = SettingsManager.Load();
            settings.CloudApi.AccessToken = response.AccessToken;
            settings.CloudApi.TokenExpiresAtUtc = response.ExpiresAtUtc;
            SettingsManager.Save(settings);
            api.ConfigureFromSettings();
        }

        return response;
    }
}
