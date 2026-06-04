using System.Globalization;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Localization;

public static class AttendanceUiLocalizer
{
    public static void ApplyRow(AttendanceRowViewModel row)
    {
        row.DisplayShiftLine = Loc.Admin("attShiftLine", "Shift: {{name}} ({{window}})",
            new Dictionary<string, string>
            {
                ["name"] = AdminTextLocalizer.TranslateShift(row.ShiftName),
                ["window"] = row.ShiftWindowText
            });
        row.DisplayClockLine = Loc.Admin("attClockInLine", "Clock In: {{in}} | Clock Out: {{out}}",
            new Dictionary<string, string>
            {
                ["in"] = AdminTextLocalizer.TranslateEmployeeClockText(row.ClockInText),
                ["out"] = AdminTextLocalizer.TranslateEmployeeClockText(row.ClockOutText)
            });
        row.DisplayStatusLine = Loc.Admin("attStatusLine", "Status: {{status}}",
            new Dictionary<string, string>
            {
                ["status"] = AdminTextLocalizer.TranslateAttendanceRowStatus(row.StatusText)
            });
        row.DisplayPendingSalaryText = Loc.Admin("attPendingSalary", "Pending Salary: $ {{amount}}",
            new Dictionary<string, string>
            {
                ["amount"] = ExtractPendingSalaryAmount(row.PendingSalaryText)
            });
        row.DisplayLateJustificationText = string.IsNullOrWhiteSpace(row.LateJustificationText)
            ? string.Empty
            : Loc.Admin("attLateJustification", "Late justification: {{text}}",
                new Dictionary<string, string> { ["text"] = ExtractLateJustificationBody(row.LateJustificationText) });
    }

    public static void ApplyDayGroup(AttendanceDayGroupViewModel group, DateTime today)
    {
        group.DayText = group.WorkDate == today.Date
            ? AdminTextLocalizer.FormatTodayCalendarDay(group.WorkDate, group.IsDayValidated)
            : AdminTextLocalizer.FormatCalendarDay(group.WorkDate, group.IsDayValidated);
        group.EmployeesCountText = Loc.Admin("attEmployeesCount", "({{count}} employees)",
            new Dictionary<string, string> { ["count"] = group.RowCount.ToString(CultureInfo.InvariantCulture) });
        foreach (var row in group.Rows)
            ApplyRow(row);
    }

    private static string ExtractPendingSalaryAmount(string raw)
    {
        var idx = raw.LastIndexOf('$');
        return idx >= 0 ? raw[(idx + 1)..].Trim() : raw;
    }

    private static string ExtractLateJustificationBody(string raw)
    {
        const string prefix = "Late justification: ";
        return raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? raw[prefix.Length..] : raw;
    }
}
