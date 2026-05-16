using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Reporting;

public static class MoneyTransactionReportHelper
{
    /// <summary>Normalize stored <see cref="MoneyTransaction.Date"/> to a local instant for range filters and sorting.</summary>
    public static DateTime ToLocalInstant(DateTime d) =>
        d.Kind switch
        {
            DateTimeKind.Utc => d.ToLocalTime(),
            DateTimeKind.Local => d,
            _ => DateTime.SpecifyKind(d, DateTimeKind.Local),
        };

    public static bool IsSalaryExpense(MoneyTransaction t) =>
        string.Equals(t.Type, "Expense", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(t.Category, "Salary", StringComparison.OrdinalIgnoreCase);

    public static string LedgerEventType(MoneyTransaction t)
    {
        var j = t.Justification ?? string.Empty;
        return j.Contains("| ADVANCE:", StringComparison.Ordinal)
            ? "Salary advance (Money)"
            : "Salary payment (Money)";
    }

    public static bool TryParseEmployeeIdFromSalaryJustification(string justification, out int employeeId)
    {
        employeeId = 0;
        if (string.IsNullOrEmpty(justification))
            return false;

        const string marker = "| EMP:";
        var idx = justification.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
            return false;

        var from = idx + marker.Length;
        var end = justification.IndexOf('|', from);
        var slice = end >= 0 ? justification.Substring(from, end - from) : justification[from..];
        return int.TryParse(slice, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out employeeId);
    }
}
