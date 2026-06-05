using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Security;

/// <summary>Resolves the shared staff gate passcode from cloud profile (per restaurant), with a file fallback.</summary>
public static class StaffLoginPasscodeHelper
{
    /// <param name="restaurantId">When set, reads the <c>default</c> row for that restaurant only (avoids cross-tenant mix-ups).</param>
    public static string ResolveConfigured(AppDbContext db, int? restaurantId = null)
    {
        var query = db.PublicMenuSettings.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.Key == "default");

        if (restaurantId is int rid && rid > 0)
            query = query.Where(s => s.RestaurantId == rid);

        var fromDb = query.Select(s => s.StaffLoginPasscode).FirstOrDefault();
        var configured = string.IsNullOrWhiteSpace(fromDb)
            ? SettingsManager.Load().BusinessProfile.StaffLoginPasscode
            : fromDb;
        return (configured ?? string.Empty).Trim();
    }

    public static bool Matches(string? submitted, string expected)
    {
        if (string.IsNullOrEmpty(expected))
            return false;

        var pass = NormalizeSubmitted(submitted);
        return string.Equals(pass, expected, StringComparison.Ordinal);
    }

    /// <summary>Trim; strip common invisible chars from mobile keyboards.</summary>
    public static string NormalizeSubmitted(string? submitted)
    {
        var pass = (submitted ?? string.Empty).Trim();
        if (pass.Length == 0)
            return pass;

        return new string(pass.Where(c => !char.IsControl(c)).ToArray()).Trim();
    }
}
