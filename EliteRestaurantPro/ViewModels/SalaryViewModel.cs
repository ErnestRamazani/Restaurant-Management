using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using EliteRestaurantPro.Data;
using EliteRestaurantPro.Models;
using EliteRestaurantPro.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurantPro.ViewModels;

public enum SalaryEmployeeVisibilityFilter
{
    All,
    PaidOnly,
    UnpaidOnly
}

public sealed class SalaryEmployeeRowVm
{
    public int EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string UniqueId { get; init; } = string.Empty;
    public decimal HourlyRateUsd { get; init; }
    public decimal ScheduledHoursMonth { get; init; }
    public int ScheduledWorkdays { get; init; }
    /// <summary>Hourly rate × scheduled hours for the month (payroll base).</summary>
    public decimal BaseGrossUsd { get; init; }
    public int AbsenceDays { get; init; }
    public int LateDays { get; init; }
    public int LatePenaltyAbsences { get; init; }
    public int TotalDeductionUnits { get; init; }
    public decimal BaseAfterAttendanceUsd { get; init; }
    public decimal MoneyGeneratedUsd { get; init; }
    public decimal BonusFivePercentUsd { get; init; }
    public decimal AdvancesDeductUsd { get; init; }
    public decimal NetPay { get; init; }
    public bool AlreadyPaid { get; init; }
    public string StatusText { get; init; } = string.Empty;

    /// <summary>True when a stored payroll payment record exists (amount and paid date available for display).</summary>
    public bool HasPaidReceiptDetail { get; init; }

    /// <summary>Formatted for inline display, e.g. <c>$1,322.65 USD</c>.</summary>
    public string PaidAmountDisplay { get; init; } = string.Empty;

    public string PaidDateDisplay { get; init; } = string.Empty;

    /// <summary>Shows generic <see cref="StatusText"/> line when there is no receipt breakdown.</summary>
    public bool ShowPlainStatusLine => !HasPaidReceiptDetail;

    public bool CanConfirmPayroll => !AlreadyPaid && BaseGrossUsd > 0m;
}

public sealed class SalaryAdvanceEmployeePickVm
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>EliteComboBox uses a custom template; closed state falls back to <see cref="object.ToString"/> unless an ItemTemplate applies.</summary>
    public override string ToString() => DisplayName;
}

public sealed class SalaryViewModel : AdminBaseViewModel
{
    private int _payrollYear;
    private int _payrollMonth;
    private int _daysPastPayDay;
    private bool _showPayrollOverdueWarning;
    private string _payrollOverdueWarningText = string.Empty;
    private SalaryAdvanceEmployeePickVm? _selectedAdvanceEmployee;
    private string _advanceAmountText = string.Empty;
    private string _advanceNoteText = string.Empty;
    private bool _useInteractiveCards = true;
    private SalaryEmployeeVisibilityFilter _employeeVisibilityFilter = SalaryEmployeeVisibilityFilter.All;
    private readonly List<SalaryEmployeeRowVm> _allPayrollRows = [];

    public override string ActivePage => "Salary";

    /// <summary>Card / expander layout. When false, <see cref="UseTableView"/> is true.</summary>
    public bool UseInteractiveCards
    {
        get => _useInteractiveCards;
        set
        {
            if (!SetField(ref _useInteractiveCards, value))
                return;
            OnPropertyChanged(nameof(UseTableView));
        }
    }

    /// <summary>Compact DataGrid layout (two-way with CheckBox).</summary>
    public bool UseTableView
    {
        get => !_useInteractiveCards;
        set
        {
            if (value == !_useInteractiveCards)
                return;
            UseInteractiveCards = !value;
        }
    }

    public ObservableCollection<int> MonthChoices { get; } = new(Enumerable.Range(1, 12).ToList());
    public ObservableCollection<int> YearChoices { get; } = [];

    public ObservableCollection<SalaryAdvanceEmployeePickVm> AdvanceEmployees { get; } = [];

    public SalaryAdvanceEmployeePickVm? SelectedAdvanceEmployee
    {
        get => _selectedAdvanceEmployee;
        set => SetField(ref _selectedAdvanceEmployee, value);
    }

