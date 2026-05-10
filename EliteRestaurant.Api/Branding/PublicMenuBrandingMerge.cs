using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Api.Branding;

internal static class PublicMenuBrandingMerge
{
    public static string RestaurantDisplayName(PublicMenuSetting? cloud, BusinessProfileSettings business)
    {
        var fileName = business.RestaurantName?.Trim();
        if (cloud is null)
            return string.IsNullOrWhiteSpace(fileName) ? "Elite Restaurant" : fileName;

        var cloudName = cloud.RestaurantName?.Trim();
        if (!string.IsNullOrWhiteSpace(cloudName))
            return cloudName;

        return string.IsNullOrWhiteSpace(fileName) ? "Elite Restaurant" : fileName!;
    }

    /// <summary>Prefer trimmed non-empty cloud values when a cloud profile exists; otherwise use file-backed strings.</summary>
    public static string? ProfileString(bool hasCloudProfile, string? cloud, string? file)
    {
        if (hasCloudProfile && !string.IsNullOrWhiteSpace(cloud))
            return cloud.Trim();

        return string.IsNullOrWhiteSpace(file) ? null : file.Trim();
    }
}
