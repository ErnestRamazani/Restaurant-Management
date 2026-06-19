using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EliteRestaurant.Core.Menu;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Localization;
using EliteRestaurantPro.Services;
using EliteRestaurantPro.Utils;
using Microsoft.Win32;
using QRCoder;

namespace EliteRestaurantPro.ViewModels;

public sealed class UiLanguageOption
{
    public string Code { get; init; } = "fr";
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>EliteComboBox closed state uses <see cref="object.ToString"/> instead of DisplayMemberPath.</summary>
    public override string ToString() => DisplayName;
}

public sealed class AppearanceSettingsViewModel : AdminBaseViewModel
{
    private readonly AppSettings _settings;
    private readonly AdminDataApiClient _adminData = new();

    private string _backgroundDarkHex = string.Empty;
    private string _backgroundMediumHex = string.Empty;
    private string _sidebarHex = string.Empty;
    private string _cardBaseHex = string.Empty;
    private string _goldAccentHex = string.Empty;
    private string _textSecondaryHex = string.Empty;
    private string _borderSubtleHex = string.Empty;
    private string _statBlueHex = string.Empty;
    private string _statGreenHex = string.Empty;
    private string _statRedHex = string.Empty;
    private string _statusMessage = SettingsUiLocalizer.StatusThemeCustomize();
    private string _selectedSettingsSectionKey = SettingsUiLocalizer.SectionKeys.All;
    private LocalizedSelectOption? _selectedSettingsSectionOption;
    private string _selectedTokenKey = "GoldAccent";
    private LocalizedSelectOption? _selectedTokenOption;
    private LocalizedSelectOption? _selectedBackgroundPageOption;
    private double _pickerHue;
    private double _pickerSaturation = 50;
    private double _pickerLightness = 50;
    private string _pickerHex = "#FFD8B24A";
    private bool _isSyncingPicker;
    private string _restaurantName = string.Empty;
    private string _restaurantPhone = string.Empty;
    private string _restaurantAddress = string.Empty;
    private string _restaurantWebsiteDomain = string.Empty;
    private string _restaurantSocialMedia = string.Empty;
    private string _ticketHeaderLogoPath = string.Empty;
    private string _receiptPrinterName = string.Empty;
    private string _restaurantLogoPath = string.Empty;
    private string _homepageBackgroundImagePath = string.Empty;
    private string _ticketFooterText = string.Empty;
    private string _taxIdLegalInfo = string.Empty;
    private string _defaultCurrencyDisplayMode = "Dual";
    private string _exchangeRateUsdToFc = "2250";
    private string _exchangeRateLastUpdated = string.Empty;
    private string _roundingLine = "Nearest";
    private string _roundingSubtotal = "Nearest";
    private string _roundingGrandTotal = "Nearest";
    private string _taxPercent = "7";
    private string _servicePercent = "10";
    private string _deliveryFeePercent = "20";
    private string _databaseProvider = "PostgreSql";
    private string _databaseHost = string.Empty;
    private string _databasePort = "5432";
    private string _databaseName = string.Empty;
    private string _databaseUsername = string.Empty;
    private string _pendingDatabasePassword = string.Empty;
    private bool _hasSavedDatabasePassword;
    private string _publicMenuBaseUrl = CloudEndpoints.ProductionApiBaseUrl;
    private string _customerMenuTagline = string.Empty;
    private string _customerMenuAboutText = string.Empty;
    private string _customerMenuContactIntro = string.Empty;
    private string _customerMenuNotesText = string.Empty;
    private string _staffLoginPasscode = string.Empty;
    private string _orderCancelPasscode = string.Empty;
    private string _employeeDeletePasscode = string.Empty;
    private string _adminWebSignInId = string.Empty;
    private string _adminWebPin = string.Empty;
    private string _onlinePromoTitle = string.Empty;
    private string _onlinePromoSubtitle = string.Empty;
    private string _onlinePromoCtaLabel = string.Empty;
    private string _onlinePromoImagePath = string.Empty;
    private string _onlineOrdersTableId = string.Empty;
    private string _reservationLeadDays = "2";
    private string _reservationMaxMonthsAhead = "6";
    private string _restaurantTimeZoneId = RestaurantTimeZone.DefaultId;
    private string _attendanceMorningStartText = "12:00";
    private string _attendanceMorningEndText = "18:00";
    private string _attendanceNightStartText = "18:00";
    private string _attendanceNightEndText = "23:00";
    private string _attendanceLateGraceMinutesText = "30";
    private string _salaryLateDaysPerAttendanceUnitText = "4";
    private bool _salaryAbsenceCountsAsAttendanceUnit = true;
    private string _salarySalesBonusPercentText = "5";
    private string _salaryMaxAdvancePercentOfGrossText = "30";
    private UiLanguageOption? _selectedUiLanguage;
    private bool _isApplyingUiLanguage;

    public string SettingsLanguageTitle => Loc.Admin("settingsLanguageTitle", "Language");
    public string SettingsLanguageLead => Loc.Admin("settingsLanguageLead", "Choose the language for Elite Pro screens.");
    public string SettingsLanguageLabel => Loc.Admin("settingsLanguageLabel", "Interface language");

