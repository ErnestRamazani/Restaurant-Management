using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Api.Branding;

internal static class PublicMenuBrandingMerge
{
    private const string LegacyDefaultRestaurantName = "Elite Restaurant";

    public static string RestaurantDisplayName(PublicMenuSetting? cloud, BusinessProfileSettings business)
    {
        var fileName = business.RestaurantName?.Trim();
        if (cloud is null)
            return string.IsNullOrWhiteSpace(fileName) ? LegacyDefaultRestaurantName : fileName;

        var cloudName = cloud.RestaurantName?.Trim();
        if (!string.IsNullOrWhiteSpace(cloudName))
        {
            if (IsLegacyPlaceholderName(cloudName)
                && !string.IsNullOrWhiteSpace(fileName)
                && !IsLegacyPlaceholderName(fileName))
                return fileName!;

            return cloudName;
        }

        return string.IsNullOrWhiteSpace(fileName) ? LegacyDefaultRestaurantName : fileName!;
    }

    public static string? ProfileRichText(bool hasCloudProfile, string? cloud, string? file)
    {
        if (hasCloudProfile && !string.IsNullOrWhiteSpace(cloud))
            return cloud.Trim();

        return string.IsNullOrWhiteSpace(file) ? null : file.Trim();
    }

    private static bool IsLegacyPlaceholderName(string name) =>
        string.Equals(name.Trim(), LegacyDefaultRestaurantName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Prefer trimmed non-empty cloud values when a cloud profile exists; otherwise use file-backed strings.</summary>
    public static string? ProfileString(bool hasCloudProfile, string? cloud, string? file)
    {
        if (hasCloudProfile && !string.IsNullOrWhiteSpace(cloud))
            return cloud.Trim();

        return string.IsNullOrWhiteSpace(file) ? null : file.Trim();
    }
}
