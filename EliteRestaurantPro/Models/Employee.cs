using System.ComponentModel.DataAnnotations.Schema;

namespace EliteRestaurantPro.Models;

public class Employee
{
    public int Id { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    /// <summary>Short memorable ID for tablet sign-in with <see cref="PinCode"/> (unique when non-empty).</summary>
    public string SignInId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string PinCode { get; set; } = string.Empty;
    public string ProfileImagePath { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    /// <summary>Optional reference amount in USD. Monthly payroll on the Salary screen uses <see cref="HourlyRate"/> × scheduled shift hours.</summary>
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

    private static void AddShift(List<string> entries, string day, string shift)
    {
        if (string.IsNullOrWhiteSpace(shift) || shift.Equals("Off", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        entries.Add($"{day} ({shift})");
    }
}
