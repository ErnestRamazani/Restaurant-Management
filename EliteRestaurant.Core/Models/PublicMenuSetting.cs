namespace EliteRestaurant.Core.Models;

public sealed class PublicMenuSetting
{
    public int Id { get; set; }
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
    public string StaffLoginPasscode { get; set; } = "er4124";
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

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
