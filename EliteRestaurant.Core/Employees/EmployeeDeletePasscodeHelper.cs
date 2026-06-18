using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Security;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Employees;

public static class EmployeeDeletePasscodeHelper
{
    public static string ResolveConfigured(AppDbContext db, int? restaurantId = null)
    {
        var query = db.PublicMenuSettings.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.Key == "default");
        if (restaurantId is int rid && rid > 0)
            query = query.Where(s => s.RestaurantId == rid);

        var fromDb = query.Select(s => s.EmployeeDeletePasscode).FirstOrDefault();
        var configured = string.IsNullOrWhiteSpace(fromDb)
            ? SettingsManager.Load().BusinessProfile.EmployeeDeletePasscode
            : fromDb;
        return (configured ?? string.Empty).Trim();
    }

    /// <summary>Returns null when valid; otherwise a user-facing error message.</summary>
    public static string? Validate(AppDbContext db, string? submitted)
    {
        var expected = ResolveConfigured(db);
        if (string.IsNullOrEmpty(expected))
        {
            return "Employee delete passcode is not configured. Set it in Elite Pro → Appearance → Business profile, then push to cloud.";
        }

        var pass = StaffLoginPasscodeHelper.NormalizeSubmitted(submitted);
        if (!string.Equals(pass, expected, StringComparison.Ordinal))
            return "Incorrect employee delete passcode.";

        return null;
    }
}