    public string AdvanceAmountText
    {
        get => _advanceAmountText;
        set => SetField(ref _advanceAmountText, value);
    }

    public string AdvanceNoteText
    {
        get => _advanceNoteText;
        set => SetField(ref _advanceNoteText, value);
    }

    public int SelectedPayrollMonth
    {
        get => _payrollMonth;
        set
        {
            if (!SetField(ref _payrollMonth, value))
                return;
            ReloadRows();
        }
    }

    public int SelectedPayrollYear
    {
        get => _payrollYear;
        set
        {
            if (!SetField(ref _payrollYear, value))
                return;
            ReloadRows();
        }
    }

    public string PayrollPeriodLabel => PayrollCalculator.FormatPayrollMonthLabel(SelectedPayrollYear, SelectedPayrollMonth);

    /// <summary>Filtered list for the UI (cards and table). Full month data lives in <see cref="_allPayrollRows"/>.</summary>
    public ObservableCollection<SalaryEmployeeRowVm> Rows { get; } = [];

    public SalaryEmployeeVisibilityFilter EmployeeVisibilityFilter
    {
        get => _employeeVisibilityFilter;
        set
        {
            if (!SetField(ref _employeeVisibilityFilter, value))
                return;
            OnPropertyChanged(nameof(FilterShowAllEmployees));
            OnPropertyChanged(nameof(FilterShowPaidOnly));
            OnPropertyChanged(nameof(FilterShowUnpaidOnly));
            ApplyEmployeeFilter();
        }
    }

    public bool FilterShowAllEmployees
    {
        get => _employeeVisibilityFilter == SalaryEmployeeVisibilityFilter.All;
        set
        {
            if (value)
                EmployeeVisibilityFilter = SalaryEmployeeVisibilityFilter.All;
        }
    }

    public bool FilterShowPaidOnly
    {
        get => _employeeVisibilityFilter == SalaryEmployeeVisibilityFilter.PaidOnly;
        set
        {
            if (value)
                EmployeeVisibilityFilter = SalaryEmployeeVisibilityFilter.PaidOnly;
        }
    }

    public bool FilterShowUnpaidOnly
    {
        get => _employeeVisibilityFilter == SalaryEmployeeVisibilityFilter.UnpaidOnly;
        set
        {
            if (value)
                EmployeeVisibilityFilter = SalaryEmployeeVisibilityFilter.UnpaidOnly;
        }
    }

    public int DaysPastPayDay
    {
        get => _daysPastPayDay;
        private set => SetField(ref _daysPastPayDay, value);
    }

    public bool ShowPayrollOverdueWarning
    {
        get => _showPayrollOverdueWarning;
        private set => SetField(ref _showPayrollOverdueWarning, value);
    }

    public string PayrollOverdueWarningText
    {
        get => _payrollOverdueWarningText;
        private set => SetField(ref _payrollOverdueWarningText, value);
    }

    public ICommand RefreshPayrollCommand { get; }
    /// <summary>Finalize payroll for every unpaid employee with base gross in the selected month.</summary>
    public ICommand ConfirmAllPayrollCommand { get; }
    /// <summary>Finalize payroll for one employee (row command parameter).</summary>
    public ICommand ConfirmSinglePayrollCommand { get; }
    public ICommand RecordSalaryAdvanceCommand { get; }

    public SalaryViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        var y = DateTime.Today.Year;
        for (var i = y - 2; i <= y + 1; i++)
            YearChoices.Add(i);

        var prev = DateTime.Today.AddMonths(-1);
        _payrollYear = prev.Year;
        _payrollMonth = prev.Month;

