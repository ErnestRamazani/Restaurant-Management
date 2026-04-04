namespace EliteRestaurantPro.Models;

public class MoneyTransaction
{
    public int Id { get; set; }
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
