using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

/// <summary>Resolves admin web portal sign-in credentials from cloud DB settings, then local app settings.</summary>
public static class AdminWebSettingsResolver
{
    public static (string SignInId, string Pin) Resolve(AppDbContext db)
    {
        var cloud = db.PublicMenuSettings.AsNoTracking()
            .FirstOrDefault(s => s.Key == "default");

        var signInId = (cloud?.AdminWebSignInId ?? string.Empty).Trim();
        var pin = (cloud?.AdminWebPin ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(signInId) && !string.IsNullOrEmpty(pin))
            return (signInId, pin);

        var business = SettingsManager.Load().BusinessProfile;
        signInId = (business.AdminWebSignInId ?? string.Empty).Trim();
        pin = (business.AdminWebPin ?? string.Empty).Trim();
        return (signInId, pin);
    }
}
