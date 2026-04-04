using System.Windows.Input;
using System.Windows.Media;
using EliteRestaurantPro.Utils;

namespace EliteRestaurantPro.ViewModels;

public sealed class AppearanceSettingsViewModel : AdminBaseViewModel
{
    private string _backgroundDarkHex = string.Empty;
    private string _backgroundMediumHex = string.Empty;
    private string _cardBaseHex = string.Empty;
    private string _goldAccentHex = string.Empty;
    private string _textSecondaryHex = string.Empty;
    private string _borderSubtleHex = string.Empty;
    private string _statBlueHex = string.Empty;
    private string _statGreenHex = string.Empty;
    private string _statRedHex = string.Empty;
    private string _statusMessage = "Customize your theme colors. Use #RRGGBB or #AARRGGBB.";
    private string _selectedToken = "Gold Accent";
    private double _pickerHue;
    private double _pickerSaturation = 50;
    private double _pickerLightness = 50;
    private string _pickerHex = "#FFD8B24A";

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

    public AppearanceSettingsViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        ApplyThemeCommand = new RelayCommand(_ => ApplyTheme());
        SaveThemeCommand = new RelayCommand(_ => SaveTheme());
        ResetThemeCommand = new RelayCommand(_ => ResetTheme());
        ReloadSavedThemeCommand = new RelayCommand(_ => LoadFromCurrentTheme());
        ApplyPickerToTokenCommand = new RelayCommand(_ => ApplyPickerToToken());
        LoadFromCurrentTheme();
    }

    private void LoadFromCurrentTheme()
    {
        var palette = ThemeManager.GetCurrentPalette();
        BackgroundDarkHex = palette.BackgroundDark;
        BackgroundMediumHex = palette.BackgroundMedium;
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

        _pickerHue = h;
        _pickerSaturation = s;
        _pickerLightness = l;
        _pickerHex = normalized;

        OnPropertyChanged(nameof(PickerHue));
        OnPropertyChanged(nameof(PickerSaturation));
        OnPropertyChanged(nameof(PickerLightness));
        OnPropertyChanged(nameof(PickerHex));
        OnPropertyChanged(nameof(PickerPreviewBrush));
    }

    private void UpdatePickerFromHsl()
    {
        var color = HslToColor(PickerHue, PickerSaturation, PickerLightness);
        PickerHex = color.ToString();
        OnPropertyChanged(nameof(PickerPreviewBrush));
    }

    private string GetHexForToken(string token)
    {
        return token switch
        {
            "Background Dark" => BackgroundDarkHex,
            "Background Medium" => BackgroundMediumHex,
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
