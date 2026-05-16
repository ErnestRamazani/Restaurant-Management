namespace EliteRestaurant.Core.Models;

/// <summary>One day cell for read-only weekly schedule summaries (e.g. Employees profile expander).</summary>
public sealed class EmployeeScheduleDayRow
{
    public EmployeeScheduleDayRow(string dayShortName, string badgeText, string badgeVariant)
    {
        DayShortName = dayShortName;
        BadgeText = badgeText;
        BadgeVariant = badgeVariant;
    }

    /// <summary>Short label, e.g. Mon–Sun.</summary>
    public string DayShortName { get; }

    /// <summary>Pill label: Off, Morning, or Evening.</summary>
    public string BadgeText { get; }

    /// <summary>Off, Morning, or Evening — drives accent styling in the UI.</summary>
    public string BadgeVariant { get; }
}
