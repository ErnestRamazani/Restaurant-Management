namespace EliteRestaurantPro.Localization;

/// <summary>Settings / Paramètres screen — section keys, labels, and status messages for Elite Pro.</summary>
public static class SettingsUiLocalizer
{
    public static class SectionKeys
    {
        public const string All = "All";
        public const string BusinessProfile = "BusinessProfile";
        public const string TicketsReceipts = "TicketsReceipts";
        public const string Reservations = "Reservations";
        public const string MenuCategories = "MenuCategories";
        public const string CurrencyPricing = "CurrencyPricing";
        public const string AttendanceShifts = "AttendanceShifts";
        public const string Salary = "Salary";
        public const string MenuBackgrounds = "MenuBackgrounds";
        public const string MenuQrCodes = "MenuQrCodes";
        public const string Database = "Database";
        public const string Language = "Language";
        public const string Appearance = "Appearance";
    }

    public static readonly string[] SectionKeyOrder =
    [
        SectionKeys.All,
        SectionKeys.BusinessProfile,
        SectionKeys.TicketsReceipts,
        SectionKeys.Reservations,
        SectionKeys.MenuCategories,
        SectionKeys.CurrencyPricing,
        SectionKeys.AttendanceShifts,
        SectionKeys.Salary,
        SectionKeys.MenuBackgrounds,
        SectionKeys.MenuQrCodes,
        SectionKeys.Database,
        SectionKeys.Language,
        SectionKeys.Appearance
    ];

    public static readonly string[] BackgroundPageKeys =
    [
        "Dashboard", "Employees", "Menu", "Inventory", "Attendance", "Tables",
        "Reservations", "Orders", "CreateOrder", "Money", "Salary", "Reports",
        "KitchenQueue", "ServerPickup"
    ];

    public static readonly string[] ThemeTokenKeys =
    [
        "BackgroundDark", "BackgroundMedium", "Sidebar", "CardBase", "GoldAccent",
        "TextSecondary", "BorderSubtle", "StatBlue", "StatGreen", "StatRed"
    ];

    public static readonly string[] RoundingValues = ["Nearest", "Up", "Down", "None"];
    public static readonly string[] CurrencyDisplayValues = ["USD", "FC", "Dual"];

    public static string SectionLabel(string key) => key switch
    {
        SectionKeys.All => Loc.Admin("setSectionAll", "All"),
        SectionKeys.BusinessProfile => Loc.Admin("setSectionBusinessProfile", "Business Profile"),
        SectionKeys.TicketsReceipts => Loc.Admin("setSectionTicketsReceipts", "Tickets & receipts"),
        SectionKeys.Reservations => Loc.Admin("setSectionReservations", "Reservations"),
        SectionKeys.MenuCategories => Loc.Admin("setSectionMenuCategories", "Menu categories"),
        SectionKeys.CurrencyPricing => Loc.Admin("setSectionCurrencyPricing", "Currency & Pricing"),
        SectionKeys.AttendanceShifts => Loc.Admin("setSectionAttendanceShifts", "Attendance & shifts"),
        SectionKeys.Salary => Loc.Admin("setSectionSalary", "Salary"),
        SectionKeys.MenuBackgrounds => Loc.Admin("setSectionMenuBackgrounds", "Menu Backgrounds"),
        SectionKeys.MenuQrCodes => Loc.Admin("setSectionMenuQrCodes", "Menu QR Codes"),
        SectionKeys.Database => Loc.Admin("setSectionDatabase", "Database"),
        SectionKeys.Language => Loc.Admin("settingsLanguageTitle", "Language"),
        SectionKeys.Appearance => Loc.Admin("setSectionAppearance", "Appearance"),
        _ => key
    };

