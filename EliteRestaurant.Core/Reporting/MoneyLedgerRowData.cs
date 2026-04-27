namespace EliteRestaurant.Core.Reporting;

public sealed class MoneyLedgerRowData
{
    public DateTime Date { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Justification { get; init; } = string.Empty;
    public string AmountText { get; init; } = "$ 0.00";
    public string AmountColor { get; init; } = "#2ECC71";
}
