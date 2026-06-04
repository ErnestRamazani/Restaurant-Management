using System.Globalization;

namespace EliteRestaurant.Core.Utils;

public static class CurrencyHelper
{
    public const string Usd = "USD";
    public const string CongoleseFranc = "FC";
    public const decimal DefaultFcPerUsd = 2250m;

    public static decimal FcPerUsd
    {
        get
        {
            var settings = SettingsManager.Load();
            var rate = settings.CurrencyPricing.UsdToFcRate;
            return rate > 0m ? rate : DefaultFcPerUsd;
        }
    }

    public static decimal ConvertUsdToFc(decimal usdAmount)
        => Math.Round(usdAmount * FcPerUsd, 2);

    public static decimal ConvertFcToUsd(decimal fcAmount)
        => Math.Round(fcAmount / FcPerUsd, 2);

    public static decimal ResolveUsdAmount(decimal amount, string currencyCode)
        => NormalizeCurrencyCode(currencyCode) == CongoleseFranc
            ? ConvertFcToUsd(amount)
            : Math.Round(amount, 2);

    public static decimal ResolveFcAmount(decimal amount, string currencyCode)
        => NormalizeCurrencyCode(currencyCode) == CongoleseFranc
            ? Math.Round(amount, 2)
            : ConvertUsdToFc(amount);

    public static string NormalizeCurrencyCode(string? currencyCode)
        => string.Equals(currencyCode, CongoleseFranc, StringComparison.OrdinalIgnoreCase)
            ? CongoleseFranc
            : Usd;

    private static readonly CultureInfo DefaultMoneyCulture = CultureInfo.GetCultureInfo("en-US");

    public static string FormatAmount(decimal amount, string currencyCode, CultureInfo? culture = null)
    {
        culture ??= DefaultMoneyCulture;
        if (NormalizeCurrencyCode(currencyCode) == CongoleseFranc)
        {
            var fcDigits = amount.ToString("N0", culture);
            return $"FC {fcDigits}";
        }

        var usdDigits = amount.ToString("N2", culture);
        return IsFrenchMoneyCulture(culture)
            ? $"{usdDigits} $"
            : $"$ {usdDigits}";
    }

    /// <summary>USD amount with two decimals and no currency symbol (compact copy in dialogs).</summary>
    public static string FormatUsdAmountDigits(decimal amount)
        => amount.ToString("N2", CultureInfo.InvariantCulture);

    public static string FormatDualCurrency(decimal usdAmount, decimal fcAmount, CultureInfo? culture = null)
    {
        culture ??= DefaultMoneyCulture;
        var mode = SettingsManager.Load().CurrencyPricing.DefaultCurrencyDisplayMode;
        if (string.Equals(mode, Usd, StringComparison.OrdinalIgnoreCase))
            return FormatAmount(usdAmount, Usd, culture);
        if (string.Equals(mode, CongoleseFranc, StringComparison.OrdinalIgnoreCase))
            return FormatAmount(fcAmount, CongoleseFranc, culture);
        return $"{FormatAmount(usdAmount, Usd, culture)} | {FormatAmount(fcAmount, CongoleseFranc, culture)}";
    }

    private static bool IsFrenchMoneyCulture(CultureInfo culture) =>
        culture.Name.StartsWith("fr", StringComparison.OrdinalIgnoreCase);
}
