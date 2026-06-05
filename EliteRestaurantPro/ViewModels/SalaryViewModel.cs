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
using EliteRestaurantPro.Localization;
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
    public DateTime MonthEndDate { get; init; }
    public int DaysLate { get; init; }
    public DateTime? PaidAtUtc { get; init; }
    public bool NeedsSalarySetup { get; init; }

    public string RateColumnText { get; set; } = string.Empty;
    public string PayrollPrimaryRateChipText { get; set; } = string.Empty;
    public string ScheduledHoursChipText { get; set; } = string.Empty;
    public string ScheduledWorkdaysChipText { get; set; } = string.Empty;
    public string BaseGrossChipText { get; set; } = string.Empty;
    public string AbsencesChipText { get; set; } = string.Empty;
    public string LatesChipText { get; set; } = string.Empty;
    public string LateUnitsChipText { get; set; } = string.Empty;
    public string TotalUnitsChipText { get; set; } = string.Empty;
    public string AfterAttendanceChipText { get; set; } = string.Empty;
    public string SalesServedChipText { get; set; } = string.Empty;
    public string SalesBonusChipText { get; set; } = string.Empty;
    public string AdvancesDisplayText { get; set; } = string.Empty;
    public string NetPayDisplayText { get; set; } = string.Empty;
    public string TableBaseGrossText { get; set; } = string.Empty;
    public string TableAfterAttText { get; set; } = string.Empty;
    public string TableSalesText { get; set; } = string.Empty;
    public string TableBonusText { get; set; } = string.Empty;
    public string TableAdvancesText { get; set; } = string.Empty;
    public string TableNetPayText { get; set; } = string.Empty;
    public string PayStatusBadgeText { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public string PayrollActionLabel { get; set; } = string.Empty;
    public string PayrollActionLabelShort { get; set; } = string.Empty;
    public string HeaderMoneyChipText { get; set; } = string.Empty;
    public string NetPaySectionTitle { get; set; } = string.Empty;
    public string PaidPrefix { get; set; } = string.Empty;
    public string PaidOnSeparator { get; set; } = string.Empty;
    public string PaidAmountDisplay { get; set; } = string.Empty;
    public string PaidDateDisplay { get; set; } = string.Empty;

    /// <summary>True when a stored payroll payment record exists (amount and paid date available for display).</summary>
    public bool HasPaidReceiptDetail { get; init; }

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
    private string _shiftHistoryTitle = string.Empty;
    private string _shiftHistorySubtitle = string.Empty;
    private string _shiftHistoryBanner = string.Empty;

    public override string ActivePage => "Salary";

    public string PageTitlePrimary => Loc.Admin("salTitlePrimary", "Salary");
    public string PageTitleAccent => Loc.Admin("salTitleAccent", "& Payroll");
    public string SimpleTableLayoutLabel => Loc.Admin("salSimpleTableLayout", "Simple table layout");
    public string RefreshLabel => Loc.Admin("refresh", "Refresh");
    public string OverdueTitle => Loc.Admin("salOverdueTitle", "Payroll overdue");
    public string DaysPastMonthEndLabel => Loc.Admin("salDaysPastMonthEnd", "Days past month end: ");
    public string AdvanceSectionTitle => Loc.Admin("salAdvanceTitle", "Salary advance");
    public string EmployeeLabel => Loc.Admin("salEmployee", "Employee");
    public string AmountUsdLabel => Loc.Admin("salAmountUsd", "Amount (USD)");
    public string NoteLabel => Loc.Admin("salNote", "Note");
    public string RecordAdvanceLabel => Loc.Admin("salRecordAdvance", "Record advance");
    public string PayAllRemainingLabel => Loc.Admin("salPayAllRemaining", "Pay all remaining balances");
    public string PayPeriodLabel => Loc.Admin("salPayPeriod", "Pay period");
    public string YearLabel => Loc.Admin("salYear", "Year");
    public string MonthLabel => Loc.Admin("salMonth", "Month");
    public string ShowLabel => Loc.Admin("salShow", "Show:");
    public string FilterAllEmployeesLabel => Loc.Admin("salFilterAll", "All employees");
    public string FilterPaidOnlyLabel => Loc.Admin("salFilterPaidOnly", "Paid only");
    public string FilterUnpaidOnlyLabel => Loc.Admin("salFilterUnpaidOnly", "Not paid yet");
    public string ScheduleBaseSectionTitle => Loc.Admin("salScheduleBase", "Schedule & payroll base");
    public string AttendanceSectionTitle => Loc.Admin("salAttendance", "Attendance");
    public string ShiftHistoryLabel => Loc.Admin("salShiftHistory", "Shift history");
    public string SalesBonusSectionTitle => Loc.Admin("salSalesBonusSection", "Sales & bonus");
    public string AdvanceNetSectionTitle => Loc.Admin("salAdvanceNetSection", "Advance and Net Salary");
    public string AdvancesDeductedLabel => Loc.Admin("salAdvancesDeducted", "Advances (deducted)");
    public string PaymentDialogTitle => Loc.Admin("salPaymentDialogTitle", "Record payroll payment");
    public string AmountToPayLabel => Loc.Admin("salAmountToPay", "Amount to pay (USD)");
    public string CancelLabel => Loc.Admin("salCancel", "Cancel");
    public string RecordPaymentLabel => Loc.Admin("salRecordPayment", "Record payment");
    public string ColEmployeeLabel => Loc.Admin("salColEmployee", "Employee");
    public string ColIdLabel => Loc.Admin("salColId", "ID");
    public string ColMonthlyLabel => Loc.Admin("salColMonthly", "Monthly");
    public string ColHrsLabel => Loc.Admin("salColHrs", "Hrs");
    public string ColDaysLabel => Loc.Admin("salColDays", "Days");
    public string ColBaseLabel => Loc.Admin("salColBase", "Base");
    public string ColAbsLabel => Loc.Admin("salColAbs", "Abs");
    public string ColLateLabel => Loc.Admin("salColLate", "Late");
    public string ColLateUnitsLabel => Loc.Admin("salColLateUnits", "L→U");
    public string ColUnitsLabel => Loc.Admin("salColUnits", "Units");
    public string ColAfterAttLabel => Loc.Admin("salColAfterAtt", "After att.");
    public string ColSalesLabel => Loc.Admin("salColSales", "Sales");
    public string ColBonusLabel => Loc.Admin("salColBonus", "Bonus");
    public string ColAdvancesLabel => Loc.Admin("salColAdvances", "Advances");
    public string ColOwedLabel => Loc.Admin("salColOwed", "Owed");
    public string ColPayLabel => Loc.Admin("salColPay", "Pay");
    public string ColStatusLabel => Loc.Admin("salColStatus", "Status");
    public string MsgBoxTitle => Loc.Admin("salMsgTitle", "Salary");
    public string MsgBoxAdvanceTitle => Loc.Admin("salMsgAdvanceTitle", "Salary advance");

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

    public string PayrollPeriodLabel =>
        SalaryUiLocalizer.FormatPayrollMonth(SelectedPayrollYear, SelectedPayrollMonth);

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
        _shiftHistoryTitle = Loc.Admin("salShiftHistory", "Shift history");
        _ = ReloadRowsAsync();
    }

    protected override void RefreshLocalizedStrings()
    {
        base.RefreshLocalizedStrings();
        Notify(
            nameof(PageTitlePrimary),
            nameof(PageTitleAccent),
            nameof(SimpleTableLayoutLabel),
            nameof(RefreshLabel),
            nameof(OverdueTitle),
            nameof(DaysPastMonthEndLabel),
            nameof(AdvanceSectionTitle),
            nameof(EmployeeLabel),
            nameof(AmountUsdLabel),
            nameof(NoteLabel),
            nameof(RecordAdvanceLabel),
            nameof(PayAllRemainingLabel),
            nameof(PayPeriodLabel),
            nameof(YearLabel),
            nameof(MonthLabel),
            nameof(ShowLabel),
            nameof(FilterAllEmployeesLabel),
            nameof(FilterPaidOnlyLabel),
            nameof(FilterUnpaidOnlyLabel),
            nameof(ScheduleBaseSectionTitle),
            nameof(AttendanceSectionTitle),
            nameof(ShiftHistoryLabel),
            nameof(SalesBonusSectionTitle),
            nameof(AdvanceNetSectionTitle),
            nameof(AdvancesDeductedLabel),
            nameof(PaymentDialogTitle),
            nameof(AmountToPayLabel),
            nameof(CancelLabel),
            nameof(RecordPaymentLabel),
            nameof(ColEmployeeLabel),
            nameof(ColIdLabel),
            nameof(ColMonthlyLabel),
            nameof(ColHrsLabel),
            nameof(ColDaysLabel),
            nameof(ColBaseLabel),
            nameof(ColAbsLabel),
            nameof(ColLateLabel),
            nameof(ColLateUnitsLabel),
            nameof(ColUnitsLabel),
            nameof(ColAfterAttLabel),
            nameof(ColSalesLabel),
            nameof(ColBonusLabel),
            nameof(ColAdvancesLabel),
            nameof(ColOwedLabel),
            nameof(ColPayLabel),
            nameof(ColStatusLabel),
            nameof(MsgBoxTitle),
            nameof(MsgBoxAdvanceTitle),
            nameof(PayrollPeriodLabel));
        RelocalizePayrollRows();
        if (_payrollPaymentDialogRow is not null)
            PayrollPaymentRemainingHint = SalaryUiLocalizer.FormatPaymentRemainingHint(
                _payrollPaymentDialogRow.HasPayrollRecord,
                _payrollPaymentDialogRow.TotalNetUsd,
                _payrollPaymentDialogRow.PaidToDateUsd,
                _payrollPaymentDialogRow.NetPay);
    }

    private void RelocalizePayrollRows()
    {
        foreach (var row in _allPayrollRows)
            SalaryUiLocalizer.Apply(row);
        ApplyEmployeeFilter();
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

        ShiftHistoryTitle = AdminTextLocalizer.FormatShiftHistoryTitle(row.EmployeeName);
        ShiftHistoryBanner = AdminTextLocalizer.ShiftHistoryLoadingText;
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
                ShiftHistoryBanner = AdminTextLocalizer.ShiftHistoryEmployeeNotFoundText;
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
                    ShiftType = AdminTextLocalizer.TranslateShift(shiftDefinition.Name),
                    ClockIn = a.ClockInTime?.ToString("HH:mm", CultureInfo.CurrentCulture) ?? "—",
                    ClockOut = a.ClockOutTime?.ToString("HH:mm", CultureInfo.CurrentCulture) ?? "—",
                    Status = AdminTextLocalizer.TranslateShiftHistoryStatus(status),
                    Justification = string.IsNullOrEmpty(lateJust) ? "—" : lateJust,
                    Notes = string.IsNullOrEmpty(absenceNote) ? "—" : absenceNote
                });
            }

            ShiftHistorySubtitle = AdminTextLocalizer.FormatShiftHistoryRowCount(history.Count);
            ShiftHistoryBanner = history.Count == 0
                ? AdminTextLocalizer.ShiftHistoryEmptyText
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
            var daysLate = DateTime.Today > monthEnd ? Math.Max(0, (DateTime.Today - monthEnd).Days) : 0;
            var hasReceipt = payRec is not null;

            var rowVm = new SalaryEmployeeRowVm
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
                HasPaidReceiptDetail = hasReceipt,
                MonthEndDate = monthEnd,
                DaysLate = daysLate,
                PaidAtUtc = payRec?.PaidAtUtc,
                NeedsSalarySetup = emp.MonthlySalaryUSD <= 0m && emp.HourlyRate <= 0m
            };
            SalaryUiLocalizer.Apply(rowVm);
            _allPayrollRows.Add(rowVm);
        }

            ApplyEmployeeFilter();

            var anyUnpaid = _allPayrollRows.Any(r => !r.AlreadyPaid && r.NetPay > 0.005m);
            var pastMonthEnd = DateTime.Today > monthEnd;
            ShowPayrollOverdueWarning = anyUnpaid && pastMonthEnd;
            DaysPastPayDay = ShowPayrollOverdueWarning ? Math.Max(0, (DateTime.Today - monthEnd).Days) : 0;
            PayrollOverdueWarningText = ShowPayrollOverdueWarning
                ? SalaryUiLocalizer.FormatOverdueWarning(PayrollPeriodLabel, DaysPastPayDay)
                : string.Empty;

            OnPropertyChanged(nameof(PayrollPeriodLabel));
            CommandManager.InvalidateRequerySuggested();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                Loc.Admin("salMsgLoadFailedTitle", "Salary load failed"),
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
                Loc.Admin("salMsgNoPayments", "No payroll payments to record for this month. Each employee needs a positive monthly salary (or an existing partial payroll row with a balance due)."),
                MsgBoxTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            Loc.Admin("salMsgConfirmAll",
                "Record payroll for all {{count}} employee(s) with a balance due for {{period}}? Each person's payment will use their remaining net amount. You can still enter a custom amount when paying one employee at a time.",
                new Dictionary<string, string>
                {
                    ["count"] = pending.Count.ToString(CultureInfo.InvariantCulture),
                    ["period"] = PayrollPeriodLabel
                }),
            Loc.Admin("salMsgConfirmAllTitle", "Confirm all payroll"),
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
        PayrollPaymentRemainingHint = SalaryUiLocalizer.FormatPaymentRemainingHint(
            row.HasPayrollRecord,
            row.TotalNetUsd,
            row.PaidToDateUsd,
            row.NetPay);
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
                Loc.Admin("salMsgPositivePayment", "Enter a positive payment amount in USD."),
                MsgBoxTitle,
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
                MessageBox.Show(
                    Loc.Admin("salMsgEmployeeNotFound", "Employee not found."),
                    MsgBoxTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
                MessageBox.Show(err, MsgBoxTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DesktopCloudPersistence.PushBatchBlocking(DesktopCloudPersistence.ToUpsertOperations(upserts));
            ClosePayrollPaymentDialog();
            MessageBox.Show(
                Loc.Admin("salMsgPaymentSaved",
                    "Payroll payment saved. Money shows a Salary expense for the amount you entered. Run Refresh if amounts look stale."),
                MsgBoxTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            await ReloadRowsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                MsgBoxTitle,
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
                    MessageBox.Show(
                        Loc.Admin("salMsgEmployeeNotFoundFor", "{{name}}: Employee not found.",
                            new Dictionary<string, string> { ["name"] = row.EmployeeName }),
                        MsgBoxTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
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
                        Loc.Admin("salMsgEmployeeError", "{{name}}: {{error}}",
                            new Dictionary<string, string> { ["name"] = row.EmployeeName, ["error"] = err }),
                        MsgBoxTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                DesktopCloudPersistence.PushBatchBlocking(DesktopCloudPersistence.ToUpsertOperations(upserts));
            }

            MessageBox.Show(
                Loc.Admin("salMsgPayrollSaved",
                    "Payroll saved. Daily and Employees reports include Money salary lines for each payment. Employee timeline shows advances and payments."),
                MsgBoxTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            await ReloadRowsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                MsgBoxTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task RecordSalaryAdvanceAsync()
    {
        if (SelectedAdvanceEmployee is null)
        {
            MessageBox.Show(
                Loc.Admin("salAdvMsgSelectEmployee", "Select an employee."),
                MsgBoxAdvanceTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!decimal.TryParse(AdvanceAmountText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amt) ||
            amt <= 0m)
        {
            MessageBox.Show(
                Loc.Admin("salAdvMsgPositiveAmount", "Enter a positive amount (USD)."),
                MsgBoxAdvanceTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
                    Loc.Admin("salAdvMsgPayrollConfirmed",
                        "Payroll is already confirmed for this employee for the selected month. Advances are not allowed for that period."),
                    MsgBoxAdvanceTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var emp = (await _data.GetEmployeesAsync().ConfigureAwait(true)).FirstOrDefault(e => e.Id == SelectedAdvanceEmployee.Id);
            if (emp is null)
            {
                MessageBox.Show(
                    Loc.Admin("salMsgEmployeeNotFound", "Employee not found."),
                    MsgBoxAdvanceTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var monthBase = PayrollCalculator.ResolvePayrollMonthBase(emp, SelectedPayrollYear, SelectedPayrollMonth);
            var scheduledGrossUsd = monthBase.GrossPayUsd;
            var payrollRules = PayrollCalculator.ResolveSalaryPayrollRulesForLocalFile();

            if ((emp.MonthlySalaryUSD <= 0m && emp.HourlyRate <= 0m) || scheduledGrossUsd <= 0m)
            {
                MessageBox.Show(
                    Loc.Admin("salAdvMsgNoPayrollBase",
                        "Advances are limited to {{pct}}% of payroll gross for the month. This employee has no payroll base for the selected period — set a positive monthly salary (USD) in Employees, then try again.",
                        new Dictionary<string, string>
                        {
                            ["pct"] = payrollRules.MaxSalaryAdvancePercentOfGross.ToString("0.##", CultureInfo.InvariantCulture)
                        }),
                    MsgBoxAdvanceTitle,
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
                    Loc.Admin("salAdvMsgCapExceeded",
                        "Each employee's advances for this payroll month cannot exceed {{pct}}% of that month's payroll gross (prorated monthly salary).{{nl}}{{nl}}Payroll gross for {{period}}: {{gross}}{{nl}}{{pct}}% cap: {{cap}}{{nl}}Pending advances already recorded for this month: {{pending}}{{nl}}You can add at most {{remaining}} now (requested {{requested}}).",
                        new Dictionary<string, string>
                        {
                            ["pct"] = payrollRules.MaxSalaryAdvancePercentOfGross.ToString("0.##", CultureInfo.InvariantCulture),
                            ["nl"] = Environment.NewLine,
                            ["period"] = PayrollPeriodLabel,
                            ["gross"] = SalaryUiLocalizer.FormatUsd(scheduledGrossUsd),
                            ["cap"] = SalaryUiLocalizer.FormatUsd(advanceCapUsd),
                            ["pending"] = SalaryUiLocalizer.FormatUsd(existingPendingUsd),
                            ["remaining"] = SalaryUiLocalizer.FormatUsd(remainingUsd),
                            ["requested"] = SalaryUiLocalizer.FormatUsd(amt)
                        }),
                    MsgBoxAdvanceTitle,
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
                    Loc.Admin("salAdvMsgSaveUnconfirmed",
                        "Advance was saved but could not be confirmed from the server. Refresh Salary and check Money."),
                    MsgBoxAdvanceTitle,
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
                Loc.Admin("salAdvMsgRecorded",
                    "Advance recorded for payroll {{period}} and posted to Money. It is deducted when you confirm that month for this employee.",
                    new Dictionary<string, string> { ["period"] = PayrollPeriodLabel }),
                MsgBoxAdvanceTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            _ = ReloadRowsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                MsgBoxAdvanceTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