    public static string BackgroundPageLabel(string key) => key switch
    {
        "Dashboard" => Loc.Admin("setBgPageDashboard", "Dashboard"),
        "Employees" => Loc.Admin("setBgPageEmployees", "Employees"),
        "Menu" => Loc.Admin("setBgPageMenu", "Menu"),
        "Inventory" => Loc.Admin("setBgPageInventory", "Inventory"),
        "Attendance" => Loc.Admin("setBgPageAttendance", "Attendance"),
        "Tables" => Loc.Admin("setBgPageTables", "Tables"),
        "Reservations" => Loc.Admin("setBgPageReservations", "Reservations"),
        "Orders" => Loc.Admin("setBgPageOrders", "Orders"),
        "CreateOrder" => Loc.Admin("setBgPageCreateOrder", "Create Order"),
        "Money" => Loc.Admin("setBgPageMoney", "Money"),
        "Salary" => Loc.Admin("setBgPageSalary", "Salary"),
        "Reports" => Loc.Admin("setBgPageReports", "Reports"),
        "KitchenQueue" => Loc.Admin("setBgPageKitchenQueue", "Kitchen queue"),
        "ServerPickup" => Loc.Admin("setBgPageServerPickup", "Server pickup"),
        _ => key
    };

    public static string ThemeTokenLabel(string key) => key switch
    {
        "BackgroundDark" => Loc.Admin("setTokenBackgroundDark", "Background Dark"),
        "BackgroundMedium" => Loc.Admin("setTokenBackgroundMedium", "Background Medium"),
        "Sidebar" => Loc.Admin("setTokenSidebar", "Sidebar"),
        "CardBase" => Loc.Admin("setTokenCardBase", "Card Base"),
        "GoldAccent" => Loc.Admin("setTokenGoldAccent", "Gold Accent"),
        "TextSecondary" => Loc.Admin("setTokenTextSecondary", "Text Secondary"),
        "BorderSubtle" => Loc.Admin("setTokenBorderSubtle", "Border Subtle"),
        "StatBlue" => Loc.Admin("setTokenStatBlue", "Stat Blue"),
        "StatGreen" => Loc.Admin("setTokenStatGreen", "Stat Green"),
        "StatRed" => Loc.Admin("setTokenStatRed", "Stat Red"),
        _ => key
    };

    public static string RoundingLabel(string value) => value switch
    {
        "Nearest" => Loc.Admin("setRoundingNearest", "Nearest"),
        "Up" => Loc.Admin("setRoundingUp", "Up"),
        "Down" => Loc.Admin("setRoundingDown", "Down"),
        "None" => Loc.Admin("setRoundingNone", "None"),
        _ => value
    };

    public static string CurrencyDisplayLabel(string value) => value switch
    {
        "USD" => "USD",
        "FC" => "FC",
        "Dual" => Loc.Admin("setCurrencyDisplayDual", "Dual"),
        _ => value
    };

    public static string TableQrLabel(int tableNumber, string name) =>
        Loc.Admin("setQrTableLabel", "Table {{number}} — {{name}}",
            new Dictionary<string, string>
            {
                ["number"] = tableNumber.ToString(),
                ["name"] = name
            });

    // Status messages
    public static string StatusThemeCustomize() =>
        Loc.Admin("setStatusThemeCustomize", "Customize your theme colors. Use #RRGGBB or #AARRGGBB.");

    public static string StatusThemeLoaded() =>
        Loc.Admin("setStatusThemeLoaded", "Theme values loaded.");

    public static string StatusDefaultPaletteRestored() =>
        Loc.Admin("setStatusDefaultPaletteRestored", "Default palette restored and saved.");

    public static string StatusThemeApplied() =>
        Loc.Admin("setStatusThemeApplied", "Theme applied. Save to keep it after restart.");

    public static string StatusThemeSaved() =>
        Loc.Admin("setStatusThemeSaved", "Theme saved and applied.");

    public static string StatusInvalidColors() =>
        Loc.Admin("setStatusInvalidColors", "One or more colors are invalid. Use #RRGGBB or #AARRGGBB.");

    public static string StatusPickerColorInvalid() =>
        Loc.Admin("setStatusPickerColorInvalid", "Picker color is invalid.");

    public static string StatusTokenUpdated(string tokenLabel) =>
        Loc.Admin("setStatusTokenUpdated", "{{token}} updated from HSL picker. Save to keep after restart.",
            new Dictionary<string, string> { ["token"] = tokenLabel });