    public string SetPanelTitle => Loc.Admin("setPanelTitle", "Settings");
    public string SetPanelTitleAccent => Loc.Admin("setPanelTitleAccent", " Panel");
    public string SetPanelSubtitle => Loc.Admin("setPanelSubtitle", "Business profile, currency & pricing, and appearance controls.");
    public string SetCloudWebsiteLabel => Loc.Admin("setCloudWebsiteLabel", "Cloud website:");
    public string SetCloudWebsiteHint => Loc.Admin("setCloudWebsiteHint", " Pushing code to GitHub deploys the API and menu app only — not your restaurant data. Each Save here must push to the Public menu base URL below (hosted API). Sign in as Admin/Manager in Elite Pro first. Menu items and photos sync when you save them on the Menu tab.");
    public string SetSectionFilterLabel => Loc.Admin("setSectionFilterLabel", "Section:");
    public string SetBusinessProfileTitle => Loc.Admin("setBusinessProfileTitle", "Business Profile");
    public string SetBusinessProfileLead => Loc.Admin("setBusinessProfileLead", "Restaurant identity, public menu URL, online promo, and staff access.");
    public string SetRestaurantNameLabel => Loc.Admin("setRestaurantName", "Restaurant name");
    public string SetPhoneLabel => Loc.Admin("setPhone", "Phone");
    public string SetAddressLabel => Loc.Admin("setAddress", "Address");
    public string SetWebsiteDomainLabel => Loc.Admin("setWebsiteDomain", "Website domain");
    public string SetOtherSocialLabel => Loc.Admin("setOtherSocial", "Other social (optional)");
    public string SetLogoPathLabel => Loc.Admin("setLogoPath", "Logo path");
    public string SetHomepageBackgroundLabel => Loc.Admin("setHomepageBackground", "Homepage background image");
    public string SetPublicMenuBaseUrlLabel => Loc.Admin("setPublicMenuBaseUrl", "Public menu base URL (QR codes for customers’ phones)");
    public string SetPublicMenuBaseUrlHint => Loc.Admin("setPublicMenuBaseUrlHint", "Set this to your live API host (e.g. https://starfish-app-owtoz.ondigitalocean.app or https://etoilegourmandekin.com if that domain serves the API). Saves push settings to that server’s database. For Wi-Fi testing only, use this PC’s LAN IP — not localhost. No trailing slash.");
    public string SetPublicMenuBaseUrlTooltip => Loc.Admin("setPublicMenuBaseUrlTooltip", "Example: https://starfish-app-owtoz.ondigitalocean.app — no trailing slash");
    public string SetUsePhoneFriendlyUrlLabel => Loc.Admin("setUsePhoneFriendlyUrl", "Use phone-friendly URL");
    public string SetCustomerMenuTaglineLabel => Loc.Admin("setCustomerMenuTagline", "Customer menu tagline (optional)");
    public string SetCustomerMenuTaglineHint => Loc.Admin("setCustomerMenuTaglineHint", "Shown under the logo on the public homepage (e.g. Cuisine moderne · Kinshasa).");
    public string SetPublicMenuAboutLabel => Loc.Admin("setPublicMenuAbout", "Public menu — About (footer sheet)");
    public string SetPublicMenuAboutHint => Loc.Admin("setPublicMenuAboutHint", "Plain text for the About link on the customer menu homepage. Saved with Business Profile and pushed to the cloud.");
    public string SetPublicMenuContactLabel => Loc.Admin("setPublicMenuContact", "Public menu — Contact intro (optional)");
    public string SetPublicMenuContactHint => Loc.Admin("setPublicMenuContactHint", "Short line above address and phone on the Contact sheet. Address and phone still come from fields above.");
    public string SetPublicMenuNotesLabel => Loc.Admin("setPublicMenuNotes", "Public menu — Notes (footer sheet)");
    public string SetPublicMenuNotesHint => Loc.Admin("setPublicMenuNotesHint", "Allergies, ordering policy, and legal notes for the Notes link. Tax / legal line can still use Tax ID field in Tickets section.");
    public string SetOnlinePromoTitle => Loc.Admin("setOnlinePromoTitle", "Online order — weekly promo (PWA)");
    public string SetOnlinePromoLead => Loc.Admin("setOnlinePromoLead", "Hero card on the dedicated Order online flow. Saved with Business Profile and pushed to the cloud menu.");
    public string SetPromoTitleLabel => Loc.Admin("setPromoTitle", "Promo title");
    public string SetPromoSubtitleLabel => Loc.Admin("setPromoSubtitle", "Promo subtitle");
    public string SetPromoCtaLabel => Loc.Admin("setPromoCta", "Button label (CTA)");
    public string SetPromoHeroImageLabel => Loc.Admin("setPromoHeroImage", "Hero image (optional)");
    public string SetOnlineOrdersTableIdLabel => Loc.Admin("setOnlineOrdersTableId", "Online orders table id (optional)");
    public string SetOnlineOrdersTableIdHint => Loc.Admin("setOnlineOrdersTableIdHint", "POS table id to attach guest online orders (must have an assigned server). Leave blank to use the first available staffed table.");
    public string SetStaffLoginPasscodeLabel => Loc.Admin("setStaffLoginPasscode", "Staff login passcode");
    public string SetStaffLoginPasscodeHint => Loc.Admin("setStaffLoginPasscodeHint", "Customers see the menu first. Staff must enter this code before the workplace chooser opens.");
    public string SetOrderCancelPasscodeLabel => Loc.Admin("setOrderCancelPasscode", "Order cancel passcode");
    public string SetOrderCancelPasscodeHint => Loc.Admin("setOrderCancelPasscodeHint", "Required when any staff member cancels an order from cashier, kitchen, bar, server, or desktop admin.");
    public string SetEmployeeDeletePasscodeLabel => Loc.Admin("setEmployeeDeletePasscode", "Employee delete passcode");
    public string SetEmployeeDeletePasscodeHint => Loc.Admin("setEmployeeDeletePasscodeHint", "Required before deleting any employee from the Employees screen.");
    public string SetAdminWebPortalLabel => Loc.Admin("setAdminWebPortal", "Admin web portal");
    public string SetAdminWebPortalHint => Loc.Admin("setAdminWebPortalHint", "Read-only owner dashboard at /admin/ on your API host. Sign-in ID and PIN are pushed to the cloud and synced to the AdminWeb employee.");
    public string SetSignInIdLabel => Loc.Admin("setSignInId", "Sign-in ID");
    public string SetPinLabel => Loc.Admin("setPin", "PIN");
    public string SetSaveBusinessProfileLabel => Loc.Admin("setSaveBusinessProfile", "Save Business Profile");
    public string SetBrowseLabel => Loc.Admin("menuBrowse", "Browse");
    public string SetClearLabel => Loc.Admin("menuClear", "Clear");
    public string SetRefreshLabel => Loc.Admin("refresh", "Refresh");
    public string SetTicketsTitle => Loc.Admin("setTicketsTitle", "Tickets & receipts");
    public string SetTicketsLead => Loc.Admin("setTicketsLead", "Printed and PDF tickets: optional header image above the restaurant name, footer and tax lines, and custom social rows (your label, text, and icon file per line). Phone is taken from Business Profile.");
    public string SetReceiptPrinterLabel => Loc.Admin("setReceiptPrinter", "Receipt printer (Windows)");
    public string SetReceiptPrinterHint => Loc.Admin("setReceiptPrinterHint", "Choose the POS printer queue (e.g. EliteRestaurant_Printer). A Windows default printer is not required.");
    public string SetTicketHeaderLogoLabel => Loc.Admin("setTicketHeaderLogo", "Ticket header logo path (not the main restaurant logo)");
    public string SetTicketHeaderLogoHint => Loc.Admin("setTicketHeaderLogoHint", "Shown centered above the restaurant name on tickets only. Leave blank to skip.");
    public string SetTicketFooterLabel => Loc.Admin("setTicketFooter", "Footer text for tickets");
    public string SetTaxIdLegalLabel => Loc.Admin("setTaxIdLegal", "Tax ID / legal info for receipts");
    public string SetTicketSocialLinesLabel => Loc.Admin("setTicketSocialLines", "Social lines on tickets");
    public string SetTicketSocialLinesHint => Loc.Admin("setTicketSocialLinesHint", "Add a row for each item: type the platform name, the username or URL to show, and pick a small icon image from disk.");
    public string SetSocialNameExampleLabel => Loc.Admin("setSocialNameExample", "Name (e.g. Instagram)");
    public string SetSocialUsernameUrlLabel => Loc.Admin("setSocialUsernameUrl", "Username / URL / text");
    public string SetSocialIconPathLabel => Loc.Admin("setSocialIconPath", "Icon image path");
    public string SetRemoveRowLabel => Loc.Admin("setRemoveRow", "Remove row");
    public string SetAddSocialRowLabel => Loc.Admin("setAddSocialRow", "Add social row");
    public string SetSaveTicketsLabel => Loc.Admin("setSaveTickets", "Save tickets & receipts");
    public string SetTimezoneTitle => Loc.Admin("setTimezoneTitle", "Restaurant timezone");
    public string SetTimezoneLead => Loc.Admin("setTimezoneLead", "All order times, reservations, guest menu confirmations, and staff web portals use this timezone — not each device's local clock. Use an IANA id (e.g. Africa/Kinshasa). Push to cloud after changing.");
    public string SetIanaTimezoneLabel => Loc.Admin("setIanaTimezone", "IANA timezone");
    public string SetReservationsTitle => Loc.Admin("setReservationsTitle", "Reservations (public menu)");
    public string SetReservationsLead => Loc.Admin("setReservationsLead", "Controls how far ahead guests can book online and how much notice is required. Saved here and pushed to the cloud API so the reservation page and server rules stay in sync.");
    public string SetReservationLeadDaysLabel => Loc.Admin("setReservationLeadDays", "Lead days (X days in advance)");
    public string SetReservationLeadDaysHint => Loc.Admin("setReservationLeadDaysHint", "Guests may choose a date starting today + this many calendar days. Use 0 to allow any future day (still not in the past).");
    public string SetReservationMaxMonthsLabel => Loc.Admin("setReservationMaxMonths", "Maximum months ahead");
    public string SetReservationMaxMonthsHint => Loc.Admin("setReservationMaxMonthsHint", "Guests cannot book beyond this many months from today.");
    public string SetSaveReservationSettingsLabel => Loc.Admin("setSaveReservationSettings", "Save reservation settings");
    public string SetMenuCategoriesTitle => Loc.Admin("setMenuCategoriesTitle", "Menu categories");
    public string SetMenuCategoriesLead => Loc.Admin("setMenuCategoriesLead", "Defines menu types (e.g. Food / Drink), product categories (saved on each item), and allowed subcategories. This drives the admin menu, customer PWA, and staff web menus after you save and push.");
    public string SetMenuCategoriesDrinkHint => Loc.Admin("setMenuCategoriesDrinkHint", "Check “Drink type” for the bucket that holds beverages. Legacy rows with Category “Drink” still match drink sections by subcategory.");
    public string SetAddMenuTypeLabel => Loc.Admin("setAddMenuType", "Add menu type");
    public string SetRestoreEliteDefaultsLabel => Loc.Admin("setRestoreEliteDefaults", "Restore Elite defaults");
    public string SetSaveMenuCategoriesLabel => Loc.Admin("setSaveMenuCategories", "Save menu categories");
    public string SetMenuTypeNameLabel => Loc.Admin("setMenuTypeName", "Menu type name");
    public string SetRemoveTypeLabel => Loc.Admin("setRemoveType", "Remove type");
    public string SetDrinkTypeCheckboxLabel => Loc.Admin("setDrinkTypeCheckbox", "Drink type (beverages)");
    public string SetSectionsHint => Loc.Admin("setSectionsHint", "Sections (product category = section name). Subcategories: comma-separated list.");
    public string SetAddSectionLabel => Loc.Admin("setAddSection", "Add section");
    public string SetCategorySectionNameLabel => Loc.Admin("setCategorySectionName", "Category (section name)");
    public string SetRemoveLabel => Loc.Admin("setRemove", "Remove");
    public string SetSubcategoriesCommaLabel => Loc.Admin("setSubcategoriesComma", "Subcategories (comma-separated)");
    public string SetMenuQrTitle => Loc.Admin("setMenuQrTitle", "Menu QR codes");
    public string SetMenuQrLead => Loc.Admin("setMenuQrLead", "Each code opens the customer menu with the table preset. URL: {base URL}/menu/?table={table id}. Save Business Profile if you change the base URL.");
    public string SetPrintAllQrPdfLabel => Loc.Admin("setPrintAllQrPdf", "Print all QR codes (PDF)");
    public string SetCurrencyTitle => Loc.Admin("setCurrencyTitle", "Currency & Pricing");
    public string SetCurrencyLead => Loc.Admin("setCurrencyLead", "Default display, exchange rate, rounding, tax and service.");
    public string SetDefaultCurrencyDisplayLabel => Loc.Admin("setDefaultCurrencyDisplay", "Default currency display mode");
    public string SetExchangeRateLabel => Loc.Admin("setExchangeRate", "USD ↔ FC exchange rate");
    public string SetExchangeRateUpdatedLabel => Loc.Admin("setExchangeRateUpdated", "Exchange rate last updated");
    public string SetRoundingLineSubtotalLabel => Loc.Admin("setRoundingLineSubtotal", "Rounding: line / subtotal");
    public string SetRoundingGrandTotalLabel => Loc.Admin("setRoundingGrandTotal", "Rounding: grand total");
    public string SetTaxServicePercentLabel => Loc.Admin("setTaxServicePercent", "Tax % / Service % (admin only)");
    public string SetDeliveryFeePercentLabel => Loc.Admin("setDeliveryFeePercent", "Delivery fee % (merchandise subtotal)");
    public string SetSaveCurrencyPricingLabel => Loc.Admin("setSaveCurrencyPricing", "Save Currency & Pricing");
    public string SetAttendanceTitle => Loc.Admin("setAttendanceTitle", "Attendance & shifts");
    public string SetAttendanceLead => Loc.Admin("setAttendanceLead", "Define shift windows (local time). Morning and night set partial shifts; Full Day on an employee schedule uses morning start through night end. Used for attendance, late grace, payroll hours, and auto-absence on this PC.");
    public string SetMorningStartLabel => Loc.Admin("setMorningStart", "Morning shift — start (HH:mm)");
    public string SetMorningEndLabel => Loc.Admin("setMorningEnd", "Morning shift — end (HH:mm)");
    public string SetNightStartLabel => Loc.Admin("setNightStart", "Night shift — start (HH:mm)");
    public string SetNightEndLabel => Loc.Admin("setNightEnd", "Night shift — end (HH:mm)");
    public string SetLateGraceLabel => Loc.Admin("setLateGrace", "Late clock-in grace (minutes after shift start)");
    public string SetSaveAttendanceLabel => Loc.Admin("setSaveAttendance", "Save attendance shift settings");
    public string SetSalaryTitle => Loc.Admin("setSalaryTitle", "Salary");
    public string SetSalaryLead => Loc.Admin("setSalaryLead", "Payroll uses scheduled workdays, attendance absences/lates, merchandise sales bonus, and advance caps. These values sync to the cloud profile so API-side payroll matches this PC.");
    public string SetLateDaysPerUnitLabel => Loc.Admin("setLateDaysPerUnit", "Late days per attendance deduction unit (e.g. 4 means four late days = one unit)");
    public string SetAbsenceCountsAsUnitLabel => Loc.Admin("setAbsenceCountsAsUnit", "Each absence day counts as one deduction unit");
    public string SetSalesBonusPercentLabel => Loc.Admin("setSalesBonusPercent", "Sales bonus (% of server merchandise for the month)");
    public string SetMaxAdvancePercentLabel => Loc.Admin("setMaxAdvancePercent", "Max salary advance (% of scheduled gross for that payroll month)");
    public string SetSaveSalaryLabel => Loc.Admin("setSaveSalary", "Save Salary settings");
    public string SetMenuBackgroundsTitle => Loc.Admin("setMenuBackgroundsTitle", "Menu Backgrounds");
    public string SetMenuBackgroundsLead => Loc.Admin("setMenuBackgroundsLead", "Set a custom image for each navigation page. Dim and contrast apply app-wide.");
    public string SetPageLabel => Loc.Admin("setPage", "Page");
    public string SetBackgroundImagePathLabel => Loc.Admin("setBackgroundImagePath", "Background image path");
    public string SetDimLightLabel => Loc.Admin("setDimLight", "Dim light: ");
    public string SetContrastLabel => Loc.Admin("setContrast", "Contrast: ");
    public string SetSaveMenuBackgroundsLabel => Loc.Admin("setSaveMenuBackgrounds", "Save Menu Background Settings");
    public string SetDatabaseTitle => Loc.Admin("setDatabaseTitle", "Database");
    public string SetDatabaseLead => Loc.Admin("setDatabaseLead", "PostgreSQL-only runtime. Password is stored with Windows DPAPI (CurrentUser). Prefer ELITE_POSTGRES_CONNECTION for services or automation.");
    public string SetProviderLabel => Loc.Admin("setProvider", "Provider");
    public string SetHostLabel => Loc.Admin("setHost", "Host");
    public string SetPortLabel => Loc.Admin("setPort", "Port");
    public string SetDatabaseNameLabel => Loc.Admin("setDatabaseName", "Database name");
    public string SetUsernameLabel => Loc.Admin("setUsername", "Username");
    public string SetPasswordLabel => Loc.Admin("setPassword", "Password");
    public string SetPasswordNotShownLabel => Loc.Admin("setPasswordNotShown", " (not shown after save)");
    public string SetPasswordAlreadySavedLabel => Loc.Admin("setPasswordAlreadySaved", "A password is already saved on this PC. Enter a new password only to change it.");
    public string SetTestConnectionLabel => Loc.Admin("setTestConnection", "Test Connection");
    public string SetSaveDatabaseLabel => Loc.Admin("setSaveDatabase", "Save Database Settings");
    public string SetAppearanceTitle => Loc.Admin("setAppearanceTitle", "Appearance");
    public string SetAppearanceLead => Loc.Admin("setAppearanceLead", "Use HSL picker, then apply the token. No live preview panel.");
    public string SetHueLabel => Loc.Admin("setHue", "Hue: ");
    public string SetSaturationLabel => Loc.Admin("setSaturation", "Saturation: ");
    public string SetLightnessLabel => Loc.Admin("setLightness", "Lightness: ");
    public string SetApplyToTokenLabel => Loc.Admin("setApplyToToken", "Apply to selected token");
    public string SetApplyThemeLabel => Loc.Admin("setApplyTheme", "Apply Theme");
    public string SetSaveThemeLabel => Loc.Admin("setSaveTheme", "Save Theme");
    public string SetResetDefaultThemeLabel => Loc.Admin("setResetDefaultTheme", "Reset Default Theme");

    public ObservableCollection<UiLanguageOption> UiLanguageOptions { get; } =
    [
        new UiLanguageOption { Code = "fr", DisplayName = "Français" },
        new UiLanguageOption { Code = "en", DisplayName = "English" }
    ];

    public UiLanguageOption? SelectedUiLanguage
    {
        get => _selectedUiLanguage;
        set
        {
            if (value is null || !SetField(ref _selectedUiLanguage, value))
                return;
            if (!_isApplyingUiLanguage)
                _ = ApplyUiLanguageAsync(value.Code);
        }
    }

    public override string ActivePage => "AppearanceSettings";

    public string BackgroundDarkHex
    {
        get => _backgroundDarkHex;
        set => SetField(ref _backgroundDarkHex, value);
    }

    public string BackgroundMediumHex
    {
        get => _backgroundMediumHex;
        set => SetField(ref _backgroundMediumHex, value);
    }

    public string SidebarHex
    {
        get => _sidebarHex;
        set => SetField(ref _sidebarHex, value);
    }

    public string CardBaseHex
    {
        get => _cardBaseHex;
        set => SetField(ref _cardBaseHex, value);
    }

    public string GoldAccentHex
    {
        get => _goldAccentHex;
        set => SetField(ref _goldAccentHex, value);
    }

    public string TextSecondaryHex
    {
        get => _textSecondaryHex;
        set => SetField(ref _textSecondaryHex, value);
    }

    public string BorderSubtleHex
    {
        get => _borderSubtleHex;
        set => SetField(ref _borderSubtleHex, value);
    }

    public string StatBlueHex
    {
        get => _statBlueHex;
        set => SetField(ref _statBlueHex, value);
    }

    public string StatGreenHex
    {
        get => _statGreenHex;
        set => SetField(ref _statGreenHex, value);
    }

