using System.Globalization;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Localization;

public static class SalaryUiLocalizer
{
    public static CultureInfo UiCulture => AdminTextLocalizer.UiCulture;

    public static string FormatUsd(decimal amount) => "$" + amount.ToString("N2", UiCulture);

    public static string FormatPayrollMonth(int year, int month) =>
        new DateTime(year, month, 1).ToString("MMMM yyyy", UiCulture);

    public static string FormatShortDate(DateTime localDate) =>
        localDate.ToString(Loc.Language == "fr" ? "d MMM yyyy" : "MMM d, yyyy", UiCulture);

    public static void Apply(SalaryEmployeeRowVm row)
    {
        row.RateColumnText = row.UsesMonthlySalary
            ? Loc.Admin("salRatePerMonth", "{{amount}}/mo",
                new Dictionary<string, string> { ["amount"] = FormatUsd(row.ContractMonthlyUsd) })
            : Loc.Admin("salSetMonthlyShort", "Set monthly");

        row.PayrollPrimaryRateChipText = row.UsesMonthlySalary
            ? Loc.Admin("salMonthlyChip", "Monthly {{amount}}/mo",
                new Dictionary<string, string> { ["amount"] = FormatUsd(row.ContractMonthlyUsd) })
            : Loc.Admin("salSetMonthlyEmployees", "Set monthly salary (Employees)");

        row.ScheduledHoursChipText = Loc.Admin("salScheduledHours", "Scheduled hours {{hours}}",
            new Dictionary<string, string>
            {
                ["hours"] = row.ScheduledHoursMonth.ToString("0.#", CultureInfo.InvariantCulture)
            });

        row.ScheduledWorkdaysChipText = Loc.Admin("salWorkdays", "Workdays {{count}}",
            new Dictionary<string, string>
            {
                ["count"] = row.ScheduledWorkdays.ToString(CultureInfo.InvariantCulture)
            });

        row.BaseGrossChipText = Loc.Admin("salBaseGross", "Base gross {{amount}}",
            new Dictionary<string, string> { ["amount"] = FormatUsd(row.BaseGrossUsd) });

        row.AbsencesChipText = Loc.Admin("salAbsences", "Absences {{count}}",
            new Dictionary<string, string> { ["count"] = row.AbsenceDays.ToString(CultureInfo.InvariantCulture) });

        row.LatesChipText = Loc.Admin("salLates", "Lates {{count}}",
            new Dictionary<string, string> { ["count"] = row.LateDays.ToString(CultureInfo.InvariantCulture) });

        row.LateUnitsChipText = Loc.Admin("salLateUnits", "Late → units {{count}}",
            new Dictionary<string, string> { ["count"] = row.LatePenaltyAbsences.ToString(CultureInfo.InvariantCulture) });

        row.TotalUnitsChipText = Loc.Admin("salTotalUnits", "Total units {{count}}",
            new Dictionary<string, string> { ["count"] = row.TotalDeductionUnits.ToString(CultureInfo.InvariantCulture) });

        row.AfterAttendanceChipText = Loc.Admin("salAfterAttendance", "After attendance {{amount}}",
            new Dictionary<string, string> { ["amount"] = FormatUsd(row.BaseAfterAttendanceUsd) });

        row.SalesServedChipText = Loc.Admin("salSalesServed", "Sales served {{amount}}",
            new Dictionary<string, string> { ["amount"] = FormatUsd(row.MoneyGeneratedUsd) });

        row.SalesBonusChipText = Loc.Admin("salSalesBonus", "Sales bonus {{amount}}",
            new Dictionary<string, string> { ["amount"] = FormatUsd(row.BonusFivePercentUsd) });

        row.AdvancesDisplayText = FormatUsd(row.AdvancesDeductUsd);
        row.NetPayDisplayText = FormatUsd(row.NetPay);

        row.TableBaseGrossText = FormatUsd(row.BaseGrossUsd);
        row.TableAfterAttText = FormatUsd(row.BaseAfterAttendanceUsd);
        row.TableSalesText = FormatUsd(row.MoneyGeneratedUsd);
        row.TableBonusText = FormatUsd(row.BonusFivePercentUsd);
        row.TableAdvancesText = FormatUsd(row.AdvancesDeductUsd);
        row.TableNetPayText = FormatUsd(row.NetPay);

        ApplyPayStatus(row);
        ApplyActionLabels(row);
        ApplyReceiptAndStatus(row);
    }