    public static string StatusShiftTimesInvalid() =>
        Loc.Admin("setStatusShiftTimesInvalid", "Shift times must be valid (use HH:mm, e.g. 12:00 and 18:00).");

    public static string StatusShiftEndBeforeStart() =>
        Loc.Admin("setStatusShiftEndBeforeStart", "Each shift end must be after its start.");

    public static string StatusLateGraceInvalid() =>
        Loc.Admin("setStatusLateGraceInvalid", "Late clock-in grace must be an integer from 0 to 240 (minutes).");

    public static string StatusAttendanceSaved() =>
        Loc.Admin("setStatusAttendanceSaved", "Attendance shift settings saved.");

    public static string StatusLateDaysInvalid() =>
        Loc.Admin("setStatusLateDaysInvalid", "Late days per attendance unit must be a whole number ≥ 1 (default 4).");

    public static string StatusSalesBonusInvalid() =>
        Loc.Admin("setStatusSalesBonusInvalid", "Sales bonus percent must be between 0 and 100.");

    public static string StatusAdvancePercentInvalid() =>
        Loc.Admin("setStatusAdvancePercentInvalid", "Max advance percent of gross must be between 0 and 100.");

    public static string StatusSalarySavedSyncing() =>
        Loc.Admin("setStatusSalarySavedSyncing", "Salary payroll settings saved. Syncing with API…");

    public static string StatusSavedAndPushed(string target) =>
        Loc.Admin("setStatusSavedAndPushed", "Saved and pushed to {{target}}.",
            new Dictionary<string, string> { ["target"] = target });

    public static string StatusSalarySavedAndPushed(string target) =>
        Loc.Admin("setStatusSalarySavedAndPushed", "Salary payroll settings saved and pushed to {{target}}.",
            new Dictionary<string, string> { ["target"] = target });

    public static string StatusCloudPushFailed(string error) =>
        Loc.Admin("setStatusCloudPushFailed",
            "Saved on this PC. Cloud push failed: {{error}}. Sign in as Admin, check Public menu base URL, then Save again.",
            new Dictionary<string, string> { ["error"] = error });

    public static string StatusReservationLeadDaysInvalid() =>
        Loc.Admin("setStatusReservationLeadDaysInvalid", "Reservation lead days must be a whole number (0–30).");

    public static string StatusReservationMonthsInvalid() =>
        Loc.Admin("setStatusReservationMonthsInvalid", "Reservation horizon (months) must be a whole number (1–24).");

    public static string StatusReservationSavedSyncing() =>
        Loc.Admin("setStatusReservationSavedSyncing", "Reservation settings saved on this PC. Pushing to cloud…");

    public static string StatusReservationSavedAndPushed(string target) =>
        Loc.Admin("setStatusReservationSavedAndPushed", "Reservation settings saved and pushed to {{target}}.",
            new Dictionary<string, string> { ["target"] = target });

    public static string StatusMenuTaxonomyReset() =>
        Loc.Admin("setStatusMenuTaxonomyReset",
            "Menu categories reset to Elite defaults in this screen. Click Save menu categories to persist and push.");

    public static string StatusMenuTypeNeedsName() =>
        Loc.Admin("setStatusMenuTypeNeedsName", "Each menu type needs a name.");

    public static string StatusMenuSectionNeedsName() =>
        Loc.Admin("setStatusMenuSectionNeedsName", "Each section needs a name (this is saved as the product category).");

    public static string StatusMenuTypeNeedsSection() =>
        Loc.Admin("setStatusMenuTypeNeedsSection", "Each menu type needs at least one section.");

    public static string StatusMenuTypeRequired() =>
        Loc.Admin("setStatusMenuTypeRequired", "Add at least one menu type (for example Food and Drink).");

    public static string StatusMenuTaxonomySavedSyncing() =>
        Loc.Admin("setStatusMenuTaxonomySavedSyncing", "Menu categories saved on this PC. Pushing to cloud…");

    public static string StatusMenuTaxonomySavedAndPushed(string target) =>
        Loc.Admin("setStatusMenuTaxonomySavedAndPushed", "Menu categories saved and pushed to {{target}}.",
            new Dictionary<string, string> { ["target"] = target });

