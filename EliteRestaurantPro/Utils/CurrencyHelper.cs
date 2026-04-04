namespace EliteRestaurantPro.Utils;

public static class CurrencyHelper
{
    public const string Usd = "USD";
    public const string CongoleseFranc = "FC";
    public const decimal FcPerUsd = 2250m;

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

    public static string FormatDualCurrency(decimal usdAmount, decimal fcAmount)
        => $"{FormatAmount(usdAmount, Usd)} | {FormatAmount(fcAmount, CongoleseFranc)}";
}
