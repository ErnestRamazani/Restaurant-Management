using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

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
        using var db = new AppDbContext();
        try
        {
            return DatabaseResilientTransaction.Execute(db, () =>
            {
                using var tx = db.Database.BeginTransaction();
                try
                {
                    db.Transactions.Add(new MoneyTransaction
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
                    db.SaveChanges();
                    tx.Commit();
                    return new ManualLedgerResult(true, string.Empty);
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    return new ManualLedgerResult(false, ex.Message);
                }
            });
        }
        catch (Exception ex)
        {
            return new ManualLedgerResult(false, ex.Message);
        }
    }
}
