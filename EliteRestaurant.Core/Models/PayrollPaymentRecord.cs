namespace EliteRestaurant.Core.Models;

/// <summary>Snapshot when monthly payroll is confirmed (for Reports and audit).</summary>
public class PayrollPaymentRecord
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    /// <summary>Payroll base gross for the month (prorated monthly salary or legacy hourly × scheduled hours).</summary>
    public decimal MonthlySalaryUsd { get; set; }
    public int AbsenceDays { get; set; }
    public int LateDays { get; set; }
    public int LatePenaltyUnits { get; set; }
    public int TotalDeductionUnits { get; set; }
    public decimal MoneyGeneratedUsd { get; set; }
    public decimal BonusFivePercentUsd { get; set; }
    public decimal AdvancesDeductedUsd { get; set; }
    public decimal NetPayUsd { get; set; }
    /// <summary>Cumulative salary cash posted for this payroll month (may be less than <see cref="NetPayUsd"/> until fully paid).</summary>
    public decimal PaidToDateUsd { get; set; }
    public DateTime PaidAtUtc { get; set; }
}
