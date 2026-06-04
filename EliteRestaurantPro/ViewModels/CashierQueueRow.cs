namespace EliteRestaurantPro.ViewModels;

public sealed class CashierQueueRow
{
    public int OrderId { get; init; }
    public string OrderCode { get; init; } = string.Empty;
    public string TableLabel { get; init; } = string.Empty;
    public string ServerName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string CreatedAtText { get; init; } = string.Empty;
    public decimal GrandTotalUsd { get; init; }
    public string GrandTotalText { get; init; } = string.Empty;
    public string LinesSummary { get; init; } = string.Empty;
    public string DisplayServerLine { get; set; } = string.Empty;
    public string DisplayTableLabel { get; set; } = string.Empty;
    public string DisplayCreatedAtText { get; set; } = string.Empty;
}
