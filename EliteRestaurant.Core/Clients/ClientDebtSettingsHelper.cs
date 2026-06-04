using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Clients;

public static class ClientDebtSettingsHelper
{
    public static decimal ResolveDebtCapUsd(AppDbContext db)
    {
        var fromDb = db.PublicMenuSettings.AsNoTracking()
            .Where(s => s.Key == "default")
            .Select(s => s.ClientDebtCapUsd)
            .FirstOrDefault();
        if (fromDb > 0m)
            return fromDb;
        var fromFile = SettingsManager.Load().BusinessProfile.ClientDebtCapUsd;
        return fromFile > 0m ? fromFile : 250m;
    }
}
