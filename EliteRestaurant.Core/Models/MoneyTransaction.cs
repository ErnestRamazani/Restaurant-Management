namespace EliteRestaurant.Core.Models;

public class MoneyTransaction
{
    public int Id { get; set; }
    /// <summary>When set, ties this row to an order for reporting filters (e.g. Money by origin).</summary>
    public int? RelatedOrderId { get; set; }
    /// <summary><see cref="OrderOrigin"/> from the source order when posted from a sale.</summary>
    public string? OrderOriginType { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountUsd { get; set; }
    public decimal AmountFc { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public string Type { get; set; } = "Expense";
    public string Category { get; set; } = "Variable";
    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRateUsed { get; set; } = 2250m;
    public string Justification { get; set; } = string.Empty;
    public bool IsFixed { get; set; }
}
