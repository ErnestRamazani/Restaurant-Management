namespace EliteRestaurant.Contracts.Admin;

public sealed record AdminReportEntityRefDto(int Id, string UniqueId, string Name, string Subtitle);

public sealed record AdminReportTimeEntryDto(
    DateTime EventTime,
    string EventType,
    string Summary,
    string RelatedInfo,
    string EntityContext,
    int OrdersCount,
    int ItemCount,
    decimal UnitUsage);

public sealed record AdminReportDayGroupDto(
    DateTime Day,
    string DayText,
    string TotalsText,
    IReadOnlyList<AdminReportTimeEntryDto> Entries);

public sealed record AdminReportListsResponse(
    IReadOnlyList<AdminReportEntityRefDto> Employees,
    IReadOnlyList<AdminReportEntityRefDto> Tables,
    IReadOnlyList<AdminReportEntityRefDto> InventoryItems,
    IReadOnlyList<AdminReportEntityRefDto> MenuItems);

public sealed record AdminReportRangeSummaryResponse(
    string SummaryText,
    IReadOnlyList<AdminReportDayGroupDto> Days);

public sealed record AdminReportEmployeeDetailResponse(
    string EmployeeSummary,
    string EmployeeNotes,
    string EmployeePayrollHistory,
    IReadOnlyList<AdminReportDayGroupDto> TimelineDays);

public sealed record AdminReportTableDetailResponse(
    string TableSummary,
    string TableCurrentServer,
    IReadOnlyList<AdminReportDayGroupDto> TimelineDays);

public sealed record AdminReportInventoryDetailResponse(
    string InventorySummary,
    string InventoryNotes,
    IReadOnlyList<AdminReportDayGroupDto> TimelineDays);

public sealed record AdminReportMenuDetailResponse(
    string MenuSummary,
    string MenuIngredientsSummary,
    IReadOnlyList<AdminReportDayGroupDto> TimelineDays);
