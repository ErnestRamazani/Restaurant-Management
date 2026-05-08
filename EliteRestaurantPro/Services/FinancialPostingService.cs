using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.Services;

namespace EliteRestaurantPro.Services;

public sealed class FinancialPostingService
{
    public sealed record ManualLedgerResult(bool Ok, string Message);

    public ManualLedgerResult AddManualLedgerEntry(
        decimal amount,
        string selectedCurrency,
        DateTime entryDate,
        string selectedType,
        string selectedCategory,
        string justification,
        bool isFixed)
    {
        try
        {
            DesktopCloudPersistence.PushUpsertBlocking(new MoneyTransaction
            {
                Amount = amount,
                AmountUsd = CurrencyHelper.ResolveUsdAmount(amount, selectedCurrency),
                AmountFc = CurrencyHelper.ResolveFcAmount(amount, selectedCurrency),
                Date = entryDate.Date.AddHours(DateTime.Now.Hour).AddMinutes(DateTime.Now.Minute),
                Type = selectedType,
                Category = selectedCategory,
                CurrencyCode = selectedCurrency,
                ExchangeRateUsed = CurrencyHelper.FcPerUsd,
                Justification = justification,
                IsFixed = isFixed
            });
            return new ManualLedgerResult(true, string.Empty);
        }
        catch (Exception ex)
        {
            return new ManualLedgerResult(false, ex.GetBaseException().Message);
        }
    }
}
