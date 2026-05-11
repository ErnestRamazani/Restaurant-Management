using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

    public ObservableCollection<string> SettingsSections { get; } = new(["All", "Business Profile", "Currency & Pricing", "Menu Backgrounds", "Menu QR Codes", "Database", "Appearance"]);
    public ObservableCollection<string> BackgroundMenuKeys { get; } = new(["Dashboard", "Employees", "Menu", "Inventory", "Attendance", "Tables", "Reservations", "Orders", "CreateOrder", "Money", "Salary", "Reports", "KitchenQueue", "ServerPickup"]);
    public ObservableCollection<string> DatabaseProviders { get; } = new(["PostgreSql"]);

    public string SelectedSettingsSection
    {
        get => _selectedSettingsSection;
        set
        {
            if (!SetField(ref _selectedSettingsSection, value))
                return;
            OnPropertyChanged(nameof(ShowBusinessSection));
            OnPropertyChanged(nameof(ShowCurrencySection));
            OnPropertyChanged(nameof(ShowMenuBackgroundSection));
            OnPropertyChanged(nameof(ShowDatabaseSection));
            OnPropertyChanged(nameof(ShowAppearanceSection));
            OnPropertyChanged(nameof(ShowMenuQrSection));
            if (ShowMenuQrSection)
                _ = RefreshMenuQrRowsAsync();
        }
    }

    public bool ShowBusinessSection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Business Profile";
    public bool ShowCurrencySection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Currency & Pricing";
    public bool ShowMenuBackgroundSection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Menu Backgrounds";
    public bool ShowMenuQrSection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Menu QR Codes";
    public bool ShowDatabaseSection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Database";
    public bool ShowAppearanceSection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Appearance";

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

    public AppearanceSettingsViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        _settings = SettingsManager.Load();
        ApplyThemeCommand = new RelayCommand(_ => ApplyTheme());
        SaveThemeCommand = new RelayCommand(_ => SaveTheme());
        ResetThemeCommand = new RelayCommand(_ => ResetTheme());
        ReloadSavedThemeCommand = new RelayCommand(_ => LoadFromCurrentTheme());
        ApplyPickerToTokenCommand = new RelayCommand(_ => ApplyPickerToToken());
        SaveBusinessProfileCommand = new RelayCommand(_ => SaveBusinessProfile());
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
        LoadBusinessAndPricingSettings();
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

        var pricing = _settings.CurrencyPricing;
        DefaultCurrencyDisplayMode = pricing.DefaultCurrencyDisplayMode;
        ExchangeRateUsdToFc = pricing.UsdToFcRate.ToString("0.##");
        ExchangeRateLastUpdated = pricing.ExchangeRateLastUpdatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        RoundingLine = pricing.RoundingLine;
        RoundingSubtotal = pricing.RoundingSubtotal;
        RoundingGrandTotal = pricing.RoundingGrandTotal;
        TaxPercent = pricing.TaxPercent.ToString("0.##");
        ServicePercent = pricing.ServicePercent.ToString("0.##");
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
        _settings.BusinessProfile.TicketFooterText = TicketFooterText.Trim();
        _settings.BusinessProfile.TaxIdLegalInfo = TaxIdLegalInfo.Trim();
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

        _settings.CloudApi.BaseUrl = CloudEndpoints.NormalizeApiBaseUrl(_settings.BusinessProfile.PublicMenuBaseUrl);

        SettingsManager.Save(_settings);
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
