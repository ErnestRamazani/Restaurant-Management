namespace EliteRestaurant.Core.Models;

public class EmployeeAttendance
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime WorkDate { get; set; } = DateTime.Today;
    public DateTime? ClockInTime { get; set; }
    public DateTime? ClockOutTime { get; set; }
    public string ClockInStatus { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public bool IsAbsence { get; set; }
    public string AbsenceJustification { get; set; } = string.Empty;
}