    public string StatRedHex
    {
        get => _statRedHex;
        set => SetField(ref _statRedHex, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public ObservableCollection<LocalizedSelectOption> SettingsSectionOptions { get; } = new();
    public ObservableCollection<LocalizedSelectOption> BackgroundPageOptions { get; } = new();
    public ObservableCollection<LocalizedSelectOption> ThemeTokenOptions { get; } = new();
    public ObservableCollection<LocalizedSelectOption> RoundingOptions { get; } = new();
    public ObservableCollection<LocalizedSelectOption> CurrencyDisplayOptions { get; } = new();
    public ObservableCollection<string> DatabaseProviders { get; } = new(["PostgreSql"]);

    public ObservableCollection<TicketSocialMediaRowViewModel> TicketSocialMediaRows { get; } = new();

    public LocalizedSelectOption? SelectedSettingsSectionOption
    {
        get => _selectedSettingsSectionOption;
        set
        {
            if (value is null || !SetField(ref _selectedSettingsSectionOption, value))
                return;
            SelectedSettingsSectionKey = value.Value;
        }
    }

    public string SelectedSettingsSectionKey
    {
        get => _selectedSettingsSectionKey;
        set
        {
            var salaryWasVisible = _selectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.All
                || _selectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.Salary;
            if (!SetField(ref _selectedSettingsSectionKey, value))
                return;
            var salaryNowVisible = _selectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.All
                || _selectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.Salary;
            if (salaryNowVisible && !salaryWasVisible)
                RefreshSalaryFromDiskIntoViewModel();

            OnPropertyChanged(nameof(ShowBusinessSection));
            OnPropertyChanged(nameof(ShowCurrencySection));
            OnPropertyChanged(nameof(ShowMenuBackgroundSection));
            OnPropertyChanged(nameof(ShowDatabaseSection));
            OnPropertyChanged(nameof(ShowAppearanceSection));
            OnPropertyChanged(nameof(ShowLanguageSection));
            OnPropertyChanged(nameof(ShowMenuQrSection));
            OnPropertyChanged(nameof(ShowAttendanceSection));
            OnPropertyChanged(nameof(ShowReservationsSection));
            OnPropertyChanged(nameof(ShowMenuTaxonomySection));
            OnPropertyChanged(nameof(ShowTicketsReceiptSection));
            OnPropertyChanged(nameof(ShowSalarySection));
            if (ShowMenuQrSection)
                _ = RefreshMenuQrRowsAsync();
        }
    }

    public bool ShowAttendanceSection => SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.All
        || SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.AttendanceShifts;

    public bool ShowSalarySection => SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.All
        || SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.Salary;

    public bool ShowReservationsSection => SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.All
        || SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.Reservations;

    public bool ShowMenuTaxonomySection => SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.All
        || SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.MenuCategories;

    public bool ShowBusinessSection => SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.All
        || SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.BusinessProfile;

    public bool ShowCurrencySection => SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.All
        || SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.CurrencyPricing;

    public bool ShowMenuBackgroundSection => SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.All
        || SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.MenuBackgrounds;

    public bool ShowMenuQrSection => SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.All
        || SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.MenuQrCodes;

    public bool ShowDatabaseSection => SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.All
        || SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.Database;

    public bool ShowAppearanceSection => SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.All
        || SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.Appearance;

    public bool ShowLanguageSection => SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.All
        || SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.Language;

    public bool ShowTicketsReceiptSection => SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.All
        || SelectedSettingsSectionKey == SettingsUiLocalizer.SectionKeys.TicketsReceipts;

    private string _selectedBackgroundMenu = "Dashboard";
    private string _selectedMenuBackgroundPath = string.Empty;
    private double _backgroundDimStrength = 0.45;
    private double _backgroundContrastIntensity = 0.55;

    public LocalizedSelectOption? SelectedBackgroundPageOption
    {
        get => _selectedBackgroundPageOption;
        set
        {
            if (value is null || !SetField(ref _selectedBackgroundPageOption, value))
                return;
            SelectedBackgroundMenu = value.Value;
        }
    }

    public string SelectedBackgroundMenu
    {
        get => _selectedBackgroundMenu;
        set
        {
            if (!SetField(ref _selectedBackgroundMenu, value))
                return;
            LoadSelectedBackgroundPath();
        }
    }

    public string SelectedMenuBackgroundPath
    {
        get => _selectedMenuBackgroundPath;
        set => SetField(ref _selectedMenuBackgroundPath, value);
    }

    public double BackgroundDimStrength
    {
        get => _backgroundDimStrength;
        set => SetField(ref _backgroundDimStrength, value);
    }

    public double BackgroundContrastIntensity
    {
        get => _backgroundContrastIntensity;
        set => SetField(ref _backgroundContrastIntensity, value);
    }

    public bool CanEditTaxService => ShowFullAdminNav;

    public string RestaurantName
    {
        get => _restaurantName;
        set => SetField(ref _restaurantName, value);
    }

    public string RestaurantPhone
    {
        get => _restaurantPhone;
        set => SetField(ref _restaurantPhone, value);
    }

    public string RestaurantAddress
    {
        get => _restaurantAddress;
        set => SetField(ref _restaurantAddress, value);
    }

    public string RestaurantWebsiteDomain
    {
        get => _restaurantWebsiteDomain;
        set => SetField(ref _restaurantWebsiteDomain, value);
    }

    public string RestaurantSocialMedia
    {
        get => _restaurantSocialMedia;
        set => SetField(ref _restaurantSocialMedia, value);
    }

    public string TicketHeaderLogoPath
    {
        get => _ticketHeaderLogoPath;
        set => SetField(ref _ticketHeaderLogoPath, value);
    }

    public string ReceiptPrinterName
    {
        get => _receiptPrinterName;
        set => SetField(ref _receiptPrinterName, value);
    }

    public ObservableCollection<string> InstalledPrinterNames { get; } = new();

    public string RestaurantLogoPath
    {
        get => _restaurantLogoPath;
        set => SetField(ref _restaurantLogoPath, value);
    }

    public string HomepageBackgroundImagePath
    {
        get => _homepageBackgroundImagePath;
        set => SetField(ref _homepageBackgroundImagePath, value);
    }

    public string TicketFooterText
    {
        get => _ticketFooterText;
        set => SetField(ref _ticketFooterText, value);
    }

    public string TaxIdLegalInfo
    {
        get => _taxIdLegalInfo;
        set => SetField(ref _taxIdLegalInfo, value);
    }

    public string PublicMenuBaseUrl
    {
        get => _publicMenuBaseUrl;
        set
        {
            if (SetField(ref _publicMenuBaseUrl, value))
                _ = RefreshMenuQrRowsAsync();
        }
    }

    public string CustomerMenuTagline
    {
        get => _customerMenuTagline;
        set => SetField(ref _customerMenuTagline, value);
    }

    public string CustomerMenuAboutText
    {
        get => _customerMenuAboutText;
        set => SetField(ref _customerMenuAboutText, value);
    }

    public string CustomerMenuContactIntro
    {
        get => _customerMenuContactIntro;
        set => SetField(ref _customerMenuContactIntro, value);
    }

    public string CustomerMenuNotesText
    {
        get => _customerMenuNotesText;
        set => SetField(ref _customerMenuNotesText, value);
    }

    public string StaffLoginPasscode
    {
        get => _staffLoginPasscode;
        set => SetField(ref _staffLoginPasscode, value);
    }

    public string OrderCancelPasscode
    {
        get => _orderCancelPasscode;
        set => SetField(ref _orderCancelPasscode, value);
    }

    public string EmployeeDeletePasscode
    {
        get => _employeeDeletePasscode;
        set => SetField(ref _employeeDeletePasscode, value);
    }

    public string AdminWebSignInId
    {
        get => _adminWebSignInId;
        set => SetField(ref _adminWebSignInId, value);
    }

    public string AdminWebPin
    {
        get => _adminWebPin;
        set => SetField(ref _adminWebPin, value);
    }

    public string OnlinePromoTitle
    {
        get => _onlinePromoTitle;
        set => SetField(ref _onlinePromoTitle, value);
    }

    public string OnlinePromoSubtitle
    {
        get => _onlinePromoSubtitle;
        set => SetField(ref _onlinePromoSubtitle, value);
    }

    public string OnlinePromoCtaLabel
    {
        get => _onlinePromoCtaLabel;
        set => SetField(ref _onlinePromoCtaLabel, value);
    }

    public string OnlinePromoImagePath
    {
        get => _onlinePromoImagePath;
        set => SetField(ref _onlinePromoImagePath, value);
    }

    /// <summary>Optional POS table id for routing public online orders.</summary>
    public string OnlineOrdersTableId
    {
        get => _onlineOrdersTableId;
        set => SetField(ref _onlineOrdersTableId, value);
    }

    public string ReservationLeadDays
    {
        get => _reservationLeadDays;
        set => SetField(ref _reservationLeadDays, value);
    }

    public string ReservationMaxMonthsAhead
    {
        get => _reservationMaxMonthsAhead;
        set => SetField(ref _reservationMaxMonthsAhead, value);
    }

    public ObservableCollection<string> RestaurantTimeZoneOptions { get; } =
        new(RestaurantTimeZoneCatalog.CommonIds);

    public string RestaurantTimeZoneId
    {
        get => _restaurantTimeZoneId;
        set => SetField(ref _restaurantTimeZoneId, value);
    }

    public string AttendanceMorningStartText
    {
        get => _attendanceMorningStartText;
        set => SetField(ref _attendanceMorningStartText, value);
    }

    public string AttendanceMorningEndText
    {
        get => _attendanceMorningEndText;
        set => SetField(ref _attendanceMorningEndText, value);
    }

    public string AttendanceNightStartText
    {
        get => _attendanceNightStartText;
        set => SetField(ref _attendanceNightStartText, value);
    }

    public string AttendanceNightEndText
    {
        get => _attendanceNightEndText;
        set => SetField(ref _attendanceNightEndText, value);
    }

    public string AttendanceLateGraceMinutesText
    {
        get => _attendanceLateGraceMinutesText;
        set => SetField(ref _attendanceLateGraceMinutesText, value);
    }

    public string SalaryLateDaysPerAttendanceUnitText
    {
        get => _salaryLateDaysPerAttendanceUnitText;
        set => SetField(ref _salaryLateDaysPerAttendanceUnitText, value);
    }

    public bool SalaryAbsenceCountsAsAttendanceUnit
    {
        get => _salaryAbsenceCountsAsAttendanceUnit;
        set => SetField(ref _salaryAbsenceCountsAsAttendanceUnit, value);
    }

    public string SalarySalesBonusPercentText
    {
        get => _salarySalesBonusPercentText;
        set => SetField(ref _salarySalesBonusPercentText, value);
    }

    public string SalaryMaxAdvancePercentOfGrossText
    {
        get => _salaryMaxAdvancePercentOfGrossText;
        set => SetField(ref _salaryMaxAdvancePercentOfGrossText, value);
    }

    public ObservableCollection<MenuQrTableRow> MenuQrRows { get; } = new();

    public string DefaultCurrencyDisplayMode
    {
        get => _defaultCurrencyDisplayMode;
        set => SetField(ref _defaultCurrencyDisplayMode, value);
    }

    public string ExchangeRateUsdToFc
    {
        get => _exchangeRateUsdToFc;
        set => SetField(ref _exchangeRateUsdToFc, value);
    }

    public string ExchangeRateLastUpdated
    {
        get => _exchangeRateLastUpdated;
        set => SetField(ref _exchangeRateLastUpdated, value);
    }

    public string RoundingLine
    {
        get => _roundingLine;
        set => SetField(ref _roundingLine, value);
    }

    public string RoundingSubtotal
    {
        get => _roundingSubtotal;
        set => SetField(ref _roundingSubtotal, value);
    }

    public string RoundingGrandTotal
    {
        get => _roundingGrandTotal;
        set => SetField(ref _roundingGrandTotal, value);
    }

    public string TaxPercent
    {
        get => _taxPercent;
        set => SetField(ref _taxPercent, value);
    }

    public string ServicePercent
    {
        get => _servicePercent;
        set => SetField(ref _servicePercent, value);
    }

    public string DeliveryFeePercent
    {
        get => _deliveryFeePercent;
        set => SetField(ref _deliveryFeePercent, value);
    }

    public string DatabaseProvider
    {
        get => _databaseProvider;
        set => SetField(ref _databaseProvider, value);
    }

    public string DatabaseHost
    {
        get => _databaseHost;
        set => SetField(ref _databaseHost, value);
    }

    public string DatabasePort
    {
        get => _databasePort;
        set => SetField(ref _databasePort, value);
    }

    public string DatabaseName
    {
        get => _databaseName;
        set => SetField(ref _databaseName, value);
    }

    public string DatabaseUsername
    {
        get => _databaseUsername;
        set => SetField(ref _databaseUsername, value);
    }

    /// <summary>True when a DPAPI-protected password exists in settings (password is never shown).</summary>
    public bool HasSavedDatabasePassword
    {
        get => _hasSavedDatabasePassword;
        private set => SetField(ref _hasSavedDatabasePassword, value);
    }

    /// <summary>Called from PasswordBox code-behind; value is not exposed via a bindable property.</summary>
    public void SetDatabasePasswordFromUi(string password)
        => _pendingDatabasePassword = password ?? string.Empty;

    public event Action? NotifyClearDatabasePassword;

    public LocalizedSelectOption? SelectedTokenOption
    {
        get => _selectedTokenOption;
        set
        {
            if (value is null || !SetField(ref _selectedTokenOption, value))
                return;
            SelectedTokenKey = value.Value;
        }
    }

    public string SelectedTokenKey
    {
        get => _selectedTokenKey;
        set
        {
            if (!SetField(ref _selectedTokenKey, value))
                return;

            LoadTokenIntoPicker();
        }
    }

    public double PickerHue
    {
        get => _pickerHue;
        set
        {
            if (!SetField(ref _pickerHue, value))
                return;
            UpdatePickerFromHsl();
        }
    }

    public double PickerSaturation
    {
        get => _pickerSaturation;
        set
        {
            if (!SetField(ref _pickerSaturation, value))
                return;
            UpdatePickerFromHsl();
        }
    }

    public double PickerLightness
    {
        get => _pickerLightness;
        set
        {
            if (!SetField(ref _pickerLightness, value))
                return;
            UpdatePickerFromHsl();
        }
    }

    public string PickerHex
    {
        get => _pickerHex;
        set => SetField(ref _pickerHex, value);
    }

    public Brush PickerPreviewBrush
    {
        get
        {
            if (!TryNormalizeAndValidate(PickerHex, out var hex))
                return Brushes.Transparent;
            var color = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(color);
        }
    }

    public ICommand ApplyThemeCommand { get; }
    public ICommand SaveThemeCommand { get; }
    public ICommand ResetThemeCommand { get; }
    public ICommand ReloadSavedThemeCommand { get; }
    public ICommand ApplyPickerToTokenCommand { get; }
    public ICommand SaveBusinessProfileCommand { get; }
    public ICommand SaveTicketReceiptLayoutCommand { get; }
    public ICommand RefreshInstalledPrintersCommand { get; }
    public ICommand BrowseTicketHeaderLogoCommand { get; }
    public ICommand BrowseTicketSocialIconCommand { get; }
    public ICommand AddTicketSocialRowCommand { get; }
    public ICommand RemoveTicketSocialRowCommand { get; }
    public ICommand SaveCurrencyPricingCommand { get; }
    public ICommand BrowseLogoCommand { get; }
    public ICommand BrowseOnlinePromoImageCommand { get; }
    public ICommand ClearOnlinePromoImageCommand { get; }
    public ICommand BrowseHomepageBackgroundCommand { get; }
    public ICommand BrowseMenuBackgroundCommand { get; }
    public ICommand SaveMenuBackgroundCommand { get; }
    public ICommand ClearMenuBackgroundCommand { get; }
    public ICommand SaveDatabaseSettingsCommand { get; }
    public ICommand TestDatabaseConnectionCommand { get; }
    public ICommand PrintAllMenuQrCommand { get; }
    public ICommand ApplyPhoneFriendlyMenuUrlCommand { get; }
    public ICommand SaveAttendanceShiftsCommand { get; }
    public ICommand SaveSalarySettingsCommand { get; }
    public ICommand SaveReservationSettingsCommand { get; }
    public ICommand SaveMenuTaxonomyCommand { get; }
    public ICommand ResetMenuTaxonomyCommand { get; }
    public ICommand AddMenuTaxonomyTypeCommand { get; }

    public ObservableCollection<MenuTaxonomyTypeEditVm> MenuTaxonomyTypes { get; } = new();

    public AppearanceSettingsViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        _settings = SettingsManager.Load();
        ApplyThemeCommand = new RelayCommand(_ => ApplyTheme());
        SaveThemeCommand = new RelayCommand(_ => SaveTheme());
        ResetThemeCommand = new RelayCommand(_ => ResetTheme());
        ReloadSavedThemeCommand = new RelayCommand(_ => LoadFromCurrentTheme());
        ApplyPickerToTokenCommand = new RelayCommand(_ => ApplyPickerToToken());
        SaveBusinessProfileCommand = new RelayCommand(_ => _ = SaveBusinessProfileAsync());
        SaveTicketReceiptLayoutCommand = new RelayCommand(_ => SaveTicketReceiptLayout());
        RefreshInstalledPrintersCommand = new RelayCommand(_ => RefreshInstalledPrinters());
        BrowseTicketHeaderLogoCommand = new RelayCommand(_ => BrowseTicketHeaderLogo());
        BrowseTicketSocialIconCommand = new RelayCommand(o => BrowseTicketSocialIcon(o));
        AddTicketSocialRowCommand = new RelayCommand(_ => TicketSocialMediaRows.Add(new TicketSocialMediaRowViewModel()));
        RemoveTicketSocialRowCommand = new RelayCommand(o => RemoveTicketSocialRow(o));
        SaveCurrencyPricingCommand = new RelayCommand(_ => SaveCurrencyPricing());
        BrowseLogoCommand = new RelayCommand(_ => BrowseLogo());
        BrowseOnlinePromoImageCommand = new RelayCommand(_ => BrowseOnlinePromoImage());
        ClearOnlinePromoImageCommand = new RelayCommand(_ => OnlinePromoImagePath = string.Empty);
        BrowseHomepageBackgroundCommand = new RelayCommand(_ => BrowseHomepageBackground());
        BrowseMenuBackgroundCommand = new RelayCommand(_ => BrowseMenuBackground());
        SaveMenuBackgroundCommand = new RelayCommand(_ => SaveMenuBackground());
        ClearMenuBackgroundCommand = new RelayCommand(_ => ClearMenuBackground());
        SaveDatabaseSettingsCommand = new RelayCommand(_ => SaveDatabaseSettings());
        TestDatabaseConnectionCommand = new RelayCommand(_ => _ = TestDatabaseConnectionAsync());
        PrintAllMenuQrCommand = new RelayCommand(_ => _ = PrintAllMenuQrToPdfAsync());
        ApplyPhoneFriendlyMenuUrlCommand = new RelayCommand(_ => ApplyPhoneFriendlyMenuUrl());
        SaveAttendanceShiftsCommand = new RelayCommand(_ => SaveAttendanceShifts());
        SaveSalarySettingsCommand = new RelayCommand(_ => SaveSalarySettings());
        SaveReservationSettingsCommand = new RelayCommand(_ => SaveReservationSettings());
        SaveMenuTaxonomyCommand = new RelayCommand(_ => SaveMenuTaxonomy());
        ResetMenuTaxonomyCommand = new RelayCommand(_ => ResetMenuTaxonomyUi());
        AddMenuTaxonomyTypeCommand = new RelayCommand(_ => AddMenuTaxonomyType());
        LoadBusinessAndPricingSettings();
        LoadMenuTaxonomyUi();
        LoadBackgroundSettings();
        LoadDatabaseSettings();
        LoadFromCurrentTheme();
        _isApplyingUiLanguage = true;
        SelectedUiLanguage = UiLanguageOptions.FirstOrDefault(o =>
            string.Equals(o.Code, Loc.NormalizeLanguage(_settings.UiLanguage), StringComparison.OrdinalIgnoreCase))
            ?? UiLanguageOptions[0];
        _isApplyingUiLanguage = false;
        if (ShowMenuQrSection)
            _ = RefreshMenuQrRowsAsync();
        RebuildLocalizedSelectLists();
    }

    private void RebuildLocalizedSelectLists()
    {
        var sectionKey = SelectedSettingsSectionKey;
        SettingsSectionOptions.Clear();
        foreach (var key in SettingsUiLocalizer.SectionKeyOrder)
            SettingsSectionOptions.Add(new LocalizedSelectOption { Value = key, Label = SettingsUiLocalizer.SectionLabel(key) });
        SelectedSettingsSectionOption = SettingsSectionOptions.FirstOrDefault(o => o.Value == sectionKey)
            ?? SettingsSectionOptions.FirstOrDefault();

        var bgKey = SelectedBackgroundMenu;
        BackgroundPageOptions.Clear();
        foreach (var key in SettingsUiLocalizer.BackgroundPageKeys)
            BackgroundPageOptions.Add(new LocalizedSelectOption { Value = key, Label = SettingsUiLocalizer.BackgroundPageLabel(key) });
        SelectedBackgroundPageOption = BackgroundPageOptions.FirstOrDefault(o => o.Value == bgKey)
            ?? BackgroundPageOptions.FirstOrDefault();

        var tokenKey = SelectedTokenKey;
        ThemeTokenOptions.Clear();
        foreach (var key in SettingsUiLocalizer.ThemeTokenKeys)
            ThemeTokenOptions.Add(new LocalizedSelectOption { Value = key, Label = SettingsUiLocalizer.ThemeTokenLabel(key) });
        SelectedTokenOption = ThemeTokenOptions.FirstOrDefault(o => o.Value == tokenKey)
            ?? ThemeTokenOptions.FirstOrDefault();

        RoundingOptions.Clear();
        foreach (var value in SettingsUiLocalizer.RoundingValues)
            RoundingOptions.Add(new LocalizedSelectOption { Value = value, Label = SettingsUiLocalizer.RoundingLabel(value) });

        CurrencyDisplayOptions.Clear();
        foreach (var value in SettingsUiLocalizer.CurrencyDisplayValues)
            CurrencyDisplayOptions.Add(new LocalizedSelectOption { Value = value, Label = SettingsUiLocalizer.CurrencyDisplayLabel(value) });
    }

    protected override void RefreshLocalizedStrings()
    {
        base.RefreshLocalizedStrings();
        RebuildLocalizedSelectLists();
        NotifyAllSetUiProperties();
    }

    private void NotifyAllSetUiProperties()
    {
        Notify(
            nameof(SettingsLanguageTitle), nameof(SettingsLanguageLead), nameof(SettingsLanguageLabel),
            nameof(SetPanelTitle), nameof(SetPanelTitleAccent), nameof(SetPanelSubtitle),
            nameof(SetCloudWebsiteLabel), nameof(SetCloudWebsiteHint), nameof(SetSectionFilterLabel),
            nameof(SetBusinessProfileTitle), nameof(SetBusinessProfileLead),
            nameof(SetRestaurantNameLabel), nameof(SetPhoneLabel), nameof(SetAddressLabel),
            nameof(SetWebsiteDomainLabel), nameof(SetOtherSocialLabel), nameof(SetLogoPathLabel),
            nameof(SetHomepageBackgroundLabel), nameof(SetPublicMenuBaseUrlLabel), nameof(SetPublicMenuBaseUrlHint),
            nameof(SetPublicMenuBaseUrlTooltip), nameof(SetUsePhoneFriendlyUrlLabel),
            nameof(SetCustomerMenuTaglineLabel), nameof(SetCustomerMenuTaglineHint),
            nameof(SetPublicMenuAboutLabel), nameof(SetPublicMenuAboutHint),
            nameof(SetPublicMenuContactLabel), nameof(SetPublicMenuContactHint),
            nameof(SetPublicMenuNotesLabel), nameof(SetPublicMenuNotesHint),
            nameof(SetOnlinePromoTitle), nameof(SetOnlinePromoLead),
            nameof(SetPromoTitleLabel), nameof(SetPromoSubtitleLabel), nameof(SetPromoCtaLabel),
            nameof(SetPromoHeroImageLabel), nameof(SetOnlineOrdersTableIdLabel), nameof(SetOnlineOrdersTableIdHint),
            nameof(SetStaffLoginPasscodeLabel), nameof(SetStaffLoginPasscodeHint),
            nameof(SetOrderCancelPasscodeLabel), nameof(SetOrderCancelPasscodeHint),
            nameof(SetEmployeeDeletePasscodeLabel), nameof(SetEmployeeDeletePasscodeHint),
            nameof(SetAdminWebPortalLabel), nameof(SetAdminWebPortalHint),
            nameof(SetSignInIdLabel), nameof(SetPinLabel), nameof(SetSaveBusinessProfileLabel),
            nameof(SetBrowseLabel), nameof(SetClearLabel), nameof(SetRefreshLabel),
            nameof(SetTicketsTitle), nameof(SetTicketsLead), nameof(SetReceiptPrinterLabel), nameof(SetReceiptPrinterHint),
            nameof(SetTicketHeaderLogoLabel), nameof(SetTicketHeaderLogoHint),
            nameof(SetTicketFooterLabel), nameof(SetTaxIdLegalLabel),
            nameof(SetTicketSocialLinesLabel), nameof(SetTicketSocialLinesHint),
            nameof(SetSocialNameExampleLabel), nameof(SetSocialUsernameUrlLabel), nameof(SetSocialIconPathLabel),
            nameof(SetRemoveRowLabel), nameof(SetAddSocialRowLabel), nameof(SetSaveTicketsLabel),
            nameof(SetTimezoneTitle), nameof(SetTimezoneLead), nameof(SetIanaTimezoneLabel),
            nameof(SetReservationsTitle), nameof(SetReservationsLead),
            nameof(SetReservationLeadDaysLabel), nameof(SetReservationLeadDaysHint),
            nameof(SetReservationMaxMonthsLabel), nameof(SetReservationMaxMonthsHint),
            nameof(SetSaveReservationSettingsLabel),
            nameof(SetMenuCategoriesTitle), nameof(SetMenuCategoriesLead), nameof(SetMenuCategoriesDrinkHint),
            nameof(SetAddMenuTypeLabel), nameof(SetRestoreEliteDefaultsLabel), nameof(SetSaveMenuCategoriesLabel),
            nameof(SetMenuTypeNameLabel), nameof(SetRemoveTypeLabel), nameof(SetDrinkTypeCheckboxLabel),
            nameof(SetSectionsHint), nameof(SetAddSectionLabel), nameof(SetCategorySectionNameLabel),
            nameof(SetRemoveLabel), nameof(SetSubcategoriesCommaLabel),
            nameof(SetMenuQrTitle), nameof(SetMenuQrLead), nameof(SetPrintAllQrPdfLabel),
            nameof(SetCurrencyTitle), nameof(SetCurrencyLead), nameof(SetDefaultCurrencyDisplayLabel),
            nameof(SetExchangeRateLabel), nameof(SetExchangeRateUpdatedLabel),
            nameof(SetRoundingLineSubtotalLabel), nameof(SetRoundingGrandTotalLabel), nameof(SetTaxServicePercentLabel),
            nameof(SetDeliveryFeePercentLabel),
            nameof(SetSaveCurrencyPricingLabel),
            nameof(SetAttendanceTitle), nameof(SetAttendanceLead),
            nameof(SetMorningStartLabel), nameof(SetMorningEndLabel), nameof(SetNightStartLabel), nameof(SetNightEndLabel),
            nameof(SetLateGraceLabel), nameof(SetSaveAttendanceLabel),
            nameof(SetSalaryTitle), nameof(SetSalaryLead), nameof(SetLateDaysPerUnitLabel),
            nameof(SetAbsenceCountsAsUnitLabel), nameof(SetSalesBonusPercentLabel), nameof(SetMaxAdvancePercentLabel),
            nameof(SetSaveSalaryLabel),
            nameof(SetMenuBackgroundsTitle), nameof(SetMenuBackgroundsLead), nameof(SetPageLabel),
            nameof(SetBackgroundImagePathLabel), nameof(SetDimLightLabel), nameof(SetContrastLabel),
            nameof(SetSaveMenuBackgroundsLabel),
            nameof(SetDatabaseTitle), nameof(SetDatabaseLead), nameof(SetProviderLabel),
            nameof(SetHostLabel), nameof(SetPortLabel), nameof(SetDatabaseNameLabel),
            nameof(SetUsernameLabel), nameof(SetPasswordLabel), nameof(SetPasswordNotShownLabel),
            nameof(SetPasswordAlreadySavedLabel), nameof(SetTestConnectionLabel), nameof(SetSaveDatabaseLabel),
            nameof(SetAppearanceTitle), nameof(SetAppearanceLead),
            nameof(SetHueLabel), nameof(SetSaturationLabel), nameof(SetLightnessLabel),
            nameof(SetApplyToTokenLabel), nameof(SetApplyThemeLabel), nameof(SetSaveThemeLabel), nameof(SetResetDefaultThemeLabel));
    }

    private async Task ApplyUiLanguageAsync(string code)
    {
        var settings = SettingsManager.Load();
        await Loc.SetLanguageAsync(code, settings);
        _settings.UiLanguage = Loc.NormalizeLanguage(code);
    }

    private void LoadFromCurrentTheme()
    {
        var palette = ThemeManager.GetCurrentPalette();
        BackgroundDarkHex = palette.BackgroundDark;
        BackgroundMediumHex = palette.BackgroundMedium;
        SidebarHex = palette.Sidebar;
        CardBaseHex = palette.CardBase;
        GoldAccentHex = palette.GoldAccent;
        TextSecondaryHex = palette.TextSecondary;
        BorderSubtleHex = palette.BorderSubtle;
        StatBlueHex = palette.StatBlue;
        StatGreenHex = palette.StatGreen;
        StatRedHex = palette.StatRed;
        LoadTokenIntoPicker();
        StatusMessage = SettingsUiLocalizer.StatusThemeLoaded();
    }

    private void ResetTheme()
    {
        var defaults = ThemeManager.GetDefaultPalette();
        BackgroundDarkHex = defaults.BackgroundDark;
        BackgroundMediumHex = defaults.BackgroundMedium;
        SidebarHex = defaults.Sidebar;
        CardBaseHex = defaults.CardBase;
        GoldAccentHex = defaults.GoldAccent;
        TextSecondaryHex = defaults.TextSecondary;
        BorderSubtleHex = defaults.BorderSubtle;
        StatBlueHex = defaults.StatBlue;
        StatGreenHex = defaults.StatGreen;
        StatRedHex = defaults.StatRed;
        LoadTokenIntoPicker();

        ThemeManager.ApplyPalette(defaults);
        ThemeManager.SavePalette(defaults);
        StatusMessage = SettingsUiLocalizer.StatusDefaultPaletteRestored();
    }

    private void ApplyTheme()
    {
        if (!TryBuildPalette(out var palette, out var error))
        {
            StatusMessage = error;
            return;
        }

        ThemeManager.ApplyPalette(palette);
        StatusMessage = SettingsUiLocalizer.StatusThemeApplied();
    }

    private void SaveTheme()
    {
        if (!TryBuildPalette(out var palette, out var error))
        {
            StatusMessage = error;
            return;
        }

        ThemeManager.ApplyPalette(palette);
        ThemeManager.SavePalette(palette);
        StatusMessage = SettingsUiLocalizer.StatusThemeSaved();
    }

    private bool TryBuildPalette(out ThemePalette palette, out string error)
    {
        palette = new ThemePalette();
        error = string.Empty;

        if (!TryNormalizeAndValidate(BackgroundDarkHex, out var backgroundDark) ||
            !TryNormalizeAndValidate(BackgroundMediumHex, out var backgroundMedium) ||
            !TryNormalizeAndValidate(SidebarHex, out var sidebar) ||
            !TryNormalizeAndValidate(CardBaseHex, out var cardBase) ||
            !TryNormalizeAndValidate(GoldAccentHex, out var goldAccent) ||
            !TryNormalizeAndValidate(TextSecondaryHex, out var textSecondary) ||
            !TryNormalizeAndValidate(BorderSubtleHex, out var borderSubtle) ||
            !TryNormalizeAndValidate(StatBlueHex, out var statBlue) ||
            !TryNormalizeAndValidate(StatGreenHex, out var statGreen) ||
            !TryNormalizeAndValidate(StatRedHex, out var statRed))
        {
            error = SettingsUiLocalizer.StatusInvalidColors();
            return false;
        }

        palette.BackgroundDark = backgroundDark;
        palette.BackgroundMedium = backgroundMedium;
        palette.Sidebar = sidebar;
        palette.CardBase = cardBase;
        palette.GoldAccent = goldAccent;
        palette.TextSecondary = textSecondary;
        palette.BorderSubtle = borderSubtle;
        palette.StatBlue = statBlue;
        palette.StatGreen = statGreen;
        palette.StatRed = statRed;

        return true;
    }

    private void ApplyPickerToToken()
    {
        if (!TryNormalizeAndValidate(PickerHex, out var normalized))
        {
            StatusMessage = SettingsUiLocalizer.StatusPickerColorInvalid();
            return;
        }

        SetHexForToken(SelectedTokenKey, normalized);
        ApplyTheme();
        StatusMessage = SettingsUiLocalizer.StatusTokenUpdated(SettingsUiLocalizer.ThemeTokenLabel(SelectedTokenKey));
    }

    private void LoadTokenIntoPicker()
    {
        var tokenHex = GetHexForToken(SelectedTokenKey);
        if (!TryNormalizeAndValidate(tokenHex, out var normalized))
            return;

        var color = (Color)ColorConverter.ConvertFromString(normalized);
        ColorToHsl(color, out var h, out var s, out var l);

        _isSyncingPicker = true;
        _pickerHue = h;
        _pickerSaturation = s;
        _pickerLightness = l;
        _pickerHex = normalized;
        _isSyncingPicker = false;

        OnPropertyChanged(nameof(PickerHue));
        OnPropertyChanged(nameof(PickerSaturation));
        OnPropertyChanged(nameof(PickerLightness));
        OnPropertyChanged(nameof(PickerHex));
        OnPropertyChanged(nameof(PickerPreviewBrush));
    }

    private void UpdatePickerFromHsl()
    {
        if (_isSyncingPicker)
            return;

        var color = HslToColor(PickerHue, PickerSaturation, PickerLightness);
        PickerHex = color.ToString();
        OnPropertyChanged(nameof(PickerPreviewBrush));
    }

    /// <summary>Normalize id, ensure it appears in the dropdown list, and bind <see cref="RestaurantTimeZoneId"/>.</summary>
    private void ApplyRestaurantTimeZoneFromSettings(string? id)
    {
        var normalized = RestaurantTimeZone.NormalizeId(id);
        if (!RestaurantTimeZoneOptions.Contains(normalized))
            RestaurantTimeZoneOptions.Insert(0, normalized);
        RestaurantTimeZoneId = normalized;
    }

    private void LoadBusinessAndPricingSettings()
    {
        var business = _settings.BusinessProfile;
        RestaurantName = business.RestaurantName;
        RestaurantPhone = business.Phone;
        RestaurantAddress = business.Address;
        RestaurantWebsiteDomain = business.WebsiteDomain;
        RestaurantSocialMedia = business.SocialMedia;
        RestaurantLogoPath = business.LogoPath;
        HomepageBackgroundImagePath = business.HomepageBackgroundImagePath;
        TicketFooterText = business.TicketFooterText;
        TaxIdLegalInfo = business.TaxIdLegalInfo;
        PublicMenuBaseUrl = string.IsNullOrWhiteSpace(business.PublicMenuBaseUrl)
            ? (PublicMenuUrlHelper.SuggestBaseUrlForPhones() ?? CloudEndpoints.ProductionApiBaseUrl)
            : CloudEndpoints.NormalizeApiBaseUrl(business.PublicMenuBaseUrl);
        CustomerMenuTagline = business.CustomerMenuTagline ?? string.Empty;
        CustomerMenuAboutText = business.CustomerMenuAboutText ?? string.Empty;
        CustomerMenuContactIntro = business.CustomerMenuContactIntro ?? string.Empty;
        CustomerMenuNotesText = business.CustomerMenuNotesText ?? string.Empty;
        StaffLoginPasscode = business.StaffLoginPasscode?.Trim() ?? string.Empty;
        OrderCancelPasscode = business.OrderCancelPasscode?.Trim() ?? string.Empty;
        EmployeeDeletePasscode = business.EmployeeDeletePasscode?.Trim() ?? string.Empty;
        AdminWebSignInId = business.AdminWebSignInId?.Trim() ?? string.Empty;
        AdminWebPin = business.AdminWebPin?.Trim() ?? string.Empty;
        OnlinePromoTitle = business.OnlinePromoTitle ?? string.Empty;
        OnlinePromoSubtitle = business.OnlinePromoSubtitle ?? string.Empty;
        OnlinePromoCtaLabel = business.OnlinePromoCtaLabel ?? string.Empty;
        OnlinePromoImagePath = business.OnlinePromoImagePath ?? string.Empty;
        OnlineOrdersTableId = business.OnlineOrdersTableId?.ToString() ?? string.Empty;
        ReservationLeadDays = Math.Clamp(business.ReservationLeadDays, 0, 30).ToString(CultureInfo.InvariantCulture);
        ReservationMaxMonthsAhead = Math.Clamp(business.ReservationMaxMonthsAhead, 1, 24).ToString(CultureInfo.InvariantCulture);
        ApplyRestaurantTimeZoneFromSettings(business.RestaurantTimeZoneId);

        var pricing = _settings.CurrencyPricing;
        DefaultCurrencyDisplayMode = pricing.DefaultCurrencyDisplayMode;
        ExchangeRateUsdToFc = pricing.UsdToFcRate.ToString("0.##");
        ExchangeRateLastUpdated = RestaurantTimeZone.FormatUtc(
            pricing.ExchangeRateLastUpdatedUtc,
            RestaurantTimeZoneId,
            "yyyy-MM-dd HH:mm");
        RoundingLine = pricing.RoundingLine;
        RoundingSubtotal = pricing.RoundingSubtotal;
        RoundingGrandTotal = pricing.RoundingGrandTotal;
        TaxPercent = pricing.TaxPercent.ToString("0.##");
        ServicePercent = pricing.ServicePercent.ToString("0.##");
        DeliveryFeePercent = pricing.DeliveryFeePercent.ToString("0.##");
        LoadAttendanceSettings();
        LoadSalarySettings();
        LoadTicketReceiptLayout();
    }

    private void LoadTicketReceiptLayout()
    {
        _settings.TicketReceipt ??= new TicketReceiptSettings();
        TicketHeaderLogoPath = _settings.TicketReceipt.HeaderLogoPath ?? string.Empty;
        ReceiptPrinterName = _settings.TicketReceipt.ReceiptPrinterName ?? string.Empty;
        RefreshInstalledPrinters();
        if (string.IsNullOrWhiteSpace(ReceiptPrinterName))
        {
            var auto = InstalledPrinterNames.FirstOrDefault(n =>
                n.Contains("EliteRestaurant", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(auto))
                ReceiptPrinterName = auto;
        }

        TicketSocialMediaRows.Clear();
        foreach (var r in _settings.TicketReceipt.SocialMediaRows)
            TicketSocialMediaRows.Add(new TicketSocialMediaRowViewModel(r.PlatformName, r.UserText, r.IconPath));
    }

    private void RefreshInstalledPrinters()
    {
        InstalledPrinterNames.Clear();
        foreach (var name in ReceiptTicketPrintService.GetInstalledPrinterNames())
            InstalledPrinterNames.Add(name);
    }

    private void LoadAttendanceSettings()
    {
        _settings.Attendance ??= new AttendanceSettings();
        var a = _settings.Attendance;
        AttendanceMorningStartText = FormatTimeForSettings(a.MorningShiftStart);
        AttendanceMorningEndText = FormatTimeForSettings(a.MorningShiftEnd);
        AttendanceNightStartText = FormatTimeForSettings(a.NightShiftStart);
        AttendanceNightEndText = FormatTimeForSettings(a.NightShiftEnd);
        AttendanceLateGraceMinutesText = a.LateClockInGraceMinutes.ToString(CultureInfo.InvariantCulture);
    }

    private void LoadSalarySettings()
    {
        _settings.Salary ??= new SalarySettings();
        var s = _settings.Salary;
        var late = s.LateDaysPerAttendanceUnit < 1 ? 4 : s.LateDaysPerAttendanceUnit;
        SalaryLateDaysPerAttendanceUnitText = late.ToString(CultureInfo.InvariantCulture);
        SalaryAbsenceCountsAsAttendanceUnit = s.AbsenceCountsAsAttendanceUnit;
        SalarySalesBonusPercentText = s.SalesBonusPercent.ToString("0.##", CultureInfo.InvariantCulture);
        SalaryMaxAdvancePercentOfGrossText = s.MaxSalaryAdvancePercentOfGross.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// When the Salary card was hidden and is shown again, re-bind from <see cref="SettingsManager"/> so the UI
    /// matches persisted JSON (and any co-hosted API rewrite) instead of stale in-memory <see cref="AppSettings"/>.
    /// </summary>
    private void RefreshSalaryFromDiskIntoViewModel()
    {
        var disk = SettingsManager.Load();
        disk.Salary ??= new SalarySettings();
        _settings.Salary ??= new SalarySettings();
        _settings.Salary.LateDaysPerAttendanceUnit = disk.Salary.LateDaysPerAttendanceUnit;
        _settings.Salary.AbsenceCountsAsAttendanceUnit = disk.Salary.AbsenceCountsAsAttendanceUnit;
        _settings.Salary.SalesBonusPercent = disk.Salary.SalesBonusPercent;
        _settings.Salary.MaxSalaryAdvancePercentOfGross = disk.Salary.MaxSalaryAdvancePercentOfGross;
        LoadSalarySettings();
    }

    private static string FormatTimeForSettings(TimeSpan t)
    {
        var totalMinutes = (int)t.TotalMinutes % (24 * 60);
        if (totalMinutes < 0)
            totalMinutes += 24 * 60;
        var h = totalMinutes / 60;
        var m = totalMinutes % 60;
        return $"{h:00}:{m:00}";
    }

    private void SaveAttendanceShifts()
    {
        if (!TryParseWorkTime(AttendanceMorningStartText, out var mStart) ||
            !TryParseWorkTime(AttendanceMorningEndText, out var mEnd) ||
            !TryParseWorkTime(AttendanceNightStartText, out var nStart) ||
            !TryParseWorkTime(AttendanceNightEndText, out var nEnd))
        {
            StatusMessage = SettingsUiLocalizer.StatusShiftTimesInvalid();
            return;
        }

        if (mEnd <= mStart || nEnd <= nStart)
        {
            StatusMessage = SettingsUiLocalizer.StatusShiftEndBeforeStart();
            return;
        }

        if (!int.TryParse((AttendanceLateGraceMinutesText ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var grace) ||
            grace < 0 || grace > 240)
        {
            StatusMessage = SettingsUiLocalizer.StatusLateGraceInvalid();
            return;
        }

        _settings.Attendance ??= new AttendanceSettings();
        _settings.Attendance.MorningShiftStart = mStart;
        _settings.Attendance.MorningShiftEnd = mEnd;
        _settings.Attendance.NightShiftStart = nStart;
        _settings.Attendance.NightShiftEnd = nEnd;
        _settings.Attendance.LateClockInGraceMinutes = grace;
        SettingsManager.Save(_settings);
        StatusMessage = SettingsUiLocalizer.StatusAttendanceSaved();
    }

    private void SaveSalarySettings()
    {
        if (!int.TryParse((SalaryLateDaysPerAttendanceUnitText ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lateDays) ||
            lateDays < 1)
        {
            StatusMessage = SettingsUiLocalizer.StatusLateDaysInvalid();
            return;
        }

        if (!TryParseDecimalInput(SalarySalesBonusPercentText, out var bonusPct) || bonusPct < 0m || bonusPct > 100m)
        {
            StatusMessage = SettingsUiLocalizer.StatusSalesBonusInvalid();
            return;
        }

        if (!TryParseDecimalInput(SalaryMaxAdvancePercentOfGrossText, out var advancePct) || advancePct < 0m || advancePct > 100m)
        {
            StatusMessage = SettingsUiLocalizer.StatusAdvancePercentInvalid();
            return;
        }

        _settings.Salary ??= new SalarySettings();
        _settings.Salary.LateDaysPerAttendanceUnit = lateDays;
        _settings.Salary.AbsenceCountsAsAttendanceUnit = SalaryAbsenceCountsAsAttendanceUnit;
        _settings.Salary.SalesBonusPercent = Math.Round(bonusPct, 2);
        _settings.Salary.MaxSalaryAdvancePercentOfGross = Math.Round(advancePct, 2);

        SettingsManager.Save(_settings);
        _adminData.ReloadFromSettings();
        LoadSalarySettings();
        _ = PushSalarySettingsToCloudAndResyncFromDiskAsync();
        StatusMessage = SettingsUiLocalizer.StatusSalarySavedSyncing();
    }

    private async Task PushSalarySettingsToCloudAndResyncFromDiskAsync()
    {
        try
        {
            await CloudSettingsPushService.PushAsync(_settings, applyLogoChanges: false, applyOnlinePromoImageChanges: false)
                .ConfigureAwait(true);
            RefreshSalaryFromDiskIntoViewModel();
            StatusMessage = SettingsUiLocalizer.StatusSalarySavedAndPushed(CloudSettingsPushService.DescribePushTarget(_settings));
        }
        catch (Exception ex)
        {
            StatusMessage = SettingsUiLocalizer.StatusCloudPushFailed(ex.GetBaseException().Message);
        }
    }

    private void SaveReservationSettings()
    {
        if (!int.TryParse((ReservationLeadDays ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var leadDays))
        {
            StatusMessage = SettingsUiLocalizer.StatusReservationLeadDaysInvalid();
            return;
        }

        if (!int.TryParse((ReservationMaxMonthsAhead ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxMonths))
        {
            StatusMessage = SettingsUiLocalizer.StatusReservationMonthsInvalid();
            return;
        }

        _settings.BusinessProfile.ReservationLeadDays = Math.Clamp(leadDays, 0, 30);
        _settings.BusinessProfile.ReservationMaxMonthsAhead = Math.Clamp(maxMonths, 1, 24);
        _settings.BusinessProfile.RestaurantTimeZoneId = RestaurantTimeZone.NormalizeId(RestaurantTimeZoneId);
        SettingsManager.Save(_settings);
        _adminData.ReloadFromSettings();
        ReservationLeadDays = _settings.BusinessProfile.ReservationLeadDays.ToString(CultureInfo.InvariantCulture);
        ReservationMaxMonthsAhead = _settings.BusinessProfile.ReservationMaxMonthsAhead.ToString(CultureInfo.InvariantCulture);
        ApplyRestaurantTimeZoneFromSettings(_settings.BusinessProfile.RestaurantTimeZoneId);
        StatusMessage = SettingsUiLocalizer.StatusReservationSavedSyncing();
        _ = PushReservationSettingsCloudAsync();
    }

    private async Task PushReservationSettingsCloudAsync()
    {
        try
        {
            await CloudSettingsPushService.PushAsync(_settings, applyLogoChanges: false, applyOnlinePromoImageChanges: false)
                .ConfigureAwait(true);
            StatusMessage = SettingsUiLocalizer.StatusReservationSavedAndPushed(CloudSettingsPushService.DescribePushTarget(_settings));
        }
        catch (Exception ex)
        {
            StatusMessage = SettingsUiLocalizer.StatusCloudPushFailed(ex.GetBaseException().Message);
        }
    }

    private void LoadMenuTaxonomyUi()
    {
        MenuTaxonomyTypes.Clear();
        foreach (var type in MenuTaxonomyHelper.Resolve(_settings.MenuTaxonomy).Types)
        {
            var typeVm = new MenuTaxonomyTypeEditVm(this)
            {
                Name = type.Name,
                IsDrink = type.IsDrink
            };
            foreach (var sec in type.Sections)
            {
                var itemsJoined = string.Join(", ", sec.Items.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
                typeVm.Sections.Add(new MenuTaxonomySectionEditVm(this, typeVm)
                {
                    Name = sec.Name,
                    ItemsText = itemsJoined
                });
            }

            MenuTaxonomyTypes.Add(typeVm);
        }
    }

    private void ResetMenuTaxonomyUi()
    {
        _settings.MenuTaxonomy = MenuTaxonomyDefaults.CreateEliteDefault();
        LoadMenuTaxonomyUi();
        StatusMessage = SettingsUiLocalizer.StatusMenuTaxonomyReset();
    }

    private void AddMenuTaxonomyType()
    {
        var typeVm = new MenuTaxonomyTypeEditVm(this) { Name = "New type", IsDrink = false };
        typeVm.Sections.Add(new MenuTaxonomySectionEditVm(this, typeVm) { Name = "Section", ItemsText = string.Empty });
        MenuTaxonomyTypes.Add(typeVm);
    }

    internal void RemoveMenuTaxonomyType(MenuTaxonomyTypeEditVm typeVm) => MenuTaxonomyTypes.Remove(typeVm);

    internal void AddMenuTaxonomySection(MenuTaxonomyTypeEditVm typeVm) =>
        typeVm.Sections.Add(new MenuTaxonomySectionEditVm(this, typeVm) { Name = "Section", ItemsText = string.Empty });

    internal void RemoveMenuTaxonomySection(MenuTaxonomyTypeEditVm typeVm, MenuTaxonomySectionEditVm sectionVm) =>
        typeVm.Sections.Remove(sectionVm);

    private void SaveMenuTaxonomy()
    {
        var types = new List<MenuTaxonomyType>();
        foreach (var t in MenuTaxonomyTypes)
        {
            var typeName = (t.Name ?? string.Empty).Trim();
            if (typeName.Length == 0)
            {
                StatusMessage = SettingsUiLocalizer.StatusMenuTypeNeedsName();
                return;
            }

            var sections = new List<MenuTaxonomySection>();
            foreach (var s in t.Sections)
            {
                var secName = (s.Name ?? string.Empty).Trim();
                if (secName.Length == 0)
                {
                    StatusMessage = SettingsUiLocalizer.StatusMenuSectionNeedsName();
                    return;
                }

                var items = (s.ItemsText ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(x => x.Length > 0)
                    .ToList();
                sections.Add(new MenuTaxonomySection { Name = secName, Items = items });
            }

            if (sections.Count == 0)
            {
                StatusMessage = SettingsUiLocalizer.StatusMenuTypeNeedsSection();
                return;
            }

            types.Add(new MenuTaxonomyType { Name = typeName, IsDrink = t.IsDrink, Sections = sections });
        }

        if (types.Count == 0)
        {
            StatusMessage = SettingsUiLocalizer.StatusMenuTypeRequired();
            return;
        }

        _settings.MenuTaxonomy = new MenuTaxonomySettings { Types = types };
        SettingsManager.Save(_settings);
        var refreshed = SettingsManager.Load();
        _settings.MenuTaxonomy = refreshed.MenuTaxonomy;
        _adminData.ReloadFromSettings();
        LoadMenuTaxonomyUi();
        StatusMessage = SettingsUiLocalizer.StatusMenuTaxonomySavedSyncing();
        _ = PushMenuTaxonomyCloudAsync();
    }

    private async Task PushMenuTaxonomyCloudAsync()
    {
        try
        {
            await CloudSettingsPushService.PushAsync(_settings, applyLogoChanges: false, applyOnlinePromoImageChanges: false)
                .ConfigureAwait(true);
            StatusMessage = SettingsUiLocalizer.StatusMenuTaxonomySavedAndPushed(CloudSettingsPushService.DescribePushTarget(_settings));
        }
        catch (Exception ex)
        {
            StatusMessage = SettingsUiLocalizer.StatusMenuTaxonomyPushFailed(ex.GetBaseException().Message);
        }
    }

    private static bool TryParseWorkTime(string? text, out TimeSpan value)
    {
        value = default;
        var s = (text ?? string.Empty).Trim();
        if (s.Length == 0)
            return false;

        if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out value))
            return true;
        foreach (var fmt in new[] { @"hh\:mm", @"h\:mm" })
        {
            if (TimeSpan.TryParseExact(s, fmt, CultureInfo.InvariantCulture, out value))
                return true;
        }

        if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.None, out var dt))
        {
            value = dt.TimeOfDay;
            return true;
        }

        return false;
    }

    private async Task SaveBusinessProfileAsync()
    {
        _settings.BusinessProfile.RestaurantName = RestaurantName.Trim();
        _settings.BusinessProfile.Phone = RestaurantPhone.Trim();
        _settings.BusinessProfile.Address = RestaurantAddress.Trim();
        _settings.BusinessProfile.WebsiteDomain = RestaurantWebsiteDomain.Trim();
        _settings.BusinessProfile.SocialMedia = RestaurantSocialMedia.Trim();
        _settings.BusinessProfile.LogoPath = RestaurantLogoPath.Trim();
        _settings.BusinessProfile.HomepageBackgroundImagePath = HomepageBackgroundImagePath.Trim();
        _settings.BusinessProfile.PublicMenuBaseUrl = (PublicMenuBaseUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(_settings.BusinessProfile.PublicMenuBaseUrl))
            _settings.BusinessProfile.PublicMenuBaseUrl = CloudEndpoints.ProductionApiBaseUrl;
        _settings.BusinessProfile.PublicMenuBaseUrl = CloudEndpoints.NormalizeApiBaseUrl(_settings.BusinessProfile.PublicMenuBaseUrl);
        _settings.BusinessProfile.CustomerMenuTagline = string.IsNullOrWhiteSpace(CustomerMenuTagline)
            ? null
            : CustomerMenuTagline.Trim();
        _settings.BusinessProfile.CustomerMenuAboutText = string.IsNullOrWhiteSpace(CustomerMenuAboutText)
            ? null
            : CustomerMenuAboutText.Trim();
        _settings.BusinessProfile.CustomerMenuContactIntro = string.IsNullOrWhiteSpace(CustomerMenuContactIntro)
            ? null
            : CustomerMenuContactIntro.Trim();
        _settings.BusinessProfile.CustomerMenuNotesText = string.IsNullOrWhiteSpace(CustomerMenuNotesText)
            ? null
            : CustomerMenuNotesText.Trim();
        _settings.BusinessProfile.StaffLoginPasscode = (StaffLoginPasscode ?? string.Empty).Trim();
        _settings.BusinessProfile.OrderCancelPasscode = (OrderCancelPasscode ?? string.Empty).Trim();
        _settings.BusinessProfile.EmployeeDeletePasscode = (EmployeeDeletePasscode ?? string.Empty).Trim();
        _settings.BusinessProfile.AdminWebSignInId = (AdminWebSignInId ?? string.Empty).Trim();
        _settings.BusinessProfile.AdminWebPin = (AdminWebPin ?? string.Empty).Trim();
        _settings.BusinessProfile.OnlinePromoTitle = string.IsNullOrWhiteSpace(OnlinePromoTitle)
            ? null
            : OnlinePromoTitle.Trim();
        _settings.BusinessProfile.OnlinePromoSubtitle = string.IsNullOrWhiteSpace(OnlinePromoSubtitle)
            ? null
            : OnlinePromoSubtitle.Trim();
        _settings.BusinessProfile.OnlinePromoCtaLabel = string.IsNullOrWhiteSpace(OnlinePromoCtaLabel)
            ? null
            : OnlinePromoCtaLabel.Trim();
        _settings.BusinessProfile.OnlinePromoImagePath = (OnlinePromoImagePath ?? string.Empty).Trim();
        if (int.TryParse((OnlineOrdersTableId ?? string.Empty).Trim(), out var onlineTid) && onlineTid > 0)
            _settings.BusinessProfile.OnlineOrdersTableId = onlineTid;
        else
            _settings.BusinessProfile.OnlineOrdersTableId = null;

        if (!int.TryParse((ReservationLeadDays ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var leadDaysBp))
            leadDaysBp = _settings.BusinessProfile.ReservationLeadDays;
        if (!int.TryParse((ReservationMaxMonthsAhead ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxMonthsBp))
            maxMonthsBp = _settings.BusinessProfile.ReservationMaxMonthsAhead;
        _settings.BusinessProfile.ReservationLeadDays = Math.Clamp(leadDaysBp, 0, 30);
        _settings.BusinessProfile.ReservationMaxMonthsAhead = Math.Clamp(maxMonthsBp, 1, 24);
        _settings.BusinessProfile.RestaurantTimeZoneId = RestaurantTimeZone.NormalizeId(RestaurantTimeZoneId);
        ApplyRestaurantTimeZoneFromSettings(_settings.BusinessProfile.RestaurantTimeZoneId);

        if (!TryApplyCurrencyPricingFromUi(out var currencyError))
        {
            StatusMessage = currencyError;
            return;
        }

        _settings.CloudApi.BaseUrl = EliteApiClient.ResolveDesktopApiBaseUrl(_settings);

        PersistTicketReceiptLayout();

        SettingsManager.Save(_settings);
        ReloadTicketReceiptSettingsFromDisk();
        _adminData.ReloadFromSettings();
        RefreshBusinessProfileBindings();

        var pushTarget = CloudSettingsPushService.DescribePushTarget(_settings);
        StatusMessage = SettingsUiLocalizer.StatusBusinessProfileSavedSyncing(pushTarget);
        try
        {
            await CloudSettingsPushService.PushAsync(_settings, applyLogoChanges: true, applyOnlinePromoImageChanges: true)
                .ConfigureAwait(true);
            var msg = SettingsUiLocalizer.StatusBusinessProfileSavedAndPushed(pushTarget);
            if (PublicMenuUrlHelper.LooksLikeLocalHostOnly(_settings.BusinessProfile.PublicMenuBaseUrl))
                msg += SettingsUiLocalizer.StatusBusinessProfileQrLocalhostHint();
            StatusMessage = msg;
        }
        catch (Exception ex)
        {
            StatusMessage = SettingsUiLocalizer.StatusBusinessProfilePushFailed(pushTarget, ex.GetBaseException().Message);
        }
    }

    private void SaveCurrencyPricing()
    {
        if (!TryApplyCurrencyPricingFromUi(out var error))
        {
            StatusMessage = error;
            return;
        }

        _settings.CloudApi.BaseUrl = EliteApiClient.ResolveDesktopApiBaseUrl(_settings);

        SettingsManager.Save(_settings);
        _adminData.ReloadFromSettings();
        StatusMessage = SettingsUiLocalizer.StatusCurrencySavedSyncing();
        _ = PushCurrencyPricingCloudAsync();
    }

    private async Task PushCurrencyPricingCloudAsync()
    {
        try
        {
            await CloudSettingsPushService.PushAsync(_settings, applyLogoChanges: false, applyOnlinePromoImageChanges: false)
                .ConfigureAwait(true);
            StatusMessage = SettingsUiLocalizer.StatusCurrencySavedAndPushed(CloudSettingsPushService.DescribePushTarget(_settings));
        }
        catch (Exception ex)
        {
            StatusMessage = SettingsUiLocalizer.StatusCloudPushFailed(ex.GetBaseException().Message);
        }
    }

    private bool TryApplyCurrencyPricingFromUi(out string errorMessage)
    {
        if (!TryParseDecimalInput(ExchangeRateUsdToFc, out var rate) || rate <= 0)
        {
            errorMessage = SettingsUiLocalizer.StatusExchangeRateInvalid();
            return false;
        }

        if (!TryParseDecimalInput(TaxPercent, out var tax) || tax < 0)
        {
            errorMessage = SettingsUiLocalizer.StatusTaxPercentInvalid();
            return false;
        }

        if (!TryParseDecimalInput(ServicePercent, out var service) || service < 0)
        {
            errorMessage = SettingsUiLocalizer.StatusServicePercentInvalid();
            return false;
        }

        if (!TryParseDecimalInput(DeliveryFeePercent, out var deliveryFee) || deliveryFee < 0 || deliveryFee > 100)
        {
            errorMessage = Loc.Admin("setDeliveryFeePercentInvalid", "Enter a delivery fee percent between 0 and 100.");
            return false;
        }

        _settings.CurrencyPricing.DefaultCurrencyDisplayMode = DefaultCurrencyDisplayMode;
        _settings.CurrencyPricing.UsdToFcRate = rate;
        _settings.CurrencyPricing.ExchangeRateLastUpdatedUtc = DateTime.UtcNow;
        _settings.CurrencyPricing.RoundingLine = RoundingLine;
        _settings.CurrencyPricing.RoundingSubtotal = RoundingSubtotal;
        _settings.CurrencyPricing.RoundingGrandTotal = RoundingGrandTotal;
        _settings.CurrencyPricing.TaxPercent = tax;
        _settings.CurrencyPricing.ServicePercent = service;
        _settings.CurrencyPricing.DeliveryFeePercent = deliveryFee;
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryParseDecimalInput(string input, out decimal value)
    {
        var normalized = (input ?? string.Empty).Trim().Replace(',', '.');
        return decimal.TryParse(
            normalized,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);
    }

    private void BrowseLogo()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = SettingsUiLocalizer.DialogSelectLogo(),
            Filter = SettingsUiLocalizer.DialogImageFilter()
        };

        if (dialog.ShowDialog() == true)
            RestaurantLogoPath = dialog.FileName;
    }

    private void PersistTicketReceiptLayout()
    {
        _settings.TicketReceipt ??= new TicketReceiptSettings();
        _settings.TicketReceipt.HeaderLogoPath = (TicketHeaderLogoPath ?? string.Empty).Trim();
        _settings.TicketReceipt.ReceiptPrinterName = (ReceiptPrinterName ?? string.Empty).Trim();
        _settings.TicketReceipt.SocialMediaRows = TicketSocialMediaRows
            .Select(vm => new TicketSocialMediaRowSettings
            {
                PlatformName = (vm.PlatformName ?? string.Empty).Trim(),
                UserText = (vm.UserText ?? string.Empty).Trim(),
                IconPath = (vm.IconPath ?? string.Empty).Trim()
            })
            .ToList();
    }

    private void SaveTicketReceiptLayout()
    {
        _settings.BusinessProfile.TicketFooterText = (TicketFooterText ?? string.Empty).Trim();
        _settings.BusinessProfile.TaxIdLegalInfo = (TaxIdLegalInfo ?? string.Empty).Trim();
        PersistTicketReceiptLayout();
        _settings.CloudApi.BaseUrl = CloudEndpoints.NormalizeApiBaseUrl(_settings.BusinessProfile.PublicMenuBaseUrl);
        SettingsManager.Save(_settings);
        ReloadTicketReceiptSettingsFromDisk();
        _adminData.ReloadFromSettings();
        _ = CloudSettingsPushService.PushAsync(
            _settings,
            applyLogoChanges: false,
            applyOnlinePromoImageChanges: false,
            applyTicketBrandingChanges: true);
        RefreshBusinessProfileBindings();
        StatusMessage = SettingsUiLocalizer.StatusTicketsSaved();
    }

    private void ReloadTicketReceiptSettingsFromDisk()
    {
        var disk = SettingsManager.Load();
        _settings.BusinessProfile.TicketFooterText = disk.BusinessProfile.TicketFooterText;
        _settings.BusinessProfile.TaxIdLegalInfo = disk.BusinessProfile.TaxIdLegalInfo;
        _settings.TicketReceipt = disk.TicketReceipt ?? new TicketReceiptSettings();
        TicketFooterText = _settings.BusinessProfile.TicketFooterText;
        TaxIdLegalInfo = _settings.BusinessProfile.TaxIdLegalInfo;
        LoadTicketReceiptLayout();
    }

    private void BrowseTicketHeaderLogo()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = SettingsUiLocalizer.DialogTicketHeaderLogo(),
            Filter = SettingsUiLocalizer.DialogImageFilterBmp()
        };

        if (dialog.ShowDialog() == true)
            TicketHeaderLogoPath = dialog.FileName;
    }

    private void BrowseTicketSocialIcon(object? parameter)
    {
        if (parameter is not TicketSocialMediaRowViewModel row)
            return;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = SettingsUiLocalizer.DialogTicketSocialIcon(),
            Filter = SettingsUiLocalizer.DialogImageFilterBmp()
        };
        if (dialog.ShowDialog() == true)
            row.IconPath = dialog.FileName;
    }

    private void RemoveTicketSocialRow(object? parameter)
    {
        if (parameter is TicketSocialMediaRowViewModel row)
            TicketSocialMediaRows.Remove(row);
    }

    private void BrowseOnlinePromoImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = SettingsUiLocalizer.DialogOnlinePromoImage(),
            Filter = SettingsUiLocalizer.DialogImageFilter()
        };

        if (dialog.ShowDialog() == true)
            OnlinePromoImagePath = dialog.FileName;
    }

    private void BrowseHomepageBackground()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = SettingsUiLocalizer.DialogHomepageBackground(),
            Filter = SettingsUiLocalizer.DialogImageFilter()
        };

