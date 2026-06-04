using System.Globalization;
using EliteRestaurant.Core.Models;

namespace EliteRestaurantPro.Localization;

public static class EmployeeUiLocalizer
{
    public static void Apply(Employee employee)
    {
        employee.DisplayRole = AdminTextLocalizer.TranslateRole(employee.Role);
        employee.DisplayEmploymentStatus = AdminTextLocalizer.TranslateEmploymentStatus(employee.EmploymentStatus);
        employee.DisplayAttendanceStatus = employee.CanClockOut
            ? Loc.Admin("staffStatusActive", "Active")
            : Loc.Admin("empNotActive", "Not active");
        employee.DisplaySignInBadge = string.IsNullOrWhiteSpace(employee.SignInId)
            ? string.Empty
            : Loc.Admin("empSignInBadge", "Sign-in {{id}}", new Dictionary<string, string> { ["id"] = employee.SignInId });
        employee.TodayClockInText = AdminTextLocalizer.TranslateEmployeeClockText(employee.TodayClockInText);
        employee.TodayClockOutText = AdminTextLocalizer.TranslateEmployeeClockText(employee.TodayClockOutText);
        employee.DisplayClockInLine = Loc.Admin("empClockIn", "Clock in") + ": " + employee.TodayClockInText;
        employee.DisplayClockOutLine = Loc.Admin("empClockOut", "Clock out") + ": " + employee.TodayClockOutText;
        employee.DisplayOrdersServedText = Loc.Admin("empOrdersServed", "Total Orders Served: {{count}}",
            new Dictionary<string, string> { ["count"] = employee.TotalOrdersServed.ToString(CultureInfo.InvariantCulture) });
        employee.DisplaySalesGeneratedText = Loc.Admin("empSalesGenerated", "Total Sales Generated: $ {{amount}}",
            new Dictionary<string, string> { ["amount"] = employee.TotalSalesGenerated.ToString("N2", CultureInfo.InvariantCulture) });
        employee.DisplayPendingSalaryText = Loc.Admin("empPendingSalary", "Pending Salary Today: $ {{amount}}",
            new Dictionary<string, string> { ["amount"] = employee.PendingSalaryToday.ToString("N2", CultureInfo.InvariantCulture) });
        employee.DisplayMonthlySalaryText = employee.MonthlySalaryUSD <= 0m
            ? Loc.Admin("empMonthlyUnset", "Monthly — set salary in Edit")
            : Loc.Admin("empMonthlySalary", "Monthly $ {{amount}}",
                new Dictionary<string, string> { ["amount"] = employee.MonthlySalaryUSD.ToString("N2", CultureInfo.InvariantCulture) });
        employee.DisplayTenureText = Loc.Admin("empTenure", "Tenure {{days}} day(s)",
            new Dictionary<string, string> { ["days"] = employee.DaysSinceJoined.ToString(CultureInfo.InvariantCulture) });
        employee.DisplayPhoneText = string.IsNullOrWhiteSpace(employee.PhoneNumber)
            ? Loc.Admin("empPhone", "Phone") + " N/A"
            : Loc.Admin("empPhone", "Phone") + " " + employee.PhoneNumber;
        employee.DisplayJoinedText = Loc.Admin("empJoined", "Joined") + " " + employee.JoinDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        employee.DisplayNotesForView = string.IsNullOrWhiteSpace(employee.Notes)
            ? Loc.Admin("empNoNotes", "No notes yet.")
            : employee.Notes.Trim();
        RebuildLocalizedScheduleDays(employee);
    }

    public static void RebuildLocalizedScheduleDays(Employee employee)
    {
        employee.ScheduleDays.Clear();
        void Add(string dayShort, string? raw)
        {
            var (badgeText, variant) = MapShiftToBadge(raw);
            employee.ScheduleDays.Add(new EmployeeScheduleDayRow(
                AdminTextLocalizer.TranslateDayShort(dayShort),
                badgeText,
                variant));
        }

        Add("Mon", employee.MondayShift);
        Add("Tue", employee.TuesdayShift);
        Add("Wed", employee.WednesdayShift);
        Add("Thu", employee.ThursdayShift);
        Add("Fri", employee.FridayShift);
        Add("Sat", employee.SaturdayShift);
        Add("Sun", employee.SundayShift);
    }

    private static (string BadgeText, string BadgeVariant) MapShiftToBadge(string? configured)
    {
        var n = (configured ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(n) || n.Equals("Off", StringComparison.OrdinalIgnoreCase))
            return (AdminTextLocalizer.TranslateShift("Off"), "Off");

        if (n.Equals("Full Day", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Full", StringComparison.OrdinalIgnoreCase) && n.Contains("Day", StringComparison.OrdinalIgnoreCase))
            return (AdminTextLocalizer.TranslateShift("Full Day"), "FullDay");

        if (n.Contains("Night", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Evening", StringComparison.OrdinalIgnoreCase))
            return (AdminTextLocalizer.TranslateShift("Evening"), "Evening");

        if (n.Contains("Morning", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Afternoon", StringComparison.OrdinalIgnoreCase))
            return (AdminTextLocalizer.TranslateShift("Morning Shift"), "Morning");

        return (AdminTextLocalizer.TranslateShift(n), "Morning");
    }
}
