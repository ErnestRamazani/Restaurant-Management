using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Tickets;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Branding;

internal static class TicketReceiptSettingsMerger
{
    public static AppSettings MergeForTicketReceipt(AppSettings file, PublicMenuSetting? cloud)
    {
        if (cloud is null)
            return file;

        var hasCloudProfile = true;
        var business = file.BusinessProfile;
        business.RestaurantName = PublicMenuBrandingMerge.RestaurantDisplayName(cloud, business);
        business.Phone = PublicMenuBrandingMerge.ProfileString(hasCloudProfile, cloud.Phone, business.Phone) ?? string.Empty;
        business.Address = PublicMenuBrandingMerge.ProfileString(hasCloudProfile, cloud.Address, business.Address) ?? string.Empty;
        business.WebsiteDomain = PublicMenuBrandingMerge.ProfileString(hasCloudProfile, cloud.WebsiteDomain, business.WebsiteDomain) ?? string.Empty;
        business.TicketFooterText = PublicMenuBrandingMerge.ProfileString(hasCloudProfile, cloud.TicketFooterText, business.TicketFooterText) ?? string.Empty;
        business.TaxIdLegalInfo = PublicMenuBrandingMerge.ProfileString(hasCloudProfile, cloud.TaxIdLegalInfo, business.TaxIdLegalInfo) ?? string.Empty;
        business.RestaurantTimeZoneId = RestaurantTimeZone.NormalizeId(
            PublicMenuBrandingMerge.ProfileString(hasCloudProfile, cloud.RestaurantTimeZoneId, business.RestaurantTimeZoneId));

        file.CurrencyPricing.TaxPercent = cloud.TaxPercent;
        file.CurrencyPricing.ServicePercent = cloud.ServicePercent;

        return file;
    }
}
