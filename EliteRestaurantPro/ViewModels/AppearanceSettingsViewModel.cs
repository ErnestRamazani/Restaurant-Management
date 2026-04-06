using System.Windows.Input;
using System.Windows.Media;
using EliteRestaurantPro.Utils;
using System.Collections.ObjectModel;
using Npgsql;

namespace EliteRestaurantPro.ViewModels;

public sealed class AppearanceSettingsViewModel : AdminBaseViewModel
{
    private readonly AppSettings _settings;

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
    private string _databaseConnectionString = string.Empty;

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

    public ObservableCollection<string> SettingsSections { get; } = new(["All", "Business Profile", "Currency & Pricing", "Menu Backgrounds", "Database", "Appearance"]);
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
        }
    }

    public bool ShowBusinessSection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Business Profile";
    public bool ShowCurrencySection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Currency & Pricing";
    public bool ShowMenuBackgroundSection => SelectedSettingsSection == "All" || SelectedSettingsSection == "Menu Backgrounds";
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

    public string DatabaseConnectionString
    {
        get => _databaseConnectionString;
        set => SetField(ref _databaseConnectionString, value);
    }

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
    public ICommand BrowseHomepageBackgroundCommand { get; }
    public ICommand BrowseMenuBackgroundCommand { get; }
    public ICommand SaveMenuBackgroundCommand { get; }
    public ICommand ClearMenuBackgroundCommand { get; }
    public ICommand SaveDatabaseSettingsCommand { get; }
    public ICommand TestDatabaseConnectionCommand { get; }

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
        BrowseHomepageBackgroundCommand = new RelayCommand(_ => BrowseHomepageBackground());
        BrowseMenuBackgroundCommand = new RelayCommand(_ => BrowseMenuBackground());
        SaveMenuBackgroundCommand = new RelayCommand(_ => SaveMenuBackground());
        ClearMenuBackgroundCommand = new RelayCommand(_ => ClearMenuBackground());
        SaveDatabaseSettingsCommand = new RelayCommand(_ => SaveDatabaseSettings());
        TestDatabaseConnectionCommand = new RelayCommand(_ => TestDatabaseConnection());
        LoadBusinessAndPricingSettings();
        LoadBackgroundSettings();
        LoadDatabaseSettings();
        LoadFromCurrentTheme();
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

        SettingsManager.Save(_settings);
        RefreshBusinessProfileBindings();
        StatusMessage = "Business profile saved.";
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

        SettingsManager.Save(_settings);
        ExchangeRateLastUpdated = _settings.CurrencyPricing.ExchangeRateLastUpdatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
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
        DatabaseConnectionString = db.PostgreSqlConnectionString ?? string.Empty;
    }

    private void SaveDatabaseSettings()
    {
        if (string.IsNullOrWhiteSpace(DatabaseConnectionString))
        {
            StatusMessage = "PostgreSQL connection string is required.";
            return;
        }

        _settings.Database.Provider = "PostgreSql";
        _settings.Database.PostgreSqlConnectionString = DatabaseConnectionString.Trim();
        SettingsManager.Save(_settings);
        StatusMessage = "Database settings saved (PostgreSQL). Restart app to apply.";
    }

    private void TestDatabaseConnection()
    {
        var cs = (DatabaseConnectionString ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cs))
        {
            StatusMessage = "Enter PostgreSQL connection string first.";
            return;
        }

        try
        {
            using var conn = new NpgsqlConnection(cs);
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT 1;", conn);
            _ = cmd.ExecuteScalar();
            StatusMessage = "PostgreSQL connection successful.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"PostgreSQL connection failed: {ex.Message}";
        }
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
}
