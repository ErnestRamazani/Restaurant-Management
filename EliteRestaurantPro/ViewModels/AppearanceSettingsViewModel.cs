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
using EliteRestaurantPro.Services;
using EliteRestaurantPro.Utils;
using Microsoft.Win32;
using QRCoder;

namespace EliteRestaurantPro.ViewModels;

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
    private string _statusMessage = "Customize your theme colors. Use #RRGGBB or #AARRGGBB.";
    private string _selectedSettingsSection = "All";
    private string _selectedToken = "Gold Accent";
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
    private string _databaseProvider = "PostgreSql";
    private string _databaseHost = string.Empty;
    private string _databasePort = "5432";
    private string _databaseName = string.Empty;
    private string _databaseUsername = string.Empty;
    private string _pendingDatabasePassword = string.Empty;
    private bool _hasSavedDatabasePassword;
    private string _publicMenuBaseUrl = CloudEndpoints.ProductionApiBaseUrl;
    private string _customerMenuTagline = string.Empty;
    private string _staffLoginPasscode = "er4124";
    private string _onlinePromoTitle = string.Empty;
    private string _onlinePromoSubtitle = string.Empty;
    private string _onlinePromoCtaLabel = string.Empty;
    private string _onlinePromoImagePath = string.Empty;
    private string _onlineOrdersTableId = string.Empty;
    private string _reservationLeadDays = "2";
    private string _reservationMaxMonthsAhead = "6";
    private string _attendanceMorningStartText = "12:00";
    private string _attendanceMorningEndText = "18:00";
    private string _attendanceNightStartText = "18:00";
    private string _attendanceNightEndText = "23:00";
    private string _attendanceLateGraceMinutesText = "30";
    private string _salaryLateDaysPerAttendanceUnitText = "4";
    private bool _salaryAbsenceCountsAsAttendanceUnit = true;
    private string _salarySalesBonusPercentText = "5";
    private string _salaryMaxAdvancePercentOfGrossText = "30";

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

    public ObservableCollection<string> SettingsSections { get; } = new(["All", "Business Profile", "Tickets & receipts", "Reservations", "Menu categories", "Currency & Pricing", "Attendance & shifts", "Salary", "Menu Backgrounds", "Menu QR Codes", "Database", "Appearance"]);
    public ObservableCollection<string> BackgroundMenuKeys { get; } = new(["Dashboard", "Employees", "Menu", "Inventory", "Attendance", "Tables", "Reservations", "Orders", "CreateOrder", "Money", "Salary", "Reports", "KitchenQueue", "ServerPickup"]);
    public ObservableCollection<string> DatabaseProviders { get; } = new(["PostgreSql"]);

    public ObservableCollection<TicketSocialMediaRowViewModel> TicketSocialMediaRows { get; } = new();

    public string SelectedSettingsSection
    {
        get => _selectedSettingsSection;
        set
        {
            var salaryWasVisible = _selectedSettingsSection == "All" || _selectedSettingsSection == "Salary";
            if (!SetField(ref _selectedSettingsSection, value))
                return;
            var salaryNowVisible = _selectedSettingsSection == "All" || _selectedSettingsSection == "Salary";
            if (salaryNowVisible && !salaryWasVisible)
                RefreshSalaryFromDiskIntoViewModel();

            OnPropertyChanged(nameof(ShowBusinessSection));
            OnPropertyChanged(nameof(ShowCurrencySection));
            OnPropertyChanged(nameof(ShowMenuBackgroundSection));
            OnPropertyChanged(nameof(ShowDatabaseSection));
            OnPropertyChanged(nameof(ShowAppearanceSection));
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

    public bool ShowAttendanceSection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Attendance & shifts";

    public bool ShowSalarySection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Salary";

    public bool ShowReservationsSection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Reservations";

    public bool ShowMenuTaxonomySection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Menu categories";

    public bool ShowBusinessSection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Business Profile";
    public bool ShowCurrencySection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Currency & Pricing";
    public bool ShowMenuBackgroundSection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Menu Backgrounds";
    public bool ShowMenuQrSection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Menu QR Codes";
    public bool ShowDatabaseSection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Database";
    public bool ShowAppearanceSection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Appearance";

    public bool ShowTicketsReceiptSection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Tickets & receipts";

    private string _selectedBackgroundMenu = "Dashboard";
    private string _selectedMenuBackgroundPath = string.Empty;
    private double _backgroundDimStrength = 0.45;
    private double _backgroundContrastIntensity = 0.55;

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

    public string StaffLoginPasscode
    {
        get => _staffLoginPasscode;
        set => SetField(ref _staffLoginPasscode, value);
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

    public string SelectedToken
    {
        get => _selectedToken;
        set
        {
            if (!SetField(ref _selectedToken, value))
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
        SaveBusinessProfileCommand = new RelayCommand(_ => SaveBusinessProfile());
        SaveTicketReceiptLayoutCommand = new RelayCommand(_ => SaveTicketReceiptLayout());
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
        if (ShowMenuQrSection)
            _ = RefreshMenuQrRowsAsync();
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
        StatusMessage = "Theme values loaded.";
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
        StatusMessage = "Default palette restored and saved.";
    }

    private void ApplyTheme()
    {
        if (!TryBuildPalette(out var palette, out var error))
        {
            StatusMessage = error;
            return;
        }

        ThemeManager.ApplyPalette(palette);
        StatusMessage = "Theme applied. Save to keep it after restart.";
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
        StatusMessage = "Theme saved and applied.";
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
            error = "One or more colors are invalid. Use #RRGGBB or #AARRGGBB.";
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
            StatusMessage = "Picker color is invalid.";
            return;
        }

        SetHexForToken(SelectedToken, normalized);
        ApplyTheme();
        StatusMessage = $"{SelectedToken} updated from HSL picker. Save to keep after restart.";
    }

    private void LoadTokenIntoPicker()
    {
        var tokenHex = GetHexForToken(SelectedToken);
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
        StaffLoginPasscode = string.IsNullOrWhiteSpace(business.StaffLoginPasscode)
            ? "er4124"
            : business.StaffLoginPasscode.Trim();
        OnlinePromoTitle = business.OnlinePromoTitle ?? string.Empty;
        OnlinePromoSubtitle = business.OnlinePromoSubtitle ?? string.Empty;
        OnlinePromoCtaLabel = business.OnlinePromoCtaLabel ?? string.Empty;
        OnlinePromoImagePath = business.OnlinePromoImagePath ?? string.Empty;
        OnlineOrdersTableId = business.OnlineOrdersTableId?.ToString() ?? string.Empty;
        ReservationLeadDays = Math.Clamp(business.ReservationLeadDays, 0, 30).ToString(CultureInfo.InvariantCulture);
        ReservationMaxMonthsAhead = Math.Clamp(business.ReservationMaxMonthsAhead, 1, 24).ToString(CultureInfo.InvariantCulture);

        var pricing = _settings.CurrencyPricing;
        DefaultCurrencyDisplayMode = pricing.DefaultCurrencyDisplayMode;
        ExchangeRateUsdToFc = pricing.UsdToFcRate.ToString("0.##");
        ExchangeRateLastUpdated = pricing.ExchangeRateLastUpdatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        RoundingLine = pricing.RoundingLine;
        RoundingSubtotal = pricing.RoundingSubtotal;
        RoundingGrandTotal = pricing.RoundingGrandTotal;
        TaxPercent = pricing.TaxPercent.ToString("0.##");
        ServicePercent = pricing.ServicePercent.ToString("0.##");
        LoadAttendanceSettings();
        LoadSalarySettings();
        LoadTicketReceiptLayout();
    }

    private void LoadTicketReceiptLayout()
    {
        _settings.TicketReceipt ??= new TicketReceiptSettings();
        TicketHeaderLogoPath = _settings.TicketReceipt.HeaderLogoPath ?? string.Empty;
        TicketSocialMediaRows.Clear();
        foreach (var r in _settings.TicketReceipt.SocialMediaRows)
            TicketSocialMediaRows.Add(new TicketSocialMediaRowViewModel(r.PlatformName, r.UserText, r.IconPath));
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
            StatusMessage = "Shift times must be valid (use HH:mm, e.g. 12:00 and 18:00).";
            return;
        }

        if (mEnd <= mStart || nEnd <= nStart)
        {
            StatusMessage = "Each shift end must be after its start.";
            return;
        }

        if (!int.TryParse((AttendanceLateGraceMinutesText ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var grace) ||
            grace < 0 || grace > 240)
        {
            StatusMessage = "Late clock-in grace must be an integer from 0 to 240 (minutes).";
            return;
        }

        _settings.Attendance ??= new AttendanceSettings();
        _settings.Attendance.MorningShiftStart = mStart;
        _settings.Attendance.MorningShiftEnd = mEnd;
        _settings.Attendance.NightShiftStart = nStart;
        _settings.Attendance.NightShiftEnd = nEnd;
        _settings.Attendance.LateClockInGraceMinutes = grace;
        SettingsManager.Save(_settings);
        StatusMessage = "Attendance shift settings saved.";
    }

    private void SaveSalarySettings()
    {
        if (!int.TryParse((SalaryLateDaysPerAttendanceUnitText ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lateDays) ||
            lateDays < 1)
        {
            StatusMessage = "Late days per attendance unit must be a whole number ≥ 1 (default 4).";
            return;
        }

        if (!TryParseDecimalInput(SalarySalesBonusPercentText, out var bonusPct) || bonusPct < 0m || bonusPct > 100m)
        {
            StatusMessage = "Sales bonus percent must be between 0 and 100.";
            return;
        }

        if (!TryParseDecimalInput(SalaryMaxAdvancePercentOfGrossText, out var advancePct) || advancePct < 0m || advancePct > 100m)
        {
            StatusMessage = "Max advance percent of gross must be between 0 and 100.";
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
        StatusMessage = "Salary payroll settings saved. Syncing with API…";
    }

    private async Task PushSalarySettingsToCloudAndResyncFromDiskAsync()
    {
        try
        {
            await new AdminSettingsApiClient().PushSettingsAsync(_settings, applyLogoChanges: false, applyOnlinePromoImageChanges: false)
                .ConfigureAwait(true);
            RefreshSalaryFromDiskIntoViewModel();
            StatusMessage = "Salary payroll settings saved and pushed to the API.";
        }
        catch (Exception ex)
        {
            StatusMessage =
                $"Salary saved on this PC. Cloud push failed: {ex.GetBaseException().Message}. Fix API URL/token and use Save again to push.";
        }
    }

    private void SaveReservationSettings()
    {
        if (!int.TryParse((ReservationLeadDays ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var leadDays))
        {
            StatusMessage = "Reservation lead days must be a whole number (0–30).";
            return;
        }

        if (!int.TryParse((ReservationMaxMonthsAhead ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxMonths))
        {
            StatusMessage = "Reservation horizon (months) must be a whole number (1–24).";
            return;
        }

        _settings.BusinessProfile.ReservationLeadDays = Math.Clamp(leadDays, 0, 30);
        _settings.BusinessProfile.ReservationMaxMonthsAhead = Math.Clamp(maxMonths, 1, 24);
        SettingsManager.Save(_settings);
        _adminData.ReloadFromSettings();
        _ = new AdminSettingsApiClient().PushSettingsAsync(_settings, applyLogoChanges: false, applyOnlinePromoImageChanges: false);
        ReservationLeadDays = _settings.BusinessProfile.ReservationLeadDays.ToString(CultureInfo.InvariantCulture);
        ReservationMaxMonthsAhead = _settings.BusinessProfile.ReservationMaxMonthsAhead.ToString(CultureInfo.InvariantCulture);
        StatusMessage = "Reservation settings saved and pushed to the public menu.";
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
        StatusMessage = "Menu categories reset to Elite defaults in this screen. Click Save menu categories to persist and push.";
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
                StatusMessage = "Each menu type needs a name.";
                return;
            }

            var sections = new List<MenuTaxonomySection>();
            foreach (var s in t.Sections)
            {
                var secName = (s.Name ?? string.Empty).Trim();
                if (secName.Length == 0)
                {
                    StatusMessage = "Each section needs a name (this is saved as the product category).";
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
                StatusMessage = "Each menu type needs at least one section.";
                return;
            }

            types.Add(new MenuTaxonomyType { Name = typeName, IsDrink = t.IsDrink, Sections = sections });
        }

        if (types.Count == 0)
        {
            StatusMessage = "Add at least one menu type (for example Food and Drink).";
            return;
        }

        _settings.MenuTaxonomy = new MenuTaxonomySettings { Types = types };
        SettingsManager.Save(_settings);
        var refreshed = SettingsManager.Load();
        _settings.MenuTaxonomy = refreshed.MenuTaxonomy;
        _adminData.ReloadFromSettings();
        LoadMenuTaxonomyUi();
        StatusMessage = "Menu categories saved on this PC. Pushing to cloud…";
        _ = PushMenuTaxonomyCloudAsync();
    }

    private async Task PushMenuTaxonomyCloudAsync()
    {
        try
        {
            await new AdminSettingsApiClient().PushSettingsAsync(_settings, applyLogoChanges: false, applyOnlinePromoImageChanges: false)
                .ConfigureAwait(true);
            StatusMessage = "Menu categories saved and pushed to the public menu API.";
        }
        catch (Exception ex)
        {
            StatusMessage =
                $"Menu categories saved on this PC. Cloud push failed: {ex.GetBaseException().Message}. Fix API URL/token and use Save again to push.";
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

    private void SaveBusinessProfile()
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
        _settings.BusinessProfile.StaffLoginPasscode = string.IsNullOrWhiteSpace(StaffLoginPasscode)
            ? "er4124"
            : StaffLoginPasscode.Trim();
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

        _settings.CloudApi.BaseUrl = CloudEndpoints.NormalizeApiBaseUrl(_settings.BusinessProfile.PublicMenuBaseUrl);

        PersistTicketReceiptLayout();

        SettingsManager.Save(_settings);
        ReloadTicketReceiptSettingsFromDisk();
        _adminData.ReloadFromSettings();
        _ = new AdminSettingsApiClient().PushSettingsAsync(_settings, applyLogoChanges: true, applyOnlinePromoImageChanges: true);
        RefreshBusinessProfileBindings();
        var msg = "Business profile saved.";
        if (PublicMenuUrlHelper.LooksLikeLocalHostOnly(_settings.BusinessProfile.PublicMenuBaseUrl))
            msg += " QR: localhost will not work on customers’ phones on Wi-Fi — use the hosted cloud URL or this PC’s LAN URL and re-print QRs.";
        StatusMessage = msg;
    }

    private void SaveCurrencyPricing()
    {
        if (!TryParseDecimalInput(ExchangeRateUsdToFc, out var rate) || rate <= 0)
        {
            StatusMessage = "Exchange rate must be a positive number.";
            return;
        }

        if (!TryParseDecimalInput(TaxPercent, out var tax) || tax < 0)
        {
            StatusMessage = "Tax percent must be zero or positive.";
            return;
        }

        if (!TryParseDecimalInput(ServicePercent, out var service) || service < 0)
        {
            StatusMessage = "Service percent must be zero or positive.";
            return;
        }

        _settings.CurrencyPricing.DefaultCurrencyDisplayMode = DefaultCurrencyDisplayMode;
        _settings.CurrencyPricing.UsdToFcRate = rate;
        _settings.CurrencyPricing.ExchangeRateLastUpdatedUtc = DateTime.UtcNow;
        _settings.CurrencyPricing.RoundingLine = RoundingLine;
        _settings.CurrencyPricing.RoundingSubtotal = RoundingSubtotal;
        _settings.CurrencyPricing.RoundingGrandTotal = RoundingGrandTotal;
        _settings.CurrencyPricing.TaxPercent = tax;
        _settings.CurrencyPricing.ServicePercent = service;

        _settings.CloudApi.BaseUrl = CloudEndpoints.NormalizeApiBaseUrl(_settings.BusinessProfile.PublicMenuBaseUrl);

        SettingsManager.Save(_settings);
        _adminData.ReloadFromSettings();
        _ = new AdminSettingsApiClient().PushSettingsAsync(_settings, applyLogoChanges: false, applyOnlinePromoImageChanges: false);
        StatusMessage = "Currency & pricing settings saved.";
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
            Title = "Select Restaurant Logo",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
            RestaurantLogoPath = dialog.FileName;
    }

    private void PersistTicketReceiptLayout()
    {
        _settings.TicketReceipt ??= new TicketReceiptSettings();
        _settings.TicketReceipt.HeaderLogoPath = (TicketHeaderLogoPath ?? string.Empty).Trim();
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
        _ = new AdminSettingsApiClient().PushSettingsAsync(_settings, applyLogoChanges: false, applyOnlinePromoImageChanges: false);
        RefreshBusinessProfileBindings();
        StatusMessage = "Tickets & receipts settings saved.";
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
            Title = "Ticket header logo (above restaurant name on printed/PDF tickets)",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All files (*.*)|*.*"
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
            Title = "Icon image for this social line on tickets",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All files (*.*)|*.*"
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
            Title = "Online order hero image (public menu)",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
            OnlinePromoImagePath = dialog.FileName;
    }

    private void BrowseHomepageBackground()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Homepage Background Image",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*"
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
            Title = $"Select background for {SelectedBackgroundMenu}",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
            SelectedMenuBackgroundPath = dialog.FileName;
    }

    private void SaveMenuBackground()
    {
        var key = SelectedBackgroundMenu.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            StatusMessage = "Select a menu first.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(SelectedMenuBackgroundPath))
            _settings.NavigationBackgrounds.PageImagePaths[key] = SelectedMenuBackgroundPath.Trim();
        else if (_settings.NavigationBackgrounds.PageImagePaths.ContainsKey(key))
            _settings.NavigationBackgrounds.PageImagePaths.Remove(key);

        _settings.NavigationBackgrounds.DimStrength = Math.Clamp(BackgroundDimStrength, 0, 0.5);
        _settings.NavigationBackgrounds.ContrastIntensity = Math.Clamp(BackgroundContrastIntensity, 0, 0.5);
        SettingsManager.Save(_settings);
        StatusMessage = $"Background saved for {key}.";
    }

    private void ClearMenuBackground()
    {
        var key = SelectedBackgroundMenu.Trim();
        if (_settings.NavigationBackgrounds.PageImagePaths.ContainsKey(key))
            _settings.NavigationBackgrounds.PageImagePaths.Remove(key);
        SelectedMenuBackgroundPath = string.Empty;
        SettingsManager.Save(_settings);
        StatusMessage = $"Background cleared for {key}.";
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
            StatusMessage = "Host, database name, and username are required.";
            return;
        }

        if (IsLocalDatabaseHost(host))
        {
            StatusMessage = "Local PostgreSQL is disabled for live data. Enter the DigitalOcean PostgreSQL host.";
            return;
        }

        if (!string.IsNullOrEmpty(_pendingDatabasePassword) && !DatabaseConnectionSecret.IsDpapiAvailable)
        {
            StatusMessage = "Cannot store a password on this OS. Leave password blank for trust auth, or use ELITE_POSTGRES_CONNECTION.";
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
        StatusMessage = "Cloud database settings saved (PostgreSQL). Restart app to apply.";
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
            StatusMessage = "API reachable (sample read succeeded). The desktop uses HTTP only; data lives on the API host.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"API request failed: {ex.GetBaseException().Message}";
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
            "Background Dark" => BackgroundDarkHex,
            "Background Medium" => BackgroundMediumHex,
            "Sidebar" => SidebarHex,
            "Card Base" => CardBaseHex,
            "Gold Accent" => GoldAccentHex,
            "Text Secondary" => TextSecondaryHex,
            "Border Subtle" => BorderSubtleHex,
            "Stat Blue" => StatBlueHex,
            "Stat Green" => StatGreenHex,
            "Stat Red" => StatRedHex,
            _ => GoldAccentHex
        };
    }

    private void SetHexForToken(string token, string hex)
    {
        switch (token)
        {
            case "Background Dark":
                BackgroundDarkHex = hex;
                break;
            case "Background Medium":
                BackgroundMediumHex = hex;
                break;
            case "Sidebar":
                SidebarHex = hex;
                break;
            case "Card Base":
                CardBaseHex = hex;
                break;
            case "Gold Accent":
                GoldAccentHex = hex;
                break;
            case "Text Secondary":
                TextSecondaryHex = hex;
                break;
            case "Border Subtle":
                BorderSubtleHex = hex;
                break;
            case "Stat Blue":
                StatBlueHex = hex;
                break;
            case "Stat Green":
                StatGreenHex = hex;
                break;
            case "Stat Red":
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
            StatusMessage =
                $"Could not detect a LAN address. Enter your PC’s IP (e.g. http://192.168.1.50:{p}). " +
                $"Allow inbound TCP {p} for Private networks in Windows Firewall if phones cannot connect.";
            return;
        }

        PublicMenuBaseUrl = suggested;
        _ = RefreshMenuQrRowsAsync();
        StatusMessage = $"Public menu URL set to {suggested}. Save Business Profile to keep it, then re-print QR labels. Phone must use the same Wi-Fi as this PC (or a routed path to it).";
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
                    TableLabel = $"Table {t.TableNumber} — {t.Name}",
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
                        ? "In development, run the customer menu with npm run dev in elite-menu (Vite must use host: true) and allow that port in Windows Firewall on Private networks."
                        : "Allow the API (static menu) port for Private networks in Windows Firewall if phones cannot connect.";

                StatusMessage =
                    $"Settings still list localhost, but the QR links below use {baseUrl} so phones on Wi-Fi can open the menu. Save that as Public menu base URL to keep it, then re-print. {dev}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not build QR list: {ex.Message}";
        }
    }

    private async Task PrintAllMenuQrToPdfAsync()
    {
        if (MenuQrRows.Count == 0)
            await RefreshMenuQrRowsAsync().ConfigureAwait(true);
        if (MenuQrRows.Count == 0)
        {
            StatusMessage = "No tables in database to export.";
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "PDF document|*.pdf",
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
            StatusMessage = $"Saved QR PDF ({pages.Count} pages).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not create PDF: {ex.Message}";
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