    private static void ApplyPayStatus(SalaryEmployeeRowVm row)
    {
        var remaining = row.NetPay;
        var totalNet = row.TotalNetUsd;

        if (row.AlreadyPaid)
            row.PayStatusBadgeText = Loc.Admin("salPaidInFullBadge", "Paid in full");
        else if (row.IsPartiallyPaid)
            row.PayStatusBadgeText = Loc.Admin("salPartiallyPaidBadge", "Partially paid");
        else
            row.PayStatusBadgeText = Loc.Admin("salPendingPayBadge", "Pending pay");

        if (row.AlreadyPaid)
            row.HeaderMoneyChipText = Loc.Admin("salHeaderPaidFull", "Paid in full");
        else if (row.IsPartiallyPaid)
            row.HeaderMoneyChipText = Loc.Admin("salHeaderStillOwed",
                "Still owed {{remaining}} of {{total}}",
                new Dictionary<string, string>
                {
                    ["remaining"] = FormatUsd(remaining),
                    ["total"] = FormatUsd(totalNet)
                });
        else
            row.HeaderMoneyChipText = Loc.Admin("salHeaderNet", "Net {{amount}}",
                new Dictionary<string, string> { ["amount"] = FormatUsd(remaining) });
    }

    private static void ApplyActionLabels(SalaryEmployeeRowVm row)
    {
        if (row.AlreadyPaid)
        {
            row.PayrollActionLabel = Loc.Admin("salActionConfirmed", "Confirmed");
            row.PayrollActionLabelShort = Loc.Admin("salActionDoneShort", "Done");
        }
        else if (row.IsPartiallyPaid)
        {
            row.PayrollActionLabel = Loc.Admin("salActionAddPayment", "Add payment");
            row.PayrollActionLabelShort = Loc.Admin("salActionAddShort", "Add");
        }
        else
        {
            row.PayrollActionLabel = Loc.Admin("salActionConfirmPayroll", "Confirm payroll");
            row.PayrollActionLabelShort = Loc.Admin("salActionPayShort", "Pay");
        }

        row.NetPaySectionTitle = row.AlreadyPaid
            ? Loc.Admin("salNetPayTitle", "Net pay")
            : row.IsPartiallyPaid
                ? Loc.Admin("salStillToPayTitle", "Still to pay")
                : Loc.Admin("salNetPayTitle", "Net pay");
    }

    private static void ApplyReceiptAndStatus(SalaryEmployeeRowVm row)
    {
        row.PaidPrefix = Loc.Admin("salPaidPrefix", "Paid ");
        row.PaidOnSeparator = Loc.Admin("salPaidOn", "  on  ");

        row.PaidAmountDisplay = string.Empty;
        row.PaidDateDisplay = string.Empty;

        if (row.PaidAtUtc is not null)
        {
            var localPaid = row.PaidAtUtc.Value.ToLocalTime();
            row.PaidDateDisplay = FormatShortDate(localPaid);
            var remaining = row.NetPay;
            row.PaidAmountDisplay = remaining <= 0.005m
                ? Loc.Admin("salPaidAmountFull", "{{amount}} USD in full",
                    new Dictionary<string, string> { ["amount"] = FormatUsd(row.TotalNetUsd) })
                : Loc.Admin("salPaidAmountPartial", "{{paid}} of {{total}} USD",
                    new Dictionary<string, string>
                    {
                        ["paid"] = FormatUsd(row.PaidToDateUsd),
                        ["total"] = FormatUsd(row.TotalNetUsd)
                    });
        }

        row.StatusText = BuildStatusText(row);
    }

