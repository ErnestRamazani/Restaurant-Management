namespace EliteRestaurant.Contracts.Admin;

public sealed record AdminDashboardSummaryDto(
    int ActiveOrders,
    int PendingCashierOrders,
    int ReadyOrders,
    int OccupiedTables,
    int AvailableTables,
    decimal TodayRevenueUsd,
    DateTime GeneratedAtUtc);

public sealed record AdminActivityDto(
    string Title,
    string Description,
    string Kind,
    DateTime CreatedAtUtc);

public sealed record AdminDashboardDto(
    AdminDashboardSummaryDto Summary,
    IReadOnlyList<AdminActivityDto> RecentActivity);
