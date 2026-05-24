using EliteRestaurant.Contracts.Setup;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Utils;

namespace EliteRestaurantPro.Services;

/// <summary>Persists cloud API URL, tenant identity, and branding pulled from the hosted site.</summary>
public static class CloudConnectionSettings
{
    public static void ApplyFromSetupStatus(AppSettings settings, string apiBaseUrl, SetupStatusDto status)
    {
        settings.CloudApi.BaseUrl = CloudEndpoints.NormalizeApiBaseUrl(apiBaseUrl);
        if (status.PrimaryRestaurantId is int restaurantId && restaurantId > 0)
            settings.CloudApi.RestaurantId = restaurantId;
        if (!string.IsNullOrWhiteSpace(status.PrimaryRestaurantSlug))
            settings.CloudApi.RestaurantSlug = status.PrimaryRestaurantSlug.Trim();
        if (!string.IsNullOrWhiteSpace(status.PrimaryRestaurantName))
            settings.BusinessProfile.RestaurantName = status.PrimaryRestaurantName.Trim();
    }

    public static void ApplyFromSiteSetup(AppSettings settings, string apiBaseUrl, SiteSetupResponse response)
    {
        settings.CloudApi.BaseUrl = CloudEndpoints.NormalizeApiBaseUrl(apiBaseUrl);
        settings.CloudApi.RestaurantId = response.RestaurantId;
        settings.CloudApi.RestaurantSlug = response.Slug.Trim();
        settings.CloudApi.AccessToken = response.AccessToken;
        settings.CloudApi.TokenExpiresAtUtc = response.ExpiresAtUtc;
    }

    public static void ApplyRestaurantIdFromAccessToken(AppSettings settings, string accessToken)
    {
        if (JwtPayloadReader.TryGetRestaurantId(accessToken, out var restaurantId) && restaurantId > 0)
            settings.CloudApi.RestaurantId = restaurantId;
    }

    public static async Task PullPublicBrandingAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var config = await new PublicMenuApiClient().TryGetConfigAsync(
            settings.CloudApi.BaseUrl,
            settings.CloudApi.RestaurantId,
            settings.CloudApi.RestaurantSlug,
            cancellationToken);
        if (config is null)
            return;

        if (!string.IsNullOrWhiteSpace(config.RestaurantName))
            settings.BusinessProfile.RestaurantName = config.RestaurantName.Trim();
        if (!string.IsNullOrWhiteSpace(config.Phone))
            settings.BusinessProfile.Phone = config.Phone.Trim();
        if (!string.IsNullOrWhiteSpace(config.Address))
            settings.BusinessProfile.Address = config.Address.Trim();
        if (!string.IsNullOrWhiteSpace(config.WebsiteDomain))
            settings.BusinessProfile.WebsiteDomain = config.WebsiteDomain.Trim();
        if (!string.IsNullOrWhiteSpace(config.SocialMedia))
            settings.BusinessProfile.SocialMedia = config.SocialMedia.Trim();
    }
}
