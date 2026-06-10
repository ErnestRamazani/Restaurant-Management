using EliteRestaurant.Core.Tenancy;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Models;

public sealed class PublicMenuSetting : IRestaurantScoped
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public string Key { get; set; } = "default";
    public string RestaurantName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string WebsiteDomain { get; set; } = string.Empty;
    public string SocialMedia { get; set; } = string.Empty;
    public string? CustomerMenuTagline { get; set; }

    /// <summary>Public menu footer — About sheet body (plain text).</summary>
    public string? CustomerMenuAboutText { get; set; }

    /// <summary>Public menu footer — optional intro above address/phone on Contact sheet.</summary>
    public string? CustomerMenuContactIntro { get; set; }

    /// <summary>Public menu footer — Notes sheet body (plain text; may include line breaks).</summary>
    public string? CustomerMenuNotesText { get; set; }
    public string StaffLoginPasscode { get; set; } = string.Empty;

    /// <summary>Maximum open client debt (USD) before new on-account charges are blocked.</summary>
    public decimal ClientDebtCapUsd { get; set; } = 250m;

    /// <summary>Admin passcode required to cancel orders from staff portals and desktop.</summary>
    public string OrderCancelPasscode { get; set; } = string.Empty;

    /// <summary>Sign-in ID for the read-only admin web portal (<c>/admin/</c>).</summary>
    public string AdminWebSignInId { get; set; } = string.Empty;

    /// <summary>PIN for the read-only admin web portal (synced to <see cref="Employee"/> role AdminWeb).</summary>
    public string AdminWebPin { get; set; } = string.Empty;
    public string TicketFooterText { get; set; } = "MERCI / THANK YOU";
    public string TaxIdLegalInfo { get; set; } = string.Empty;
    public string DefaultCurrencyDisplayMode { get; set; } = "Dual";
    public decimal UsdToFcRate { get; set; } = 2250m;
    public string RoundingLine { get; set; } = "Nearest";
    public string RoundingSubtotal { get; set; } = "Nearest";
    public string RoundingGrandTotal { get; set; } = "Nearest";
    public decimal TaxPercent { get; set; } = 7m;
    public decimal ServicePercent { get; set; } = 10m;
    /// <summary>Optional: POS table id for guest online orders (must have assigned server in ops).</summary>
    public int? OnlineOrdersTableId { get; set; }

    /// <summary>Minimum days before a guest may reserve (public menu + floor API).</summary>
    public int ReservationLeadDays { get; set; } = 2;

    /// <summary>How far ahead guests may book (months).</summary>
    public int ReservationMaxMonthsAhead { get; set; } = 6;

    /// <summary>IANA timezone for all restaurant-facing dates (e.g. Africa/Kinshasa).</summary>
    public string RestaurantTimeZoneId { get; set; } = RestaurantTimeZone.DefaultId;

    public string? OnlinePromoTitle { get; set; }
    public string? OnlinePromoSubtitle { get; set; }
    public string? OnlinePromoCtaLabel { get; set; }

    /// <summary>JSON for <see cref="Menu.MenuTaxonomySettings"/>; exposed on public menu config for PWAs.</summary>
    public string? MenuTaxonomyJson { get; set; }

    /// <summary>Late days per payroll attendance deduction unit (legacy default 4).</summary>
    public int PayrollLateDaysPerAttendanceUnit { get; set; } = 4;

    /// <summary>Whether each absence counts as one deduction unit.</summary>
    public bool PayrollAbsenceCountsAsAttendanceUnit { get; set; } = true;

    /// <summary>Sales bonus percent on server merchandise totals (legacy default 5).</summary>
    public decimal PayrollSalesBonusPercent { get; set; } = 5m;

    /// <summary>Salary advance cap as percent of scheduled gross for the month (legacy default 30).</summary>
    public decimal PayrollMaxSalaryAdvancePercentOfGross { get; set; } = 30m;

    /// <summary>JSON array of ticket social footer rows (icons stored in <c>PublicMenuAssets</c>).</summary>
    public string? TicketSocialMediaJson { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
