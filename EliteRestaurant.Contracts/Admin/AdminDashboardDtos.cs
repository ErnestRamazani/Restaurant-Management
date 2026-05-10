namespace EliteRestaurant.Contracts.Admin;

public sealed record AdminPayrollAlertDto(
    bool Show,
    string Title,
    string Message,
    int DaysPastMonthEnd);

public sealed record AdminKpiCardDto(
    string Key,
    string Label,
    string PrimaryText,
    string Subtitle,
    string AccentClass,
    string NavView,
    string? NavFilter,
    IReadOnlyList<decimal>? SparklineUsdRevenue7d);

/// <param name="AlertLevel"><c>critical</c> (expiry/stock emergency) or <c>reorder</c> (low / reorder soon).</param>
public sealed record AdminInventoryAlertRowDto(
    string Name,
    string Detail,
    string? UniqueId,
    string AlertLevel);

public sealed record AdminTopDishDto(
    string Name,
    int ProductId,
    string? PhotoUrl,
    int Quantity,
    double BarPercent);

public sealed record AdminStaffRosterRowDto(
    string Name,
    string Role,
    bool IsClockedIn,
    string StatusLabel);

public sealed record AdminActivityCardDto(
    string Title,
    string KindLabel,
    string DetailBlock,
    string NavView,
    string? NavFilter);

public sealed record AdminDashboardSummaryDto(
    int ActiveOrders,
    int PendingCashierOrders,
    int ReadyOrders,
    int OccupiedTables,
    int AvailableTables,
    int TotalTables,
    decimal TodayRevenueUsd,
    decimal TodayRevenueFc,
    decimal TodayExpensesUsd,
    DateTime GeneratedAtUtc,
    int OrdersCompletedToday,
    int InKitchenNow,
    int LowStockSkuCount,
    int ClockedInEmployees,
    int TotalActiveEmployees);

public sealed record AdminDashboardDto(
    string WelcomeName,
    string HeaderSubtitle,
    string ApiStatusText,
    bool ApiConnected,
    AdminPayrollAlertDto? PayrollAlert,
    AdminDashboardSummaryDto Summary,
    IReadOnlyList<AdminKpiCardDto> KpiCards,
    IReadOnlyList<AdminInventoryAlertRowDto> InventoryAlerts,
    IReadOnlyList<AdminTopDishDto> TopDishesToday,
    IReadOnlyList<decimal> HourlyRevenueToday,
    decimal HourlyRevenueChartMax,
    IReadOnlyList<AdminStaffRosterRowDto> StaffRoster,
    IReadOnlyList<AdminActivityCardDto> RecentActivity);
