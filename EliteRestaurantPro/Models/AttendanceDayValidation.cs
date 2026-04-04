namespace EliteRestaurantPro.Models;

/// <summary>Marks a calendar workday as attendance-validated (admin confirmed; single use).</summary>
public class AttendanceDayValidation
{
    public int Id { get; set; }
    public DateTime WorkDate { get; set; }
    public DateTime ValidatedAtUtc { get; set; }
}
