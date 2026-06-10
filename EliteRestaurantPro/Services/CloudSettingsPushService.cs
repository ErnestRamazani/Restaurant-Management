using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;

namespace EliteRestaurantPro.Services;

/// <summary>Pushes desktop settings to the hosted API database (not part of git deploy).</summary>
public static class CloudSettingsPushService
{
    public static string DescribePushTarget(AppSettings settings) =>
        EliteApiClient.ResolvePublicMenuCloudBaseUrl(settings);

    public static bool HasCloudAdminToken(AppSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.CloudApi.AccessToken);

    public static async Task PushAsync(
        AppSettings settings,
        bool applyLogoChanges,
        bool applyOnlinePromoImageChanges,
        bool applyTicketBrandingChanges = false,
        CancellationToken cancellationToken = default)
    {
        if (!HasCloudAdminToken(settings))
        {
            throw new InvalidOperationException(
                "Not signed in to the cloud API. In Elite Pro, sign in as Admin or Manager (full admin login), then save again.");
        }

        await new AdminSettingsApiClient().PushSettingsAsync(
                settings,
                applyLogoChanges,
                applyOnlinePromoImageChanges,
                applyTicketBrandingChanges,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
