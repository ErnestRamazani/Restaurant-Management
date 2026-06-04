using System.Globalization;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Localization;

public static class OrderDetailUiLocalizer
{
    public static void Apply(OrderDetailPanelViewModel panel)
    {
        panel.ViewOrderTitle = Loc.Admin("ordViewOrder", "View order");
        panel.CloseLabel = Loc.Common("close", "Close");
        panel.LinesLabel = Loc.Admin("detailSectionLineItems", "Line items");
        panel.CustomerNotesLabel = Loc.Admin("detailCustomerNotes", "Customer notes");
        panel.AllergyNotesLabel = Loc.Admin("detailAllergy", "Allergy notes");
        panel.TotalPrefix = Loc.Admin("ordDetailTotalPrefix", "Total: ");

        panel.DisplayStatus = AdminTextLocalizer.TranslateOrderStatus(panel.RawStatus);
        panel.ServerLine = string.IsNullOrWhiteSpace(panel.RawServerName)
            ? string.Empty
            : Loc.Admin("ordServerPrefix", "Server:") + " " + panel.RawServerName;

        if (!string.IsNullOrWhiteSpace(panel.RawTableCaption))
        {
            panel.DisplayTableLabel = ReportsUiLocalizer.TranslateTableCaption(panel.RawTableCaption);
        }
        else if (panel.HasTableCode)
        {
            panel.DisplayTableLabel = $"{panel.RawTableCode} · {panel.RawTableName}";
        }
        else
        {
            panel.DisplayTableLabel = Loc.Admin("ordDetailTableNumber", "Table {{num}}",
                new Dictionary<string, string>
                {
                    ["num"] = panel.RawTableNumber.ToString(CultureInfo.InvariantCulture)
                });
        }

        if (panel.RawCreatedAtUtc != default)
        {
            var fmt = Loc.Language == "fr" ? "d MMM yyyy · HH:mm" : "MMM d, yyyy · HH:mm";
            panel.DisplayCreatedText = RestaurantTimeZone.FormatUtc(
                panel.RawCreatedAtUtc,
                SettingsManager.Load().BusinessProfile.RestaurantTimeZoneId,
                fmt,
                AdminTextLocalizer.UiCulture);
        }
        else
        {
            panel.DisplayCreatedText = string.Empty;
        }

        panel.PackagingBannerLine = panel.RawPackagingRequired
            ? Loc.Admin("ordPackagingBanner", "ONLINE — PACKAGING REQUIRED")
            : string.Empty;
    }
}
