namespace EliteRestaurant.Contracts.Admin;

public sealed record AdminOrderSummaryRow(
    int Id,
    string OrderId,
    DateTime CreatedAt,
    string TableLabel,
    string ServerName,
    decimal GrandTotal,
    string ItemsPreview,
    string Status);

public sealed record AdminEmployeeWebRow(
    int Id,
    string UniqueId,
    string Name,
    string Role,
    string Notes,
    string EmploymentStatus,
    string MondayShift,
    string TuesdayShift,
    string WednesdayShift,
    string ThursdayShift,
    string FridayShift,
    string SaturdayShift,
    string SundayShift,
    string ProfilePhotoUrl,
    string PhoneNumber,
    string JoinDate,
    string WorkScheduleSummary,
    string TodayClockInText,
    string TodayClockOutText,
    string AttendanceStatus);
