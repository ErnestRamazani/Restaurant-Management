using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Services;

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
    public string EmployeeInitials { get; init; } = "?";
    public string UniqueId { get; init; } = string.Empty;
    /// <summary>True when <see cref="ContractMonthlyUsd"/> drives payroll gross for the month.</summary>
    public bool UsesMonthlySalary { get; init; }
    /// <summary>Employee contract monthly amount (not prorated).</summary>
    public decimal ContractMonthlyUsd { get; init; }
    public decimal ScheduledHoursMonth { get; init; }
    public int ScheduledWorkdays { get; init; }
    /// <summary>Gross payroll base stored or computed for the month (prorated monthly salary, or legacy hourly path until migrated).</summary>
    public decimal BaseGrossUsd { get; init; }
    public ImageSource? HeaderProfileImage { get; init; }
    public bool HeaderHasProfilePhoto => HeaderProfileImage is not null;

    public string RateColumnText => UsesMonthlySalary
        ? $"{ContractMonthlyUsd:N2}/mo"
        : "Set monthly";

    /// <summary>Primary rate line for the payroll card chips (monthly-only UX; legacy hourly is not shown).</summary>
    public string PayrollPrimaryRateChipText => UsesMonthlySalary
        ? $"Monthly ${ContractMonthlyUsd:N2}/mo"
        : "Set monthly salary (Employees)";
    public int AbsenceDays { get; init; }
    public int LateDays { get; init; }
    public int LatePenaltyAbsences { get; init; }
    public int TotalDeductionUnits { get; init; }
    public decimal BaseAfterAttendanceUsd { get; init; }
    public decimal MoneyGeneratedUsd { get; init; }
    public decimal BonusFivePercentUsd { get; init; }
    public decimal AdvancesDeductUsd { get; init; }
    /// <summary>Remaining net pay still owed for this payroll month (installments reduce this).</summary>
    public decimal NetPay { get; init; }
    /// <summary>Full net pay for the month from the payroll snapshot (or live calc when no snapshot yet).</summary>
    public decimal TotalNetUsd { get; init; }
    /// <summary>Cumulative cash posted toward this month’s net (from <see cref="PayrollPaymentRecord.PaidToDateUsd"/>).</summary>
    public decimal PaidToDateUsd { get; init; }
    public bool HasPayrollRecord { get; init; }
    public bool IsPartiallyPaid { get; init; }
    public bool AlreadyPaid { get; init; }
    public string StatusText { get; init; } = string.Empty;

    public string PayrollActionLabel { get; init; } = "Confirm payroll";
    public string PayrollActionLabelShort { get; init; } = "Pay";
    public string HeaderMoneyChipText { get; init; } = string.Empty;
    public string NetPaySectionTitle { get; init; } = "Net pay";

    /// <summary>True when a stored payroll payment record exists (amount and paid date available for display).</summary>
    public bool HasPaidReceiptDetail { get; init; }

    /// <summary>Formatted for inline display, e.g. <c>$1,322.65 USD</c>.</summary>
    public string PaidAmountDisplay { get; init; } = string.Empty;

    public string PaidDateDisplay { get; init; } = string.Empty;

    /// <summary>Shows generic <see cref="StatusText"/> line when there is no receipt breakdown.</summary>
    public bool ShowPlainStatusLine => !HasPaidReceiptDetail;

    public bool CanConfirmPayroll =>
        !AlreadyPaid
        && NetPay > 0.005m
        && (HasPayrollRecord || BaseGrossUsd > 0.005m);
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
    private readonly AdminDataApiClient _data = new();
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
    private bool _isPayrollPaymentDialogOpen;
    private string _payrollPaymentDialogEmployee = string.Empty;
    private string _payrollPaymentAmountText = string.Empty;
    private string _payrollPaymentRemainingHint = string.Empty;
    private SalaryEmployeeRowVm? _payrollPaymentDialogRow;

    private bool _isShiftHistoryOpen;
    private string _shiftHistoryTitle = "Shift history";
    private string _shiftHistorySubtitle = string.Empty;
    private string _shiftHistoryBanner = string.Empty;

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
            _ = ReloadRowsAsync();
        }
    }

    public int SelectedPayrollYear
    {
        get => _payrollYear;
        set
        {
            if (!SetField(ref _payrollYear, value))
                return;
            _ = ReloadRowsAsync();
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
    public ICommand SubmitPayrollPaymentDialogCommand { get; }
    public ICommand CancelPayrollPaymentDialogCommand { get; }
    public ICommand ShowShiftHistoryCommand { get; }
    public ICommand CloseShiftHistoryCommand { get; }

    public bool IsShiftHistoryOpen
    {
        get => _isShiftHistoryOpen;
        set => SetField(ref _isShiftHistoryOpen, value);
    }

    public string ShiftHistoryTitle
    {
        get => _shiftHistoryTitle;
        set => SetField(ref _shiftHistoryTitle, value);
    }

    public string ShiftHistorySubtitle
    {
        get => _shiftHistorySubtitle;
        set => SetField(ref _shiftHistorySubtitle, value);
    }

    public string ShiftHistoryBanner
    {
        get => _shiftHistoryBanner;
        set
        {
            if (!SetField(ref _shiftHistoryBanner, value))
                return;
            OnPropertyChanged(nameof(ShiftHistoryBannerVisible));
        }
    }

    public bool ShiftHistoryBannerVisible => !string.IsNullOrWhiteSpace(ShiftHistoryBanner);

    public ObservableCollection<ShiftHistoryRowViewModel> ShiftHistoryRows { get; } = [];

    public SalaryViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        var y = DateTime.Today.Year;
        for (var i = y - 2; i <= y + 1; i++)
            YearChoices.Add(i);

        var prev = DateTime.Today.AddMonths(-1);
        _payrollYear = prev.Year;
        _payrollMonth = prev.Month;

        RefreshPayrollCommand = new RelayCommand(_ => _ = ReloadRowsAsync());
        ConfirmAllPayrollCommand = new RelayCommand(_ => ConfirmAllPayments());
        ConfirmSinglePayrollCommand = new RelayCommand(
            p => ConfirmSinglePayment(p as SalaryEmployeeRowVm),
            p => p is SalaryEmployeeRowVm r && r.CanConfirmPayroll);
        RecordSalaryAdvanceCommand = new RelayCommand(_ => _ = RecordSalaryAdvanceAsync());
        SubmitPayrollPaymentDialogCommand = new RelayCommand(_ => _ = SubmitPayrollPaymentDialogAsync());
        CancelPayrollPaymentDialogCommand = new RelayCommand(_ => ClosePayrollPaymentDialog());
        ShowShiftHistoryCommand = new RelayCommand(p => _ = ShowShiftHistoryAsync(p as SalaryEmployeeRowVm));
        CloseShiftHistoryCommand = new RelayCommand(_ => CloseShiftHistory());
        _ = ReloadRowsAsync();
    }

    public bool IsPayrollPaymentDialogOpen
    {
        get => _isPayrollPaymentDialogOpen;
        private set
        {
            if (!SetField(ref _isPayrollPaymentDialogOpen, value))
                return;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string PayrollPaymentDialogEmployee
    {
        get => _payrollPaymentDialogEmployee;
        private set => SetField(ref _payrollPaymentDialogEmployee, value);
    }

    public string PayrollPaymentAmountText
    {
        get => _payrollPaymentAmountText;
        set => SetField(ref _payrollPaymentAmountText, value);
    }

    public string PayrollPaymentRemainingHint
    {
        get => _payrollPaymentRemainingHint;
        private set => SetField(ref _payrollPaymentRemainingHint, value);
    }

    private void CloseShiftHistory()
    {
        IsShiftHistoryOpen = false;
        ShiftHistoryBanner = string.Empty;
        ShiftHistoryRows.Clear();
    }

    private async Task ShowShiftHistoryAsync(SalaryEmployeeRowVm? row)
    {
        if (row is null)
            return;

        ShiftHistoryTitle = $"Shift history — {row.EmployeeName}";
        ShiftHistoryBanner = "Loading…";
        ShiftHistorySubtitle = string.Empty;
        ShiftHistoryRows.Clear();
        IsShiftHistoryOpen = true;

        try
        {
            var employees = (await _data.GetEmployeesAsync().ConfigureAwait(true)).ToList();
            var employee = employees.FirstOrDefault(e => e.Id == row.EmployeeId);
            if (employee is null)
            {
                ShiftHistoryRows.Clear();
                ShiftHistorySubtitle = string.Empty;
                ShiftHistoryBanner = "Employee not found.";
                return;
            }

            var attendanceRows = await _data.GetAttendanceAsync().ConfigureAwait(true);
            var att = SettingsManager.Load().Attendance ?? new AttendanceSettings();
            var shiftSchedule = AttendanceShiftSchedule.FromSettings(att);

            var history = attendanceRows
                .Where(a => a.EmployeeId == employee.Id)
                .OrderByDescending(a => a.WorkDate)
                .ToList();

            ShiftHistoryRows.Clear();
            foreach (var a in history)
            {
                var localDay = a.WorkDate.Date;
                var shiftDefinition = AttendanceScheduleHelper.ResolveShiftWindow(employee, localDay, shiftSchedule);
                var isAbsence = a.IsAbsence;
                var status = string.IsNullOrWhiteSpace(a.ClockInStatus) ? "Pending" : a.ClockInStatus;
                if (shiftDefinition.IsOff && a.ClockInTime is null)
                    status = "Off Shift";
                else if (isAbsence)
                    status = "Absent";

                var lateJust = (a.Justification ?? string.Empty).Trim();
                var absenceNote = (a.AbsenceJustification ?? string.Empty).Trim();

                ShiftHistoryRows.Add(new ShiftHistoryRowViewModel
                {
                    WorkDateDisplay = a.WorkDate.ToString("ddd yyyy-MM-dd", CultureInfo.CurrentCulture),
                    ShiftType = shiftDefinition.Name,
                    ClockIn = a.ClockInTime?.ToString("HH:mm", CultureInfo.CurrentCulture) ?? "—",
                    ClockOut = a.ClockOutTime?.ToString("HH:mm", CultureInfo.CurrentCulture) ?? "—",
                    Status = status,
                    Justification = string.IsNullOrEmpty(lateJust) ? "—" : lateJust,
                    Notes = string.IsNullOrEmpty(absenceNote) ? "—" : absenceNote
                });
            }

            ShiftHistorySubtitle = history.Count == 1 ? "1 row" : $"{history.Count} rows";
            ShiftHistoryBanner = history.Count == 0
                ? "No attendance rows stored for this employee yet."
                : string.Empty;
        }
        catch (Exception ex)
        {
            ShiftHistoryRows.Clear();
            ShiftHistorySubtitle = string.Empty;
            ShiftHistoryBanner = ex.GetBaseException().Message;
        }
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

    private async Task ReloadRowsAsync()
    {
        _allPayrollRows.Clear();
        Rows.Clear();
        AdvanceEmployees.Clear();

        var start = new DateTime(SelectedPayrollYear, SelectedPayrollMonth, 1).Date;
        var endExclusive = start.AddMonths(1);
        var monthStartUtc = AttendanceCalendar.DayAnchorUtc(start);
        var monthEndExclusiveUtc = AttendanceCalendar.DayAnchorUtc(endExclusive);
        var monthEnd = new DateTime(
            SelectedPayrollYear,
            SelectedPayrollMonth,
            DateTime.DaysInMonth(SelectedPayrollYear, SelectedPayrollMonth)).Date;

        try
        {
            var payrollRules = PayrollCalculator.ResolveSalaryPayrollRulesForLocalFile();
            var employeesTask = _data.GetEmployeesAsync();
            var attendanceTask = _data.GetAttendanceAsync();
            var payrollTask = _data.GetPayrollAsync();
            var ordersTask = _data.GetOrdersAsync();
            var productsTask = _data.GetProductsAsync();
            var advancesTask = _data.GetSalaryAdvancesAsync();
            var moneyTask = _data.GetMoneyTransactionsAsync();
            await Task.WhenAll(employeesTask, attendanceTask, payrollTask, ordersTask, productsTask, advancesTask, moneyTask).ConfigureAwait(true);

            var employees = (await employeesTask.ConfigureAwait(true))
                .Where(e => e.EmploymentStatus == "Active")
                .OrderBy(e => e.Name)
                .ToList();
            var attendanceAll = (await attendanceTask.ConfigureAwait(true)).ToList();
            var payrollList = (await payrollTask.ConfigureAwait(true)).ToList();
            var orders = (await ordersTask.ConfigureAwait(true)).ToList();
            var productPriceById = (await productsTask.ConfigureAwait(true)).ToDictionary(p => p.Id, p => p.Price);
            var advances = (await advancesTask.ConfigureAwait(true)).ToList();
            var transactions = (await moneyTask.ConfigureAwait(true)).ToList();

            foreach (var e in employees)
            {
                if (FinancialTransactionService.HasMonthlySalaryPayment(payrollList, transactions, e.Id, SelectedPayrollYear, SelectedPayrollMonth))
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

            var attendancesByEmployee = attendanceAll
                .Where(a => a.WorkDate >= monthStartUtc && a.WorkDate < monthEndExclusiveUtc)
                .GroupBy(a => a.EmployeeId)
                .ToDictionary(g => g.Key, g => (IEnumerable<EmployeeAttendance>)g);

            var payrollRecords = payrollList
                .Where(p => p.Year == SelectedPayrollYear && p.Month == SelectedPayrollMonth)
                .ToDictionary(p => p.EmployeeId);

            foreach (var emp in employees)
            {
                attendancesByEmployee.TryGetValue(emp.Id, out var attRows);
                var rows = attRows ?? Enumerable.Empty<EmployeeAttendance>();
                var (absLive, lateLive, penLive, totalLive) =
                    PayrollCalculator.CountAttendanceUnitsForPayroll(emp, SelectedPayrollYear, SelectedPayrollMonth, rows, payrollRules);

                var monthBase = PayrollCalculator.ResolvePayrollMonthBase(emp, SelectedPayrollYear, SelectedPayrollMonth);
                var schedHours = monthBase.ScheduledHours;
                var workdays = monthBase.ScheduledWorkdays;
                var grossLive = monthBase.GrossPayUsd;

                var moneyLive = PayrollSupport.SumServerCompletedOrderMerchandiseUsd(orders, productPriceById, emp.Id, start, endExclusive);
                var bonusLive = PayrollCalculator.ComputeBonusUsd(moneyLive, payrollRules);
                var advancesPending = PayrollSupport.SumPendingAdvancesForPayrollMonth(
                    advances,
                    emp.Id,
                    SelectedPayrollYear,
                    SelectedPayrollMonth);

                payrollRecords.TryGetValue(emp.Id, out var payRec);
                var fullyPaid = FinancialTransactionService.IsPayrollFullyPaid(payrollList, transactions, emp.Id, SelectedPayrollYear, SelectedPayrollMonth);

            decimal baseGrossUsd;
            int abs;
            int late;
            int pen;
            int total;
            decimal money;
            decimal bonus;
            decimal advancesApplied;
            decimal baseAfter;
            decimal totalNet;
            decimal paidToDate;
            var hasPayrollRecord = payRec is not null;

            if (payRec is not null)
            {
                baseGrossUsd = payRec.MonthlySalaryUsd;
                abs = payRec.AbsenceDays;
                late = payRec.LateDays;
                pen = payRec.LatePenaltyUnits;
                total = payRec.TotalDeductionUnits;
                money = payRec.MoneyGeneratedUsd;
                bonus = payRec.BonusFivePercentUsd;
                advancesApplied = payRec.AdvancesDeductedUsd;
                baseAfter = PayrollCalculator.ComputeBaseAfterAttendanceUsd(baseGrossUsd, monthBase.AttendanceDenominatorWorkdays, total);
                totalNet = payRec.NetPayUsd;
                paidToDate = payRec.PaidToDateUsd;
            }
            else
            {
                baseGrossUsd = grossLive;
                abs = absLive;
                late = lateLive;
                pen = penLive;
                total = totalLive;
                money = moneyLive;
                bonus = bonusLive;
                advancesApplied = advancesPending;
                baseAfter = PayrollCalculator.ComputeBaseAfterAttendanceUsd(grossLive, monthBase.AttendanceDenominatorWorkdays, totalLive);
                totalNet = PayrollCalculator.ComputeFinalNetPayUsd(
                    grossLive,
                    monthBase.AttendanceDenominatorWorkdays,
                    totalLive,
                    moneyLive,
                    advancesPending,
                    payrollRules);
                paidToDate = 0m;
            }

            var remaining = hasPayrollRecord
                ? Math.Max(0m, Math.Round(totalNet - paidToDate, 2))
                : fullyPaid ? 0m : totalNet;

            var advancesDisplay = fullyPaid ? 0m : advancesApplied;
            var netDisplay = fullyPaid ? 0m : remaining;

            var isPartiallyPaid = hasPayrollRecord && !fullyPaid && paidToDate > 0.005m;

            var payrollActionLabel = fullyPaid ? "Confirmed" : isPartiallyPaid ? "Add payment" : "Confirm payroll";
            var payrollActionLabelShort = fullyPaid ? "Done" : isPartiallyPaid ? "Add" : "Pay";

            string headerMoneyChipText;
            if (fullyPaid)
                headerMoneyChipText = "Paid in full";
            else if (isPartiallyPaid)
                headerMoneyChipText = $"Still owed ${remaining:N2} of ${totalNet:N2}";
            else
                headerMoneyChipText = $"Net ${remaining:N2}";

            var netPaySectionTitle = fullyPaid ? "Net pay" : isPartiallyPaid ? "Still to pay" : "Net pay";

            var lastDay = monthEnd;
            var daysLate = DateTime.Today > lastDay ? Math.Max(0, (DateTime.Today - lastDay).Days) : 0;

            var hasReceipt = payRec is not null;
            string paidAmountDisplay = string.Empty;
            string paidDateDisplay = string.Empty;
            if (payRec is not null)
            {
                var localPaid = payRec.PaidAtUtc.ToLocalTime();
                paidDateDisplay = localPaid.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);
                paidAmountDisplay = remaining <= 0.005m
                    ? $"${payRec.NetPayUsd:N2} USD in full"
                    : $"${payRec.PaidToDateUsd:N2} of ${payRec.NetPayUsd:N2} USD";
            }

            string status;
            if (fullyPaid && payRec is not null)
            {
                var localPaid = payRec.PaidAtUtc.ToLocalTime();
                status = $"Paid in full (${payRec.NetPayUsd:N2} USD). Last posting {localPaid:MMM d, yyyy}.";
            }
            else if (fullyPaid)
            {
                status = "Paid";
            }
            else if (payRec is not null)
            {
                var localPaid = payRec.PaidAtUtc.ToLocalTime();
                status =
                    $"Partially paid ${payRec.PaidToDateUsd:N2} of ${payRec.NetPayUsd:N2} USD — still owe ${remaining:N2}. Last payment {localPaid:MMM d, yyyy}.";
            }
            else if (emp.MonthlySalaryUSD <= 0m && emp.HourlyRate <= 0m)
            {
                status = "Set monthly salary (USD) in Employees — required for payroll";
            }
            else if (monthBase.GrossPayUsd <= 0m)
            {
                status = monthBase.UsesMonthlySalary
                    ? "No payroll gross this month — check join date or monthly salary in Employees"
                    : "No payroll gross this month — set a positive monthly salary in Employees (or check join date and schedule)";
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
                EmployeeInitials = emp.Initials,
                UniqueId = emp.UniqueId,
                UsesMonthlySalary = monthBase.UsesMonthlySalary,
                ContractMonthlyUsd = monthBase.ContractMonthlySalaryUsd,
                HeaderProfileImage = TryLoadBitmapFromFilePath(emp.ProfileImagePath),
                ScheduledHoursMonth = schedHours,
                ScheduledWorkdays = workdays,
                BaseGrossUsd = baseGrossUsd,
                AbsenceDays = abs,
                LateDays = late,
                LatePenaltyAbsences = pen,
                TotalDeductionUnits = total,
                BaseAfterAttendanceUsd = baseAfter,
                MoneyGeneratedUsd = money,
                BonusFivePercentUsd = bonus,
                AdvancesDeductUsd = advancesDisplay,
                NetPay = netDisplay,
                TotalNetUsd = totalNet,
                PaidToDateUsd = payRec?.PaidToDateUsd ?? 0m,
                HasPayrollRecord = hasPayrollRecord,
                IsPartiallyPaid = isPartiallyPaid,
                AlreadyPaid = fullyPaid,
                StatusText = status,
                HasPaidReceiptDetail = hasReceipt,
                PaidAmountDisplay = paidAmountDisplay,
                PaidDateDisplay = paidDateDisplay,
                PayrollActionLabel = payrollActionLabel,
                PayrollActionLabelShort = payrollActionLabelShort,
                HeaderMoneyChipText = headerMoneyChipText,
                NetPaySectionTitle = netPaySectionTitle
            });
        }

            ApplyEmployeeFilter();

            var anyUnpaid = _allPayrollRows.Any(r => !r.AlreadyPaid && r.NetPay > 0.005m);
            var pastMonthEnd = DateTime.Today > monthEnd;
            ShowPayrollOverdueWarning = anyUnpaid && pastMonthEnd;
            DaysPastPayDay = ShowPayrollOverdueWarning ? Math.Max(0, (DateTime.Today - monthEnd).Days) : 0;
            PayrollOverdueWarningText = ShowPayrollOverdueWarning
                ? $"Payroll for {PayrollPeriodLabel} is overdue. You are {DaysPastPayDay} day(s) past the pay date (last day of the month). Confirm payments below."
                : string.Empty;

            OnPropertyChanged(nameof(PayrollPeriodLabel));
            CommandManager.InvalidateRequerySuggested();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                "Salary load failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ConfirmAllPayments()
    {
        var pending = _allPayrollRows.Where(r => r.CanConfirmPayroll).ToList();
        if (pending.Count == 0)
        {
            MessageBox.Show(
                "No payroll payments to record for this month. Each employee needs a positive monthly salary (or an existing partial payroll row with a balance due).",
                "Salary",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Record payroll for all {pending.Count} employee(s) with a balance due for {PayrollPeriodLabel}? Each person’s payment will use their remaining net amount. You can still enter a custom amount when paying one employee at a time.",
            "Confirm all payroll",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        _ = RunPayrollForRowsAsync(pending, r => r.NetPay);
    }

    private void ConfirmSinglePayment(SalaryEmployeeRowVm? row)
    {
        if (row is null || !row.CanConfirmPayroll)
            return;

        OpenPayrollPaymentDialog(row);
    }

    private void OpenPayrollPaymentDialog(SalaryEmployeeRowVm row)
    {
        _payrollPaymentDialogRow = row;
        PayrollPaymentDialogEmployee = row.EmployeeName;
        PayrollPaymentAmountText = row.NetPay > 0.005m
            ? row.NetPay.ToString("0.00", CultureInfo.InvariantCulture)
            : string.Empty;
        PayrollPaymentRemainingHint = row.HasPayrollRecord
            ? $"Net this month: ${row.TotalNetUsd:N2} — paid so far: ${row.PaidToDateUsd:N2} — remaining: ${row.NetPay:N2}"
            : $"Net pay due: ${row.NetPay:N2}";
        IsPayrollPaymentDialogOpen = true;
    }

    private void ClosePayrollPaymentDialog()
    {
        IsPayrollPaymentDialogOpen = false;
        _payrollPaymentDialogRow = null;
        PayrollPaymentAmountText = string.Empty;
        PayrollPaymentRemainingHint = string.Empty;
        PayrollPaymentDialogEmployee = string.Empty;
    }

    private async Task SubmitPayrollPaymentDialogAsync()
    {
        var row = _payrollPaymentDialogRow;
        if (row is null)
            return;

        if (!decimal.TryParse(
                PayrollPaymentAmountText.Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amt) ||
            amt <= 0m)
        {
            MessageBox.Show(
                "Enter a positive payment amount in USD.",
                "Salary",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        amt = Math.Round(amt, 2);

        try
        {
            var employees = (await _data.GetEmployeesAsync().ConfigureAwait(true)).ToList();
            var employee = employees.FirstOrDefault(e => e.Id == row.EmployeeId);
            if (employee is null)
            {
                MessageBox.Show("Employee not found.", "Salary", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var start = new DateTime(SelectedPayrollYear, SelectedPayrollMonth, 1).Date;
            var endExclusive = start.AddMonths(1);
            var monthStartUtc = AttendanceCalendar.DayAnchorUtc(start);
            var monthEndExclusiveUtc = AttendanceCalendar.DayAnchorUtc(endExclusive);

            var attendance = (await _data.GetAttendanceAsync().ConfigureAwait(true))
                .Where(a => a.EmployeeId == row.EmployeeId && a.WorkDate >= monthStartUtc && a.WorkDate < monthEndExclusiveUtc)
                .ToList();
            var orders = (await _data.GetOrdersAsync().ConfigureAwait(true)).ToList();
            var productPriceById = (await _data.GetProductsAsync().ConfigureAwait(true)).ToDictionary(p => p.Id, p => p.Price);
            var moneyGenerated = PayrollSupport.SumServerCompletedOrderMerchandiseUsd(
                orders,
                productPriceById,
                row.EmployeeId,
                start,
                endExclusive);

            var advances = (await _data.GetSalaryAdvancesAsync().ConfigureAwait(true)).ToList();
            var transactions = (await _data.GetMoneyTransactionsAsync().ConfigureAwait(true)).ToList();
            var payrollList = (await _data.GetPayrollAsync().ConfigureAwait(true)).ToList();
            var existing = payrollList.FirstOrDefault(p =>
                p.EmployeeId == row.EmployeeId && p.Year == SelectedPayrollYear && p.Month == SelectedPayrollMonth);

            var err = FinancialTransactionService.TryRecordMonthlySalaryPaymentMemory(
                employee,
                attendance,
                moneyGenerated,
                advances,
                existing,
                amt,
                SelectedPayrollYear,
                SelectedPayrollMonth,
                transactions,
                out var upserts);
            if (err is not null)
            {
                MessageBox.Show(err, "Salary", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DesktopCloudPersistence.PushBatchBlocking(DesktopCloudPersistence.ToUpsertOperations(upserts));
            ClosePayrollPaymentDialog();
            MessageBox.Show(
                "Payroll payment saved. Money shows a Salary expense for the amount you entered. Run Refresh if amounts look stale.",
                "Salary",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            await ReloadRowsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                "Salary",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task RunPayrollForRowsAsync(IReadOnlyList<SalaryEmployeeRowVm> rows, Func<SalaryEmployeeRowVm, decimal> amountSelector)
    {
        try
        {
            foreach (var row in rows)
            {
                var amt = Math.Round(amountSelector(row), 2);
                var employees = (await _data.GetEmployeesAsync().ConfigureAwait(true)).ToList();
                var employee = employees.FirstOrDefault(e => e.Id == row.EmployeeId);
                if (employee is null)
                {
                    MessageBox.Show($"{row.EmployeeName}: Employee not found.", "Salary", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var start = new DateTime(SelectedPayrollYear, SelectedPayrollMonth, 1).Date;
                var endExclusive = start.AddMonths(1);
                var monthStartUtc = AttendanceCalendar.DayAnchorUtc(start);
                var monthEndExclusiveUtc = AttendanceCalendar.DayAnchorUtc(endExclusive);

                var attendance = (await _data.GetAttendanceAsync().ConfigureAwait(true))
                    .Where(a => a.EmployeeId == row.EmployeeId && a.WorkDate >= monthStartUtc && a.WorkDate < monthEndExclusiveUtc)
                    .ToList();
                var orders = (await _data.GetOrdersAsync().ConfigureAwait(true)).ToList();
                var productPriceById = (await _data.GetProductsAsync().ConfigureAwait(true)).ToDictionary(p => p.Id, p => p.Price);
                var moneyGenerated = PayrollSupport.SumServerCompletedOrderMerchandiseUsd(
                    orders,
                    productPriceById,
                    row.EmployeeId,
                    start,
                    endExclusive);

                var advances = (await _data.GetSalaryAdvancesAsync().ConfigureAwait(true)).ToList();
                var transactions = (await _data.GetMoneyTransactionsAsync().ConfigureAwait(true)).ToList();
                var payrollList = (await _data.GetPayrollAsync().ConfigureAwait(true)).ToList();
                var existing = payrollList.FirstOrDefault(p =>
                    p.EmployeeId == row.EmployeeId && p.Year == SelectedPayrollYear && p.Month == SelectedPayrollMonth);

                var err = FinancialTransactionService.TryRecordMonthlySalaryPaymentMemory(
                    employee,
                    attendance,
                    moneyGenerated,
                    advances,
                    existing,
                    amt,
                    SelectedPayrollYear,
                    SelectedPayrollMonth,
                    transactions,
                    out var upserts);
                if (err is not null)
                {
                    MessageBox.Show(
                        $"{row.EmployeeName}: {err}",
                        "Salary",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                DesktopCloudPersistence.PushBatchBlocking(DesktopCloudPersistence.ToUpsertOperations(upserts));
            }

            MessageBox.Show(
                "Payroll saved. Daily and Employees reports include Money salary lines for each payment. Employee timeline shows advances and payments.",
                "Salary",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            await ReloadRowsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                "Salary",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task RecordSalaryAdvanceAsync()
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

        try
        {
            var payrollList = (await _data.GetPayrollAsync().ConfigureAwait(true)).ToList();
            var transactions = (await _data.GetMoneyTransactionsAsync().ConfigureAwait(true)).ToList();
            if (FinancialTransactionService.HasMonthlySalaryPayment(
                    payrollList,
                    transactions,
                    SelectedAdvanceEmployee.Id,
                    SelectedPayrollYear,
                    SelectedPayrollMonth))
            {
                MessageBox.Show(
                    "Payroll is already confirmed for this employee for the selected month. Advances are not allowed for that period.",
                    "Salary advance",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var emp = (await _data.GetEmployeesAsync().ConfigureAwait(true)).FirstOrDefault(e => e.Id == SelectedAdvanceEmployee.Id);
            if (emp is null)
            {
                MessageBox.Show("Employee not found.", "Salary advance", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var monthBase = PayrollCalculator.ResolvePayrollMonthBase(emp, SelectedPayrollYear, SelectedPayrollMonth);
            var scheduledGrossUsd = monthBase.GrossPayUsd;
            var payrollRules = PayrollCalculator.ResolveSalaryPayrollRulesForLocalFile();

            if ((emp.MonthlySalaryUSD <= 0m && emp.HourlyRate <= 0m) || scheduledGrossUsd <= 0m)
            {
                MessageBox.Show(
                    $"Advances are limited to {payrollRules.MaxSalaryAdvancePercentOfGross:0.##}% of payroll gross for the month. This employee has no payroll base for the selected period — set a positive monthly salary (USD) in Employees, then try again.",
                    "Salary advance",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var advances = (await _data.GetSalaryAdvancesAsync().ConfigureAwait(true)).ToList();
            var advanceCapUsd = Math.Round(PayrollCalculator.MaxAdvanceFractionOfScheduledGross(payrollRules) * scheduledGrossUsd, 2);
            var existingPendingUsd = PayrollSupport.SumPendingAdvancesForPayrollMonth(
                advances,
                SelectedAdvanceEmployee.Id,
                SelectedPayrollYear,
                SelectedPayrollMonth);
            var remainingUsd = Math.Round(advanceCapUsd - existingPendingUsd, 2);
            if (remainingUsd < 0m)
                remainingUsd = 0m;

            if (amt > remainingUsd)
            {
                MessageBox.Show(
                    $"Each employee’s advances for this payroll month cannot exceed {payrollRules.MaxSalaryAdvancePercentOfGross:0.##}% of that month’s payroll gross (prorated monthly salary).{Environment.NewLine}{Environment.NewLine}" +
                    $"Payroll gross for {PayrollPeriodLabel}: ${scheduledGrossUsd:N2}{Environment.NewLine}" +
                    $"{payrollRules.MaxSalaryAdvancePercentOfGross:0.##}% cap: ${advanceCapUsd:N2}{Environment.NewLine}" +
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

            DesktopCloudPersistence.PushUpsertBlocking(adv);

            var refreshedAdvances = (await _data.GetSalaryAdvancesAsync().ConfigureAwait(true)).ToList();
            var created = refreshedAdvances
                .Where(a =>
                    a.EmployeeId == SelectedAdvanceEmployee.Id &&
                    a.ForPayrollYear == SelectedPayrollYear &&
                    a.ForPayrollMonth == SelectedPayrollMonth &&
                    Math.Abs(a.AmountUsd - amt) < 0.001m)
                .OrderByDescending(a => a.Id)
                .FirstOrDefault();

            if (created is null)
            {
                MessageBox.Show(
                    "Advance was saved but could not be confirmed from the server. Refresh Salary and check Money.",
                    "Salary advance",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                _ = ReloadRowsAsync();
                return;
            }

            var refreshedTx = (await _data.GetMoneyTransactionsAsync().ConfigureAwait(true)).ToList();
            var expense = FinancialTransactionService.BuildSalaryAdvanceExpenseIfMissing(
                created.Id,
                created.EmployeeId,
                empName,
                amt,
                refreshedTx);
            if (expense is not null)
                DesktopCloudPersistence.PushUpsertBlocking(expense);

            AdvanceAmountText = string.Empty;
            AdvanceNoteText = string.Empty;
            MessageBox.Show(
                $"Advance recorded for payroll {PayrollPeriodLabel} and posted to Money. It is deducted when you confirm that month for this employee.",
                "Salary advance",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            _ = ReloadRowsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                "Salary advance",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
