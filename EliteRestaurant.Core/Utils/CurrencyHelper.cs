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

    public static string FormatAmount(decimal amount, string currencyCode)
        => NormalizeCurrencyCode(currencyCode) == CongoleseFranc
            ? $"FC {amount:N0}"
            : $"$ {amount:N2}";

    /// <summary>USD amount with two decimals and no currency symbol (compact copy in dialogs).</summary>
    public static string FormatUsdAmountDigits(decimal amount)
        => amount.ToString("N2", CultureInfo.InvariantCulture);

    public static string FormatDualCurrency(decimal usdAmount, decimal fcAmount)
    {
        var mode = SettingsManager.Load().CurrencyPricing.DefaultCurrencyDisplayMode;
        if (string.Equals(mode, Usd, StringComparison.OrdinalIgnoreCase))
            return FormatAmount(usdAmount, Usd);
        if (string.Equals(mode, CongoleseFranc, StringComparison.OrdinalIgnoreCase))
            return FormatAmount(fcAmount, CongoleseFranc);
        return $"{FormatAmount(usdAmount, Usd)} | {FormatAmount(fcAmount, CongoleseFranc)}";
    }
}
