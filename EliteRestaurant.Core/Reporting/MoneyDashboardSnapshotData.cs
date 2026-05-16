namespace EliteRestaurant.Core.Reporting;

public sealed class MoneyDashboardSnapshotData
{
    public string TodayRevenueText { get; init; } = "$ 0.00 | FC 0";
    public string TodayExpensesText { get; init; } = "$ 0.00 | FC 0";
    public string TodayNetProfitText { get; init; } = "$ 0.00 | FC 0";
    public string TodayNetProfitColor { get; init; } = "#2ECC71";
    public string SelectedPeriodLabel { get; init; } = "This Day";
    public DateTime ReportStartDate { get; init; } = DateTime.Today;
    public DateTime ReportEndDate { get; init; } = DateTime.Today;
    public List<MoneyLedgerRowData> LedgerItems { get; init; } = [];
    public string TotalRevenueText { get; init; } = "$ 0.00 | FC 0";
    public string TotalExpensesText { get; init; } = "$ 0.00 | FC 0";
    public string NetProfitText { get; init; } = "$ 0.00 | FC 0";
    public string NetProfitColor { get; init; } = "#2ECC71";
    public string SalesSummaryText { get; init; } = "$ 0.00 | FC 0";
    public string TipsSummaryText { get; init; } = "$ 0.00 | FC 0";
    public string PayrollSummaryText { get; init; } = "$ 0.00 | FC 0";
    /// <summary>Revenue rows with category Delivery Fee (same dual-currency formatting as sales).</summary>
    public string DeliveryFeesSummaryText { get; init; } = "$ 0.00 | FC 0";
    /// <summary>When set, snapshot was filtered by order origin (Online / InStore).</summary>
    public string? OriginFilterLabel { get; init; }
}
