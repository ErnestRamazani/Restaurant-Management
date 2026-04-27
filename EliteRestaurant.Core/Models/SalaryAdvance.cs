namespace EliteRestaurant.Core.Models;

/// <summary>Cash advance to an employee, deducted on the next monthly payroll confirmation for that period.</summary>
public class SalaryAdvance
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public decimal AmountUsd { get; set; }
    public DateTime GivenAt { get; set; }
    /// <summary>Payroll month this advance deducts from (defaults from Salary screen when recorded).</summary>
    public int? ForPayrollYear { get; set; }
    public int? ForPayrollMonth { get; set; }
    public int? AppliedPayrollYear { get; set; }
    public int? AppliedPayrollMonth { get; set; }
    public string Note { get; set; } = string.Empty;
}