    public static string StatusMenuTaxonomyPushFailed(string error) =>
        Loc.Admin("setStatusMenuTaxonomyPushFailed",
            "Menu categories saved on this PC. Cloud push failed: {{error}}. Fix API URL/token and use Save again to push.",
            new Dictionary<string, string> { ["error"] = error });

    public static string StatusBusinessProfileSavedSyncing(string target) =>
        Loc.Admin("setStatusBusinessProfileSavedSyncing", "Business profile saved on this PC. Pushing to {{target}}…",
            new Dictionary<string, string> { ["target"] = target });

    public static string StatusBusinessProfileSavedAndPushed(string target) =>
        Loc.Admin("setStatusBusinessProfileSavedAndPushed",
            "Business profile saved and pushed to {{target}}. Refresh your live menu site to see changes (git deploy alone does not copy settings).",
            new Dictionary<string, string> { ["target"] = target });

    public static string StatusBusinessProfileQrLocalhostHint() =>
        Loc.Admin("setStatusBusinessProfileQrLocalhostHint",
            " QR: localhost will not work on customers’ phones — use the hosted cloud URL and re-print QRs.");

    public static string StatusBusinessProfilePushFailed(string target, string error) =>
        Loc.Admin("setStatusBusinessProfilePushFailed",
            "Saved on this PC, but cloud push to {{target}} failed: {{error}}. Sign in as admin on the web dashboard (or ensure Cloud API token in settings), then Save Business Profile again.",
            new Dictionary<string, string> { ["target"] = target, ["error"] = error });

    public static string StatusCurrencySavedSyncing() =>
        Loc.Admin("setStatusCurrencySavedSyncing", "Currency & pricing saved on this PC. Pushing to cloud…");

    public static string StatusCurrencySavedAndPushed(string target) =>
        Loc.Admin("setStatusCurrencySavedAndPushed", "Currency & pricing saved and pushed to {{target}}.",
            new Dictionary<string, string> { ["target"] = target });

    public static string StatusExchangeRateInvalid() =>
        Loc.Admin("setStatusExchangeRateInvalid", "Exchange rate must be a positive number.");

    public static string StatusTaxPercentInvalid() =>
        Loc.Admin("setStatusTaxPercentInvalid", "Tax percent must be zero or positive.");

    public static string StatusServicePercentInvalid() =>
        Loc.Admin("setStatusServicePercentInvalid", "Service percent must be zero or positive.");

    public static string StatusTicketsSaved() =>
        Loc.Admin("setStatusTicketsSaved", "Tickets & receipts settings saved.");

    public static string StatusSelectMenuFirst() =>
        Loc.Admin("setStatusSelectMenuFirst", "Select a menu first.");

    public static string StatusBackgroundSaved(string pageLabel) =>
        Loc.Admin("setStatusBackgroundSaved", "Background saved for {{page}}.",
            new Dictionary<string, string> { ["page"] = pageLabel });

    public static string StatusBackgroundCleared(string pageLabel) =>
        Loc.Admin("setStatusBackgroundCleared", "Background cleared for {{page}}.",
            new Dictionary<string, string> { ["page"] = pageLabel });

    public static string StatusDatabaseFieldsRequired() =>
        Loc.Admin("setStatusDatabaseFieldsRequired", "Host, database name, and username are required.");

    public static string StatusLocalPostgresDisabled() =>
        Loc.Admin("setStatusLocalPostgresDisabled", "Local PostgreSQL is disabled for live data. Enter the DigitalOcean PostgreSQL host.");

    public static string StatusPasswordStorageUnavailable() =>
        Loc.Admin("setStatusPasswordStorageUnavailable",
            "Cannot store a password on this OS. Leave password blank for trust auth, or use ELITE_POSTGRES_CONNECTION.");

    public static string StatusDatabaseSaved() =>
        Loc.Admin("setStatusDatabaseSaved", "Cloud database settings saved (PostgreSQL). Restart app to apply.");

