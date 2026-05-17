using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Branding;

internal static class PublicMenuReservationSettings
{
    public static (int LeadDays, int MaxMonthsAhead) Resolve(AppDbContext db)
    {
        var cloud = db.PublicMenuSettings.AsNoTracking().FirstOrDefault(s => s.Key == "default");
        if (cloud is not null)
        {
            return (
                Math.Clamp(cloud.ReservationLeadDays, 0, 30),
                Math.Clamp(cloud.ReservationMaxMonthsAhead, 1, 24));
        }

        var business = SettingsManager.Load().BusinessProfile;
        return (
            Math.Clamp(business.ReservationLeadDays, 0, 30),
            Math.Clamp(business.ReservationMaxMonthsAhead, 1, 24));
    }
}