        if (dialog.ShowDialog() == true)
            HomepageBackgroundImagePath = dialog.FileName;
    }

    private void LoadBackgroundSettings()
    {
        var nav = _settings.NavigationBackgrounds;
        BackgroundDimStrength = Math.Clamp(nav.DimStrength, 0, 0.5);
        BackgroundContrastIntensity = Math.Clamp(nav.ContrastIntensity, 0, 0.5);
        LoadSelectedBackgroundPath();
    }

    private void LoadSelectedBackgroundPath()
    {
        if (_settings.NavigationBackgrounds.PageImagePaths.TryGetValue(SelectedBackgroundMenu, out var path))
            SelectedMenuBackgroundPath = path;
        else
            SelectedMenuBackgroundPath = string.Empty;
    }

    private void BrowseMenuBackground()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = SettingsUiLocalizer.DialogMenuBackground(SettingsUiLocalizer.BackgroundPageLabel(SelectedBackgroundMenu)),
            Filter = SettingsUiLocalizer.DialogImageFilter()
        };
        if (dialog.ShowDialog() == true)
            SelectedMenuBackgroundPath = dialog.FileName;
    }

    private void SaveMenuBackground()
    {
        var key = SelectedBackgroundMenu.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            StatusMessage = SettingsUiLocalizer.StatusSelectMenuFirst();
            return;
        }

        if (!string.IsNullOrWhiteSpace(SelectedMenuBackgroundPath))
            _settings.NavigationBackgrounds.PageImagePaths[key] = SelectedMenuBackgroundPath.Trim();
        else if (_settings.NavigationBackgrounds.PageImagePaths.ContainsKey(key))
            _settings.NavigationBackgrounds.PageImagePaths.Remove(key);

        _settings.NavigationBackgrounds.DimStrength = Math.Clamp(BackgroundDimStrength, 0, 0.5);
        _settings.NavigationBackgrounds.ContrastIntensity = Math.Clamp(BackgroundContrastIntensity, 0, 0.5);
        SettingsManager.Save(_settings);
        StatusMessage = SettingsUiLocalizer.StatusBackgroundSaved(SettingsUiLocalizer.BackgroundPageLabel(key));
    }

    private void ClearMenuBackground()
    {
        var key = SelectedBackgroundMenu.Trim();
        if (_settings.NavigationBackgrounds.PageImagePaths.ContainsKey(key))
            _settings.NavigationBackgrounds.PageImagePaths.Remove(key);
        SelectedMenuBackgroundPath = string.Empty;
        SettingsManager.Save(_settings);
        StatusMessage = SettingsUiLocalizer.StatusBackgroundCleared(SettingsUiLocalizer.BackgroundPageLabel(key));
    }

    private void LoadDatabaseSettings()
    {
        var db = _settings.Database ?? new DatabaseSettings();
        DatabaseProvider = string.IsNullOrWhiteSpace(db.Provider) ? "PostgreSql" : db.Provider.Trim();

        if (!string.IsNullOrWhiteSpace(db.PostgreSqlHost))
        {
            DatabaseHost = db.PostgreSqlHost.Trim();
            DatabasePort = db.PostgreSqlPort > 0 ? db.PostgreSqlPort.ToString() : "5432";
            DatabaseName = db.PostgreSqlDatabase?.Trim() ?? string.Empty;
            DatabaseUsername = db.PostgreSqlUsername?.Trim() ?? string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(db.PostgreSqlConnectionString))
        {
            DatabaseHost = string.Empty;
            DatabasePort = "5432";
            DatabaseName = string.Empty;
            DatabaseUsername = string.Empty;
            foreach (var part in db.PostgreSqlConnectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var eq = part.IndexOf('=');
                if (eq <= 0) continue;
                var key = part[..eq].Trim();
                var val = part[(eq + 1)..].Trim();
                if (key.Equals("Host", StringComparison.OrdinalIgnoreCase) && val.Length > 0)
                    DatabaseHost = val;
                else if (key.Equals("Port", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out var parsedPort))
                    DatabasePort = parsedPort > 0 ? parsedPort.ToString() : "5432";
                else if (key.Equals("Database", StringComparison.OrdinalIgnoreCase) && val.Length > 0)
                    DatabaseName = val;
                else if ((key.Equals("Username", StringComparison.OrdinalIgnoreCase) || key.Equals("User ID", StringComparison.OrdinalIgnoreCase)) && val.Length > 0)
                    DatabaseUsername = val;
            }
        }
        else
        {
            DatabaseHost = string.Empty;
            DatabasePort = "5432";
            DatabaseName = string.Empty;
            DatabaseUsername = string.Empty;
        }

        HasSavedDatabasePassword = !string.IsNullOrWhiteSpace(db.PostgreSqlPasswordProtected);
        _pendingDatabasePassword = string.Empty;
    }

    private void SaveDatabaseSettings()
    {
        var host = (DatabaseHost ?? string.Empty).Trim();
        var database = (DatabaseName ?? string.Empty).Trim();
        var username = (DatabaseUsername ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(username))
        {
            StatusMessage = SettingsUiLocalizer.StatusDatabaseFieldsRequired();
            return;
        }

        if (IsLocalDatabaseHost(host))
        {
            StatusMessage = SettingsUiLocalizer.StatusLocalPostgresDisabled();
            return;
        }

        if (!string.IsNullOrEmpty(_pendingDatabasePassword) && !DatabaseConnectionSecret.IsDpapiAvailable)
        {
            StatusMessage = SettingsUiLocalizer.StatusPasswordStorageUnavailable();
            return;
        }

        _settings.Database.Provider = "PostgreSql";
        _settings.Database.PostgreSqlHost = host;
        _settings.Database.PostgreSqlPort = int.TryParse((DatabasePort ?? string.Empty).Trim(), out var p) ? p : 5432;
        _settings.Database.PostgreSqlDatabase = database;
        _settings.Database.PostgreSqlUsername = username;

        if (!string.IsNullOrEmpty(_pendingDatabasePassword))
            _settings.Database.PostgreSqlPasswordProtected = DatabaseConnectionSecret.ProtectUtf8(_pendingDatabasePassword);

        _settings.Database.PostgreSqlConnectionString = null;
        SettingsManager.Save(_settings);
        HasSavedDatabasePassword = !string.IsNullOrWhiteSpace(_settings.Database.PostgreSqlPasswordProtected);
        _pendingDatabasePassword = string.Empty;
        NotifyClearDatabasePassword?.Invoke();
        StatusMessage = SettingsUiLocalizer.StatusDatabaseSaved();
    }

    private static bool IsLocalDatabaseHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || host.Equals("::1", StringComparison.OrdinalIgnoreCase);

    private async Task TestDatabaseConnectionAsync()
    {
        try
        {
            _ = await _adminData.GetProductsAsync().ConfigureAwait(true);
            StatusMessage = SettingsUiLocalizer.StatusApiReachable();
        }
        catch (Exception ex)
        {
            StatusMessage = SettingsUiLocalizer.StatusApiRequestFailed(ex.GetBaseException().Message);
        }
    }

    private string BuildConnectionStringForTest()
    {
        var host = (DatabaseHost ?? string.Empty).Trim();
        var database = (DatabaseName ?? string.Empty).Trim();
        var username = (DatabaseUsername ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(username))
            return string.Empty;

        var port = int.TryParse((DatabasePort ?? string.Empty).Trim(), out var p) ? p : 5432;

        var password = _pendingDatabasePassword;
        if (string.IsNullOrEmpty(password))
        {
            var prot = _settings.Database.PostgreSqlPasswordProtected?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(prot))
                return string.Empty;
            if (!DatabaseConnectionSecret.IsDpapiAvailable)
                return string.Empty;
            try
            {
                password = DatabaseConnectionSecret.UnprotectUtf8(prot);
            }
            catch
            {
                return string.Empty;
            }
        }

        return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }

    private string GetHexForToken(string token)
    {
        return token switch
        {
            "BackgroundDark" => BackgroundDarkHex,
            "BackgroundMedium" => BackgroundMediumHex,
            "Sidebar" => SidebarHex,
            "CardBase" => CardBaseHex,
            "GoldAccent" => GoldAccentHex,
            "TextSecondary" => TextSecondaryHex,
            "BorderSubtle" => BorderSubtleHex,
            "StatBlue" => StatBlueHex,
            "StatGreen" => StatGreenHex,
            "StatRed" => StatRedHex,
            _ => GoldAccentHex
        };
    }

    private void SetHexForToken(string token, string hex)
    {
        switch (token)
        {
            case "BackgroundDark":
                BackgroundDarkHex = hex;
                break;
            case "BackgroundMedium":
                BackgroundMediumHex = hex;
                break;
            case "Sidebar":
                SidebarHex = hex;
                break;
            case "CardBase":
                CardBaseHex = hex;
                break;
            case "GoldAccent":
                GoldAccentHex = hex;
                break;
            case "TextSecondary":
                TextSecondaryHex = hex;
                break;
            case "BorderSubtle":
                BorderSubtleHex = hex;
                break;
            case "StatBlue":
                StatBlueHex = hex;
                break;
            case "StatGreen":
                StatGreenHex = hex;
                break;
            case "StatRed":
                StatRedHex = hex;
                break;
        }
    }

    private static Color HslToColor(double h, double sPercent, double lPercent)
    {
        var s = Math.Clamp(sPercent / 100.0, 0, 1);
        var l = Math.Clamp(lPercent / 100.0, 0, 1);
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs((h / 60.0 % 2) - 1));
        var m = l - c / 2;

        double r1, g1, b1;
        if (h < 60) { r1 = c; g1 = x; b1 = 0; }
        else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
        else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
        else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
        else { r1 = c; g1 = 0; b1 = x; }

        return Color.FromRgb(
            (byte)Math.Round((r1 + m) * 255),
            (byte)Math.Round((g1 + m) * 255),
            (byte)Math.Round((b1 + m) * 255));
    }

    private static void ColorToHsl(Color color, out double h, out double s, out double l)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        l = (max + min) / 2.0;
        if (Math.Abs(delta) < 0.000001)
        {
            h = 0;
            s = 0;
            l *= 100;
            return;
        }

        s = delta / (1 - Math.Abs(2 * l - 1));

        if (Math.Abs(max - r) < 0.000001)
            h = 60 * (((g - b) / delta) % 6);
        else if (Math.Abs(max - g) < 0.000001)
            h = 60 * (((b - r) / delta) + 2);
        else
            h = 60 * (((r - g) / delta) + 4);

        if (h < 0)
            h += 360;

        s *= 100;
        l *= 100;
    }

    private static bool TryNormalizeAndValidate(string input, out string normalized)
    {
        normalized = ThemeManager.NormalizeHex(input);
        try
        {
            _ = (Color)ColorConverter.ConvertFromString(normalized);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyPhoneFriendlyMenuUrl()
    {
        var suggested = PublicMenuUrlHelper.SuggestBaseUrlForPhones();
        if (string.IsNullOrEmpty(suggested))
        {
            var p = PublicMenuUrlHelper.QrBasePort;
            StatusMessage = SettingsUiLocalizer.StatusLanAddressNotDetected(p);
            return;
        }

        PublicMenuBaseUrl = suggested;
        _ = RefreshMenuQrRowsAsync();
        StatusMessage = SettingsUiLocalizer.StatusPublicMenuUrlSet(suggested);
    }

    private async Task RefreshMenuQrRowsAsync()
    {
        MenuQrRows.Clear();
        try
        {
            var baseUrl = PublicMenuUrlHelper.ResolveQrBaseUrl(PublicMenuBaseUrl);
            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = PublicMenuUrlHelper.SuggestBaseUrlForPhones() ?? CloudEndpoints.ProductionApiBaseUrl;

            var tables = (await _adminData.GetTablesAsync().ConfigureAwait(true)).OrderBy(t => t.TableNumber).ToList();
            foreach (var t in tables)
            {
                var url = $"{baseUrl}/menu/?table={t.Id}";
                var png = BuildQrPngBytes(url);
                MenuQrRows.Add(new MenuQrTableRow
                {
                    TableLabel = SettingsUiLocalizer.TableQrLabel(t.TableNumber, t.Name),
                    Url = url,
                    PngBytes = png,
                    QrImage = BitmapImageFromPng(png)
                });
            }

            if (PublicMenuUrlHelper.LooksLikeLocalHostOnly((PublicMenuBaseUrl ?? string.Empty).Trim()) &&
                !PublicMenuUrlHelper.LooksLikeLocalHostOnly(baseUrl))
            {
                var dev =
                    PublicMenuUrlHelper.QrBasePort == PublicMenuUrlHelper.ViteDevMenuPort
                        ? SettingsUiLocalizer.StatusQrDevViteHint()
                        : SettingsUiLocalizer.StatusQrFirewallHint();

                StatusMessage = SettingsUiLocalizer.StatusQrLocalhostMismatch(baseUrl, dev);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = SettingsUiLocalizer.StatusQrListFailed(ex.Message);
        }
    }

    private async Task PrintAllMenuQrToPdfAsync()
    {
        if (MenuQrRows.Count == 0)
            await RefreshMenuQrRowsAsync().ConfigureAwait(true);
        if (MenuQrRows.Count == 0)
        {
            StatusMessage = SettingsUiLocalizer.StatusNoTablesForQr();
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = SettingsUiLocalizer.DialogPdfFilter(),
            FileName = "Menu-QR-codes.pdf"
        };
        if (dlg.ShowDialog() != true)
            return;

        var pages = MenuQrRows
            .Select(r => new MenuQrPdfPage(r.TableLabel, r.Url, r.PngBytes))
            .ToList();
        try
        {
            MenuQrPdfExportService.Save(dlg.FileName, pages);
            StatusMessage = SettingsUiLocalizer.StatusQrPdfSaved(pages.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = SettingsUiLocalizer.StatusQrPdfFailed(ex.Message);
        }
    }

    private static byte[] BuildQrPngBytes(string text)
    {
        using var gen = new QRCodeGenerator();
        var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(20);
    }

    private static BitmapImage BitmapImageFromPng(byte[] png)
    {
        var img = new BitmapImage();
        using var ms = new MemoryStream(png);
        img.BeginInit();
        img.StreamSource = ms;
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.EndInit();
        img.Freeze();
        return img;
    }
}

public sealed class MenuQrTableRow
{
    public string TableLabel { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public byte[] PngBytes { get; init; } = Array.Empty<byte>();
    public ImageSource? QrImage { get; init; }
}