    public static string StatusApiReachable() =>
        Loc.Admin("setStatusApiReachable",
            "API reachable (sample read succeeded). The desktop uses HTTP only; data lives on the API host.");

    public static string StatusApiRequestFailed(string error) =>
        Loc.Admin("setStatusApiRequestFailed", "API request failed: {{error}}",
            new Dictionary<string, string> { ["error"] = error });

    public static string StatusLanAddressNotDetected(int port) =>
        Loc.Admin("setStatusLanAddressNotDetected",
            "Could not detect a LAN address. Enter your PC’s IP (e.g. http://192.168.1.50:{{port}}). Allow inbound TCP {{port}} for Private networks in Windows Firewall if phones cannot connect.",
            new Dictionary<string, string> { ["port"] = port.ToString() });

    public static string StatusPublicMenuUrlSet(string url) =>
        Loc.Admin("setStatusPublicMenuUrlSet",
            "Public menu URL set to {{url}}. Save Business Profile to keep it, then re-print QR labels. Phone must use the same Wi-Fi as this PC (or a routed path to it).",
            new Dictionary<string, string> { ["url"] = url });

    public static string StatusQrLocalhostMismatch(string baseUrl, string devHint) =>
        Loc.Admin("setStatusQrLocalhostMismatch",
            "Settings still list localhost, but the QR links below use {{baseUrl}} so phones on Wi-Fi can open the menu. Save that as Public menu base URL to keep it, then re-print. {{devHint}}",
            new Dictionary<string, string> { ["baseUrl"] = baseUrl, ["devHint"] = devHint });

    public static string StatusQrDevViteHint() =>
        Loc.Admin("setStatusQrDevViteHint",
            "In development, run the customer menu with npm run dev in elite-menu (Vite must use host: true) and allow that port in Windows Firewall on Private networks.");

    public static string StatusQrFirewallHint() =>
        Loc.Admin("setStatusQrFirewallHint",
            "Allow the API (static menu) port for Private networks in Windows Firewall if phones cannot connect.");

    public static string StatusQrListFailed(string error) =>
        Loc.Admin("setStatusQrListFailed", "Could not build QR list: {{error}}",
            new Dictionary<string, string> { ["error"] = error });

    public static string StatusNoTablesForQr() =>
        Loc.Admin("setStatusNoTablesForQr", "No tables in database to export.");

    public static string StatusQrPdfSaved(int pageCount) =>
        Loc.Admin("setStatusQrPdfSaved", "Saved QR PDF ({{count}} pages).",
            new Dictionary<string, string> { ["count"] = pageCount.ToString() });

    public static string StatusQrPdfFailed(string error) =>
        Loc.Admin("setStatusQrPdfFailed", "Could not create PDF: {{error}}",
            new Dictionary<string, string> { ["error"] = error });

    // File dialog titles
    public static string DialogSelectLogo() => Loc.Admin("setDialogSelectLogo", "Select Restaurant Logo");
    public static string DialogTicketHeaderLogo() =>
        Loc.Admin("setDialogTicketHeaderLogo", "Ticket header logo (above restaurant name on printed/PDF tickets)");
    public static string DialogTicketSocialIcon() =>
        Loc.Admin("setDialogTicketSocialIcon", "Icon image for this social line on tickets");
    public static string DialogOnlinePromoImage() =>
        Loc.Admin("setDialogOnlinePromoImage", "Online order hero image (public menu)");
    public static string DialogHomepageBackground() =>
        Loc.Admin("setDialogHomepageBackground", "Select Homepage Background Image");
    public static string DialogMenuBackground(string pageLabel) =>
        Loc.Admin("setDialogMenuBackground", "Select background for {{page}}",
            new Dictionary<string, string> { ["page"] = pageLabel });
    public static string DialogImageFilter() =>
        Loc.Admin("setDialogImageFilter", "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*");
    public static string DialogImageFilterBmp() =>
        Loc.Admin("setDialogImageFilterBmp",
            "Image files (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All files (*.*)|*.*");
    public static string DialogPdfFilter() => Loc.Admin("setDialogPdfFilter", "PDF document|*.pdf");
}
