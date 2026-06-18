using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using EliteRestaurant.Core.Tenancy;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Models;

public class Employee : IRestaurantScoped
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    /// <summary>Short memorable ID for tablet sign-in with <see cref="PinCode"/> (unique when non-empty).</summary>
    public string SignInId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    /// <summary>When <see cref="Role"/> is <c>Other</c>, free-text job title (e.g. Janitor, Security).</summary>
    public string? CustomRoleTitle { get; set; }
    /// <summary>BCrypt hash of the tablet PIN (never a plaintext PIN in normal operation).</summary>
    public string PinCode { get; set; } = string.Empty;
    public string ProfileImagePath { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    /// <summary>Hourly wage in USD — used for payroll when <see cref="MonthlySalaryUSD"/> is zero.</summary>
    public decimal HourlyRate { get; set; }
    /// <summary>
    /// Fixed monthly gross in USD. When greater than zero, Salary payroll uses this (calendar-prorated after <see cref="JoinDate"/>) as the base before attendance, bonus, and advances.
    /// </summary>
    public decimal MonthlySalaryUSD { get; set; }
    public DateTime JoinDate { get; set; } = DateTime.Today;
    public string EmploymentStatus { get; set; } = "Active";
    public string Notes { get; set; } = string.Empty;
    public string MondayShift { get; set; } = "Off";
    public string TuesdayShift { get; set; } = "Off";
    public string WednesdayShift { get; set; } = "Off";
    public string ThursdayShift { get; set; } = "Off";
    public string FridayShift { get; set; } = "Off";
    public string SaturdayShift { get; set; } = "Off";
    public string SundayShift { get; set; } = "Off";

    /// <summary>UI language preference: <c>en</c> or <c>fr</c>.</summary>
    public string PreferredLanguage { get; set; } = "en";

    /// <summary>Auto-applied when an order is linked to this employee's mirrored client record.</summary>
    public decimal StaffMealDiscountPercent { get; set; }

    [NotMapped]
    public int TotalOrdersServed { get; set; }

    [NotMapped]
    public decimal TotalSalesGenerated { get; set; }

    [NotMapped]
    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name))
                return "E";

            var parts = Name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0][0].ToString().ToUpperInvariant();

            return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
        }
    }

    [NotMapped]
    public int DaysSinceJoined => Math.Max(0, (DateTime.Today - JoinDate.Date).Days);

    [NotMapped]
    public string TodayClockInText { get; set; } = "Not clocked in";

    [NotMapped]
    public string TodayClockOutText { get; set; } = "Not clocked out";

    [NotMapped]
    public bool CanClockIn { get; set; } = true;

    [NotMapped]
    public bool CanClockOut { get; set; } = false;

    [NotMapped]
    public decimal PendingSalaryToday { get; set; }

    [NotMapped]
    [JsonIgnore]
    public bool CanDeleteFromEmployeesScreen { get; set; } = true;

    [NotMapped]
    [JsonIgnore]
    public string DisplayRole { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayEmploymentStatus { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayAttendanceStatus { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplaySignInBadge { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayOrdersServedText { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplaySalesGeneratedText { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayPendingSalaryText { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayMonthlySalaryText { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayTenureText { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayPhoneText { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayJoinedText { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayClockInLine { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayClockOutLine { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string DisplayNotesForView { get; set; } = string.Empty;

    [NotMapped]
    public string WorkScheduleSummary
    {
        get
        {
            var entries = new List<string>();
            AddShift(entries, "Monday", MondayShift);
            AddShift(entries, "Tuesday", TuesdayShift);
            AddShift(entries, "Wednesday", WednesdayShift);
            AddShift(entries, "Thursday", ThursdayShift);
            AddShift(entries, "Friday", FridayShift);
            AddShift(entries, "Saturday", SaturdayShift);
            AddShift(entries, "Sunday", SundayShift);
            return entries.Count == 0 ? "No schedule assigned." : string.Join(", ", entries);
        }
    }

    /// <summary>Mon–Sun display rows for UI; refresh with <see cref="RebuildScheduleDays"/> after shift fields change.</summary>
    [NotMapped]
    [JsonIgnore]
    public ObservableCollection<EmployeeScheduleDayRow> ScheduleDays { get; } = new();

    /// <summary>Rebuilds <see cref="ScheduleDays"/> from per-day shift fields (Mon–Sun).</summary>
    public void RebuildScheduleDays()
    {
        ScheduleDays.Clear();
        void Add(string dayShort, string? raw)
        {
            var (badgeText, variant) = MapShiftToBadge(raw);
            ScheduleDays.Add(new EmployeeScheduleDayRow(dayShort, badgeText, variant));
        }

        Add("Mon", MondayShift);
        Add("Tue", TuesdayShift);
        Add("Wed", WednesdayShift);
        Add("Thu", ThursdayShift);
        Add("Fri", FridayShift);
        Add("Sat", SaturdayShift);
        Add("Sun", SundayShift);
    }

    private static (string BadgeText, string BadgeVariant) MapShiftToBadge(string? configured)
    {
        var n = (configured ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(n) || n.Equals("Off", StringComparison.OrdinalIgnoreCase))
            return ("Off", "Off");

        if (AttendanceScheduleHelper.IsFullDayShift(n))
            return ("Full Day", "FullDay");

        if (n.Contains("Night", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Evening", StringComparison.OrdinalIgnoreCase))
            return ("Evening", "Evening");

        if (n.Contains("Morning", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Afternoon", StringComparison.OrdinalIgnoreCase))
            return ("Morning", "Morning");

        return ("Morning", "Morning");
    }

    private static void AddShift(List<string> entries, string day, string shift)
    {
        if (string.IsNullOrWhiteSpace(shift) || shift.Equals("Off", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        entries.Add($"{day} ({shift})");
    }
}