    private static string BuildStatusText(SalaryEmployeeRowVm row)
    {
        var remaining = row.NetPay;

        if (row.AlreadyPaid && row.PaidAtUtc is not null)
        {
            var localPaid = row.PaidAtUtc.Value.ToLocalTime();
            return Loc.Admin("salStatusPaidFullDetail",
                "Paid in full ({{amount}} USD). Last posting {{date}}.",
                new Dictionary<string, string>
                {
                    ["amount"] = row.TotalNetUsd.ToString("N2", UiCulture),
                    ["date"] = FormatShortDate(localPaid)
                });
        }

        if (row.AlreadyPaid)
            return Loc.Admin("salStatusPaid", "Paid");

        if (row.HasPayrollRecord && row.PaidAtUtc is not null && !row.AlreadyPaid)
        {
            var localPaid = row.PaidAtUtc.Value.ToLocalTime();
            return Loc.Admin("salStatusPartialDetail",
                "Partially paid {{paid}} of {{total}} USD — still owe {{remaining}}. Last payment {{date}}.",
                new Dictionary<string, string>
                {
                    ["paid"] = row.PaidToDateUsd.ToString("N2", UiCulture),
                    ["total"] = row.TotalNetUsd.ToString("N2", UiCulture),
                    ["remaining"] = remaining.ToString("N2", UiCulture),
                    ["date"] = FormatShortDate(localPaid)
                });
        }

        if (row.NeedsSalarySetup)
        {
            return Loc.Admin("salStatusSetMonthly",
                "Set monthly salary (USD) in Employees — required for payroll");
        }

        if (row.BaseGrossUsd <= 0.005m)
        {
            return row.UsesMonthlySalary
                ? Loc.Admin("salStatusNoGrossMonthly",
                    "No payroll gross this month — check join date or monthly salary in Employees")
                : Loc.Admin("salStatusNoGross",
                    "No payroll gross this month — set a positive monthly salary in Employees (or check join date and schedule)");
        }

        if (row.DaysLate > 0)
        {
            return Loc.Admin("salStatusPendingLate",
                "Pending — you are {{days}} day(s) late for pay (due last day of month)",
                new Dictionary<string, string> { ["days"] = row.DaysLate.ToString(CultureInfo.InvariantCulture) });
        }

        return Loc.Admin("salStatusDue",
            "Due on {{date}} (last day of month)",
            new Dictionary<string, string> { ["date"] = FormatShortDate(row.MonthEndDate) });
    }

    public static string FormatOverdueWarning(string monthYear, int days) =>
        Loc.Admin("salOverdueBody",
            "Payroll for {{monthYear}} is overdue. You are {{days}} day(s) past the pay date (last day of the month). Confirm payments below.",
            new Dictionary<string, string>
            {
                ["monthYear"] = monthYear,
                ["days"] = days.ToString(CultureInfo.InvariantCulture)
            });

    public static string FormatPaymentRemainingHint(
        bool hasPayrollRecord,
        decimal totalNet,
        decimal paidToDate,
        decimal remaining) =>
        hasPayrollRecord
            ? Loc.Admin("salDlgRemainingWithRecord",
                "Net this month: {{net}} — paid so far: {{paid}} — remaining: {{remaining}}",
                new Dictionary<string, string>
                {
                    ["net"] = FormatUsd(totalNet),
                    ["paid"] = FormatUsd(paidToDate),
                    ["remaining"] = FormatUsd(remaining)
                })
            : Loc.Admin("salDlgRemainingDue", "Net pay due: {{amount}}",
                new Dictionary<string, string> { ["amount"] = FormatUsd(remaining) });
}