        RefreshPayrollCommand = new RelayCommand(_ => ReloadRows());
        ConfirmAllPayrollCommand = new RelayCommand(_ => ConfirmAllPayments());
        ConfirmSinglePayrollCommand = new RelayCommand(
            p => ConfirmSinglePayment(p as SalaryEmployeeRowVm),
            p => p is SalaryEmployeeRowVm r && r.CanConfirmPayroll);
        RecordSalaryAdvanceCommand = new RelayCommand(_ => RecordSalaryAdvance());
        ReloadRows();
    }

    private void ApplyEmployeeFilter()
    {
        Rows.Clear();
        IEnumerable<SalaryEmployeeRowVm> query = _employeeVisibilityFilter switch
        {
            SalaryEmployeeVisibilityFilter.PaidOnly => _allPayrollRows.Where(r => r.AlreadyPaid),
            SalaryEmployeeVisibilityFilter.UnpaidOnly => _allPayrollRows.Where(r => !r.AlreadyPaid),
            _ => _allPayrollRows
        };
        foreach (var r in query)
            Rows.Add(r);
    }

    private void ReloadRows()
    {
        _allPayrollRows.Clear();
        Rows.Clear();
        AdvanceEmployees.Clear();

        using var db = new AppDbContext();
        var start = new DateTime(SelectedPayrollYear, SelectedPayrollMonth, 1).Date;
        var endExclusive = start.AddMonths(1);
        var monthEnd = new DateTime(
            SelectedPayrollYear,
            SelectedPayrollMonth,
            DateTime.DaysInMonth(SelectedPayrollYear, SelectedPayrollMonth)).Date;

        var employees = db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active")
            .OrderBy(e => e.Name)
            .ToList();

        foreach (var e in employees)
        {
            if (FinancialTransactionService.HasMonthlySalaryPayment(db, e.Id, SelectedPayrollYear, SelectedPayrollMonth))
                continue;
            AdvanceEmployees.Add(new SalaryAdvanceEmployeePickVm
            {
                Id = e.Id,
                DisplayName = $"{e.Name} ({e.UniqueId})"
            });
        }

        var prevId = SelectedAdvanceEmployee?.Id;
        SelectedAdvanceEmployee = AdvanceEmployees.FirstOrDefault(x => x.Id == prevId)
                                  ?? AdvanceEmployees.FirstOrDefault();

        var attendancesByEmployee = db.EmployeeAttendances.AsNoTracking()
            .Where(a => a.WorkDate >= start && a.WorkDate < endExclusive)
            .ToList()
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.AsEnumerable());

        var payrollRecords = db.PayrollPaymentRecords.AsNoTracking()
            .Where(p => p.Year == SelectedPayrollYear && p.Month == SelectedPayrollMonth)
            .ToDictionary(p => p.EmployeeId);

        foreach (var emp in employees)
        {
            attendancesByEmployee.TryGetValue(emp.Id, out var attRows);
            var rows = attRows ?? Enumerable.Empty<EmployeeAttendance>();
            var (abs, late, pen, total) =
                PayrollCalculator.CountAttendanceUnitsForPayroll(emp, SelectedPayrollYear, SelectedPayrollMonth, rows);

            var (schedHours, workdays, grossPay) =
                PayrollCalculator.GetHourlyGrossForPayrollMonth(emp, SelectedPayrollYear, SelectedPayrollMonth);

            var money = PayrollSupport.SumServerCompletedOrderMerchandiseUsd(db, emp.Id, start, endExclusive);
            var bonus = PayrollCalculator.ComputeBonusUsd(money);
            var advances = PayrollSupport.SumPendingAdvancesForPayrollMonth(
                db,
                emp.Id,
                SelectedPayrollYear,
                SelectedPayrollMonth);
            var baseAfter = PayrollCalculator.ComputeBaseAfterAttendanceUsd(grossPay, workdays, total);
            var net = PayrollCalculator.ComputeFinalNetPayUsd(
                grossPay,
                workdays,
                total,
                money,
                advances);

            var paid = FinancialTransactionService.HasMonthlySalaryPayment(db, emp.Id, SelectedPayrollYear, SelectedPayrollMonth);
            payrollRecords.TryGetValue(emp.Id, out var payRec);

            var advancesDisplay = paid ? 0m : advances;
            var netDisplay = paid ? 0m : net;

            var lastDay = monthEnd;
            var daysLate = DateTime.Today > lastDay ? Math.Max(0, (DateTime.Today - lastDay).Days) : 0;

            var hasReceipt = payRec is not null;
            string paidAmountDisplay = string.Empty;
            string paidDateDisplay = string.Empty;
            if (payRec is not null)
            {
                var localPaid = payRec.PaidAtUtc.ToLocalTime();
                paidAmountDisplay = $"${payRec.NetPayUsd:N2} USD";
                paidDateDisplay = localPaid.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);
            }

            string status;
            if (paid && payRec is not null)
            {
                var localPaid = payRec.PaidAtUtc.ToLocalTime();
                status = $"Paid ${payRec.NetPayUsd:N2} USD on {localPaid:MMM d, yyyy}";
            }
            else if (paid)
            {
                status = "Paid";
            }
            else if (emp.HourlyRate <= 0m)
            {
                status = "Set hourly rate (USD) in Employees — required for payroll";
            }
            else if (workdays == 0)
            {
                status = "No scheduled work this month (all Off) — adjust weekly shifts in Employees";
            }
            else if (daysLate > 0)
            {
                status = $"Pending — you are {daysLate} day(s) late for pay (due last day of month)";
            }
            else
            {
                status = $"Due on {lastDay:MMM d, yyyy} (last day of month)";
            }

            _allPayrollRows.Add(new SalaryEmployeeRowVm
            {
                EmployeeId = emp.Id,
                EmployeeName = emp.Name,
                UniqueId = emp.UniqueId,
                HourlyRateUsd = emp.HourlyRate,
                ScheduledHoursMonth = schedHours,
                ScheduledWorkdays = workdays,
                BaseGrossUsd = grossPay,
                AbsenceDays = abs,
                LateDays = late,
                LatePenaltyAbsences = pen,
                TotalDeductionUnits = total,
                BaseAfterAttendanceUsd = baseAfter,
                MoneyGeneratedUsd = money,
                BonusFivePercentUsd = bonus,
                AdvancesDeductUsd = advancesDisplay,
                NetPay = netDisplay,
                AlreadyPaid = paid,
                StatusText = status,
                HasPaidReceiptDetail = hasReceipt,
                PaidAmountDisplay = paidAmountDisplay,
                PaidDateDisplay = paidDateDisplay
            });
        }

        ApplyEmployeeFilter();

        var anyUnpaid = _allPayrollRows.Any(r => r.BaseGrossUsd > 0m && !r.AlreadyPaid);
        var pastMonthEnd = DateTime.Today > monthEnd;
        ShowPayrollOverdueWarning = anyUnpaid && pastMonthEnd;
        DaysPastPayDay = ShowPayrollOverdueWarning ? Math.Max(0, (DateTime.Today - monthEnd).Days) : 0;
        PayrollOverdueWarningText = ShowPayrollOverdueWarning
            ? $"Payroll for {PayrollPeriodLabel} is overdue. You are {DaysPastPayDay} day(s) past the pay date (last day of the month). Confirm payments below."
            : string.Empty;

        OnPropertyChanged(nameof(PayrollPeriodLabel));
        CommandManager.InvalidateRequerySuggested();
    }

    private void ConfirmAllPayments()
    {
        var pending = _allPayrollRows.Where(r => !r.AlreadyPaid && r.BaseGrossUsd > 0m).ToList();
        if (pending.Count == 0)
        {
            MessageBox.Show(
                "No employees to finalize for this month. Each person needs an hourly rate (USD) and at least one scheduled workday (not Off) in the month.",
                "Salary",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Finalize payroll for all {pending.Count} unpaid employee(s) for {PayrollPeriodLabel}? Matching salary advances (for this payroll month) reduce net pay. A Money expense is posted for each positive net pay.",
            "Confirm all payroll",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        RunPayrollForRows(pending);
    }

    private void ConfirmSinglePayment(SalaryEmployeeRowVm? row)
    {
        if (row is null || row.AlreadyPaid || row.BaseGrossUsd <= 0m)
            return;

        var confirm = MessageBox.Show(
            $"Finalize payroll only for {row.EmployeeName} for {PayrollPeriodLabel}? Matching salary advances for this month reduce net pay.",
            "Confirm payroll",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        RunPayrollForRows([row]);
    }

    private void RunPayrollForRows(IReadOnlyList<SalaryEmployeeRowVm> rows)
    {
        using var db = new AppDbContext();
        foreach (var row in rows)
        {
            FinancialTransactionService.RecordMonthlySalaryPayment(
                db,
                row.EmployeeId,
                SelectedPayrollYear,
                SelectedPayrollMonth);
        }

        db.SaveChanges();
        MessageBox.Show(
            "Payroll saved. Daily and Employees reports include Money salary lines for the payment date. Employee timeline shows advances and payments.",
            "Salary",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        ReloadRows();
    }

    private void RecordSalaryAdvance()
    {
        if (SelectedAdvanceEmployee is null)
        {
            MessageBox.Show("Select an employee.", "Salary advance", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!decimal.TryParse(AdvanceAmountText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amt) ||
            amt <= 0m)
        {
            MessageBox.Show("Enter a positive amount (USD).", "Salary advance", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        amt = Math.Round(amt, 2);

        using var db = new AppDbContext();
        if (FinancialTransactionService.HasMonthlySalaryPayment(db, SelectedAdvanceEmployee.Id, SelectedPayrollYear, SelectedPayrollMonth))
        {
            MessageBox.Show(
                "Payroll is already confirmed for this employee for the selected month. Advances are not allowed for that period.",
                "Salary advance",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var emp = db.Employees.AsNoTracking().SingleOrDefault(e => e.Id == SelectedAdvanceEmployee.Id);
        if (emp is null)
        {
            MessageBox.Show("Employee not found.", "Salary advance", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var (_, workdays, scheduledGrossUsd) =
            PayrollCalculator.GetHourlyGrossForPayrollMonth(emp, SelectedPayrollYear, SelectedPayrollMonth);

        if (emp.HourlyRate <= 0m || workdays == 0)
        {
            MessageBox.Show(
                "Advances are limited to 30% of scheduled gross pay for the month. This employee has no scheduled gross for the selected period — set a positive hourly rate (USD) and at least one scheduled workday (not Off) in Employees, then try again.",
                "Salary advance",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var advanceCapUsd = Math.Round(PayrollCalculator.MaxAdvanceFractionOfScheduledGross * scheduledGrossUsd, 2);
        var existingPendingUsd = PayrollSupport.SumPendingAdvancesForPayrollMonth(
            db,
            SelectedAdvanceEmployee.Id,
            SelectedPayrollYear,
            SelectedPayrollMonth);
        var remainingUsd = Math.Round(advanceCapUsd - existingPendingUsd, 2);
        if (remainingUsd < 0m)
            remainingUsd = 0m;

        if (amt > remainingUsd)
        {
            MessageBox.Show(
                $"Each employee’s advances for this payroll month cannot exceed 30% of scheduled gross pay (hourly wage × scheduled shift hours on working days).{Environment.NewLine}{Environment.NewLine}" +
                $"Scheduled gross for {PayrollPeriodLabel}: ${scheduledGrossUsd:N2}{Environment.NewLine}" +
                $"30% cap: ${advanceCapUsd:N2}{Environment.NewLine}" +
                $"Pending advances already recorded for this month: ${existingPendingUsd:N2}{Environment.NewLine}" +
                $"You can add at most ${remainingUsd:N2} now (requested ${amt:N2}).",
                "Salary advance",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var empName = emp.Name;

        var adv = new SalaryAdvance
        {
            EmployeeId = SelectedAdvanceEmployee.Id,
            AmountUsd = amt,
            GivenAt = DateTime.Now,
            ForPayrollYear = SelectedPayrollYear,
            ForPayrollMonth = SelectedPayrollMonth,
            Note = AdvanceNoteText.Trim()
        };
        db.SalaryAdvances.Add(adv);
        db.SaveChanges();

        FinancialTransactionService.RecordSalaryAdvanceExpense(db, adv.Id, adv.EmployeeId, empName, amt);
        db.SaveChanges();

        AdvanceAmountText = string.Empty;
        AdvanceNoteText = string.Empty;
        MessageBox.Show(
            $"Advance recorded for payroll {PayrollPeriodLabel} and posted to Money. It is deducted when you confirm that month for this employee.",
            "Salary advance",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        ReloadRows();
    }
}
