using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Sync;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Localization;
using EliteRestaurantPro.Services;

namespace EliteRestaurantPro.ViewModels;

public sealed class AttendanceRowViewModel : BaseViewModel
{
    public int EmployeeId { get; init; }
    public DateTime WorkDate { get; init; }
    public string EmployeeUniqueId { get; init; } = string.Empty;
    public string EmployeeName { get; init; } = string.Empty;
    public string ScheduledStartText { get; set; } = "08:00 AM";
    public string ShiftName { get; init; } = "Morning Shift";
    public string ShiftWindowText { get; set; } = "12:00 PM - 06:00 PM";
    public TimeSpan ShiftStartTime { get; init; } = new(12, 0, 0);
    public TimeSpan ShiftEndTime { get; init; } = new(18, 0, 0);
    public bool IsScheduledOff { get; init; }

    private string _clockInText = "Not clocked in";
    public string ClockInText
    {
        get => _clockInText;
        set => SetField(ref _clockInText, value);
    }

    private string _clockOutText = "Not clocked out";
    public string ClockOutText
    {
        get => _clockOutText;
        set => SetField(ref _clockOutText, value);
    }

    private string _statusText = "Pending";
    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    private bool _canClockIn = true;
    public bool CanClockIn
    {
        get => _canClockIn;
        set => SetField(ref _canClockIn, value);
    }

    private bool _canClockOut;
    public bool CanClockOut
    {
        get => _canClockOut;
        set => SetField(ref _canClockOut, value);
    }

    private string _lateJustificationText = string.Empty;
    public string LateJustificationText
    {
        get => _lateJustificationText;
        set => SetField(ref _lateJustificationText, value);
    }

    private bool _showLateJustification;
    public bool ShowLateJustification
    {
        get => _showLateJustification;
        set => SetField(ref _showLateJustification, value);
    }

    private string _pendingSalaryText = "Pending Salary: $ 0.00";
    public string PendingSalaryText
    {
        get => _pendingSalaryText;
        set => SetField(ref _pendingSalaryText, value);
    }

    public int? AttendanceId { get; init; }

    private bool _showAbsenceJustification;
    public bool ShowAbsenceJustification
    {
        get => _showAbsenceJustification;
        set => SetField(ref _showAbsenceJustification, value);
    }

    private string _absenceJustification = string.Empty;
    public string AbsenceJustification
    {
        get => _absenceJustification;
        set => SetField(ref _absenceJustification, value);
    }

    public bool IsDayLocked { get; init; }

    public bool CanMarkAbsence { get; init; }

    /// <summary>Editable absence note (before day is validated).</summary>
    public bool ShowAbsenceJustificationEditor { get; init; }

    /// <summary>Read-only absence note after validate.</summary>
    public bool ShowAbsenceJustificationReadOnly { get; init; }

    public string DisplayShiftLine { get; set; } = string.Empty;
    public string DisplayClockLine { get; set; } = string.Empty;
    public string DisplayStatusLine { get; set; } = string.Empty;
    public string DisplayPendingSalaryText { get; set; } = string.Empty;
    public string DisplayLateJustificationText { get; set; } = string.Empty;
}

public sealed class AttendanceDayGroupViewModel
{
    public DateTime WorkDate { get; init; }
    public string DayText { get; set; } = string.Empty;
    public string EmployeesCountText { get; set; } = string.Empty;
    public bool IsExpanded { get; set; }
    public bool IsDayValidated { get; init; }
    public bool CanValidateAttendance => !IsDayValidated;
    public int RowCount => Rows.Count;
    public ObservableCollection<AttendanceRowViewModel> Rows { get; init; } = [];
}

public class AttendanceViewModel : AdminBaseViewModel
{
    private enum ShiftListFilter
    {
        All,
        Morning,
        Night,
        FullDay
    }

    private readonly AdminDataApiClient _data = new();
    private const string PendingSalaryReferencePrefix = "Pending salary accrual:";

    public string AttendanceShiftSummaryText
    {
        get => _attendanceShiftSummaryText;
        private set => SetField(ref _attendanceShiftSummaryText, value);
    }

    public bool ShiftFilterAllSelected => _shiftListFilter == ShiftListFilter.All;

    public bool ShiftFilterMorningSelected => _shiftListFilter == ShiftListFilter.Morning;

    public bool ShiftFilterNightSelected => _shiftListFilter == ShiftListFilter.Night;

    public bool ShiftFilterFullDaySelected => _shiftListFilter == ShiftListFilter.FullDay;

    public ICommand SetShiftFilterAllCommand { get; }
    public ICommand SetShiftFilterMorningCommand { get; }
    public ICommand SetShiftFilterNightCommand { get; }
    public ICommand SetShiftFilterFullDayCommand { get; }

    public override string ActivePage => "Attendance";

    public string PageTitle => Loc.Admin("attTitle", "Attendance");
    public string PageTitleAccent => Loc.Admin("attTitleAccent", "Control");
    public string ShowLabel => Loc.Admin("attShowLabel", "Show:");
    public string FilterAllLabel => Loc.Admin("attFilterAll", "All");
    public string FilterMorningLabel => Loc.Admin("attFilterMorning", "Morning shift");
    public string FilterNightLabel => Loc.Admin("attFilterNight", "Night shift");
    public string FilterFullDayLabel => Loc.Admin("attFilterFullDay", "Full day");
    public string SearchTooltip => Loc.Admin("attSearchTooltip", "Search by employee name, ID, shift, or status");
    public string ValidateLabel => Loc.Admin("attValidate", "Validate attendance");
    public string ClockInLabel => Loc.Admin("attClockInBtn", "Clock In");
    public string ClockOutLabel => Loc.Admin("attClockOutBtn", "Clock Out");
    public string AbsenceLabel => Loc.Admin("attAbsence", "Absence");
    public string AbsenceEditorLabel => Loc.Admin("attAbsenceEditorLabel", "ABSENCE JUSTIFICATION (required before Validate)");
    public string AbsenceReadOnlyLabel => Loc.Admin("attAbsenceReadOnlyLabel", "ABSENCE JUSTIFICATION (validated — read only)");
    public string MarkAbsenceBody => Loc.Admin("attMarkAbsenceBody", "Record that this employee did not work this day. Justification is required.");
    public string LateDialogBody => Loc.Admin("attLateDialogBody", "Employee clocked in after 08:30 AM. Please provide a justification.");
    public string JustificationRequiredLabel => Loc.Admin("attJustificationRequired", "JUSTIFICATION (required)");
    public string SaveAbsenceLabel => Loc.Admin("attSaveAbsence", "Save absence");
    public string SaveLateClockInLabel => Loc.Admin("attSaveLateClockIn", "Save Late Clock-In");
    public string CancelLabel => Loc.Admin("attCancel", "Cancel");

    public ObservableCollection<AttendanceDayGroupViewModel> AttendanceDayGroups { get; } = [];

    private readonly List<AttendanceDayGroupViewModel> _attendanceSourceGroups = [];
    private string _searchText = string.Empty;
    private ShiftListFilter _shiftListFilter = ShiftListFilter.All;
    private AttendanceShiftSchedule _shiftSchedule = AttendanceShiftSchedule.Defaults;
    private TimeSpan _lateGraceWindow = TimeSpan.FromMinutes(30);
    private string _attendanceShiftSummaryText = string.Empty;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value))
                return;
            if (_attendanceSourceGroups.Count > 0)
                ApplyListFilter();
        }
    }

    private bool _isJustificationDialogOpen;
    public bool IsJustificationDialogOpen
    {
        get => _isJustificationDialogOpen;
        set => SetField(ref _isJustificationDialogOpen, value);
    }

    private string _lateJustification = string.Empty;
    public string LateJustification
    {
        get => _lateJustification;
        set => SetField(ref _lateJustification, value);
    }

    private string _dialogTitle = string.Empty;
    public string DialogTitle
    {
        get => _dialogTitle;
        set => SetField(ref _dialogTitle, value);
    }

    private int _pendingLateEmployeeId;
    private string _lateDialogEmployeeName = string.Empty;
    private string _lateDialogShiftName = string.Empty;

    private bool _isMarkAbsenceDialogOpen;
    public bool IsMarkAbsenceDialogOpen
    {
        get => _isMarkAbsenceDialogOpen;
        set => SetField(ref _isMarkAbsenceDialogOpen, value);
    }

    private string _markAbsenceDialogTitle = string.Empty;
    public string MarkAbsenceDialogTitle
    {
        get => _markAbsenceDialogTitle;
        set => SetField(ref _markAbsenceDialogTitle, value);
    }

    private string _markAbsenceEmployeeName = string.Empty;
    private string _markAbsenceJustification = string.Empty;
    public string MarkAbsenceJustification
    {
        get => _markAbsenceJustification;
        set => SetField(ref _markAbsenceJustification, value);
    }

    private int _markAbsenceEmployeeId;
    private DateTime _markAbsenceWorkDate;

    public ICommand ClockInCommand { get; }
    public ICommand ClockOutCommand { get; }
    public ICommand SaveLateClockInCommand { get; }
    public ICommand CancelLateClockInCommand { get; }
    public ICommand ValidateAttendanceCommand { get; }
    public ICommand MarkAbsenceCommand { get; }
    public ICommand SaveMarkAbsenceCommand { get; }
    public ICommand CancelMarkAbsenceCommand { get; }

    public AttendanceViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        ClockInCommand = new RelayCommand(ClockIn);
        ClockOutCommand = new RelayCommand(p => _ = ClockOutAsync(p));
        SaveLateClockInCommand = new RelayCommand(_ => _ = SaveLateClockInAsync());
        CancelLateClockInCommand = new RelayCommand(_ => CloseLateDialog());
        ValidateAttendanceCommand = new RelayCommand(p => _ = ValidateAttendanceAsync(p));
        MarkAbsenceCommand = new RelayCommand(OpenMarkAbsenceDialog);
        SaveMarkAbsenceCommand = new RelayCommand(_ => _ = SaveMarkAbsenceAsync());
        CancelMarkAbsenceCommand = new RelayCommand(_ => CloseMarkAbsenceDialog());
        SetShiftFilterAllCommand = new RelayCommand(_ => SetShiftListFilter(ShiftListFilter.All));
        SetShiftFilterMorningCommand = new RelayCommand(_ => SetShiftListFilter(ShiftListFilter.Morning));
        SetShiftFilterNightCommand = new RelayCommand(_ => SetShiftListFilter(ShiftListFilter.Night));
        SetShiftFilterFullDayCommand = new RelayCommand(_ => SetShiftListFilter(ShiftListFilter.FullDay));
        RefreshShiftSettingsFromDisk();
        _ = LoadAttendanceAsync();
    }

    private async Task LoadAttendanceAsync()
    {
        RefreshShiftSettingsFromDisk();
        var today = DateTime.Today;
        var fromDate = today.AddDays(-13);

        try
        {
            var validationsTask = _data.GetAttendanceDayValidationsAsync();
            var employeesTask = _data.GetEmployeesAsync();
            var attendanceTask = _data.GetAttendanceAsync();
            var moneyTask = _data.GetMoneyTransactionsAsync();
            await Task.WhenAll(validationsTask, employeesTask, attendanceTask, moneyTask).ConfigureAwait(true);

            var validations = await validationsTask.ConfigureAwait(true);
            var validationRangeStart = AttendanceCalendar.DayAnchorUtc(fromDate);
            var validationRangeEndExclusive = AttendanceCalendar.DayAnchorUtc(today).AddDays(1);
            var validatedDates = validations
                .Where(v => v.WorkDate >= validationRangeStart && v.WorkDate < validationRangeEndExclusive)
                .Select(v => v.WorkDate.Date)
                .ToHashSet();

            var employees = (await employeesTask.ConfigureAwait(true)).OrderBy(e => e.Name).ToList();
            var attendanceList = (await attendanceTask.ConfigureAwait(true)).ToList();

            var historyStart = AttendanceCalendar.DayAnchorUtc(fromDate);
            var historyEndExclusive = AttendanceCalendar.DayAnchorUtc(today).AddDays(1);
            var attendanceInRange = attendanceList
                .Where(a => a.WorkDate >= historyStart && a.WorkDate < historyEndExclusive)
                .ToList();

            var autoOps = AttendanceCloudHelper.BuildAutoAbsenceUpserts(
                employees.Where(e => e.EmploymentStatus == "Active").ToList(),
                attendanceInRange,
                fromDate,
                today,
                validatedDates);

            if (autoOps.Count > 0)
            {
                DesktopCloudPersistence.PushBatchBlocking(autoOps);
                attendanceList = (await _data.GetAttendanceAsync().ConfigureAwait(true)).ToList();
                attendanceInRange = attendanceList
                    .Where(a => a.WorkDate >= historyStart && a.WorkDate < historyEndExclusive)
                    .ToList();
            }

            var todayPendingSalariesByEmployeeId = (await moneyTask.ConfigureAwait(true))
                .Where(t =>
                    t.Type == "Expense" &&
                    t.Category == "Salary" &&
                    t.Date.Date == today &&
                    t.Justification.StartsWith(PendingSalaryReferencePrefix))
                .GroupBy(t => ParseEmployeeIdFromPendingSalaryJustification(t.Justification))
                .Where(g => g.Key.HasValue)
                .ToDictionary(g => g.Key!.Value, g => g.Sum(x => x.Amount));

            var employeeById = employees.ToDictionary(e => e.Id);
            foreach (var a in attendanceInRange)
            {
                if (a.Employee is null && employeeById.TryGetValue(a.EmployeeId, out var emp))
                    a.Employee = emp;
            }

            var todayStart = AttendanceCalendar.DayAnchorUtc(today);
            var todayEndExclusive = todayStart.AddDays(1);

            var attendanceHistory = attendanceInRange
                .OrderByDescending(a => a.WorkDate)
                .ThenBy(a => a.Employee?.Name)
                .ToList();

            var todayAttendanceByEmployee = attendanceHistory
                .Where(a => a.WorkDate >= todayStart && a.WorkDate < todayEndExclusive)
                .GroupBy(a => a.EmployeeId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

            AttendanceDayGroups.Clear();

            var todayValidated = validatedDates.Contains(today);
            var todayGroup = new AttendanceDayGroupViewModel
            {
                WorkDate = today,
                IsExpanded = true,
                IsDayValidated = todayValidated
            };
            todayGroup.DayText = AdminTextLocalizer.FormatTodayCalendarDay(today, todayValidated);

            foreach (var employee in employees)
            {
                todayAttendanceByEmployee.TryGetValue(employee.Id, out var attendance);
                todayPendingSalariesByEmployeeId.TryGetValue(employee.Id, out var pendingSalaryToday);
                var row = BuildAttendanceRow(employee, attendance, today, isCurrentDay: true, pendingSalaryToday, todayValidated);
                AttendanceUiLocalizer.ApplyRow(row);
                todayGroup.Rows.Add(row);
            }

            AttendanceDayGroups.Add(todayGroup);

            var historyGroups = attendanceHistory
                .Where(a => a.WorkDate < todayStart)
                .GroupBy(a => a.WorkDate.Date)
                .OrderByDescending(g => g.Key);

            foreach (var dayGroup in historyGroups)
            {
                var dayValidated = validatedDates.Contains(dayGroup.Key);
                var vm = new AttendanceDayGroupViewModel
                {
                    WorkDate = dayGroup.Key,
                    IsExpanded = false,
                    IsDayValidated = dayValidated
                };
                vm.DayText = AdminTextLocalizer.FormatCalendarDay(dayGroup.Key, dayValidated);

                foreach (var attendance in dayGroup.OrderBy(a => a.Employee?.Name))
                {
                    if (attendance.Employee is null)
                        continue;
                    var row = BuildAttendanceRow(attendance.Employee, attendance, dayGroup.Key, isCurrentDay: false, pendingSalary: 0m, dayValidated);
                    AttendanceUiLocalizer.ApplyRow(row);
                    vm.Rows.Add(row);
                }

                AttendanceDayGroups.Add(vm);
            }

            SnapshotAttendanceSource();
            LocalizeAttendanceGroups(today);
            ApplyListFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                Loc.Admin("attLoadFailedTitle", "Attendance load failed"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void SnapshotAttendanceSource()
    {
        _attendanceSourceGroups.Clear();
        foreach (var g in AttendanceDayGroups)
        {
            var copy = new AttendanceDayGroupViewModel
            {
                WorkDate = g.WorkDate,
                DayText = g.DayText,
                IsExpanded = g.IsExpanded,
                IsDayValidated = g.IsDayValidated
            };
            foreach (var r in g.Rows)
                copy.Rows.Add(r);
            _attendanceSourceGroups.Add(copy);
        }
    }

    private void RefreshShiftSettingsFromDisk()
    {
        var att = SettingsManager.Load().Attendance ?? new AttendanceSettings();
        _shiftSchedule = AttendanceShiftSchedule.FromSettings(att);
        var grace = att.LateClockInGraceMinutes;
        if (grace < 0)
            grace = 0;
        if (grace > 240)
            grace = 240;
        _lateGraceWindow = TimeSpan.FromMinutes(grace);
        var s = _shiftSchedule;
        var morning = s.FormatMorningRange().Replace("-", " - ");
        var night = s.FormatNightRange().Replace("-", " - ");
        var fullDay = s.FormatFullDayRange().Replace("-", " - ");
        AttendanceShiftSummaryText = Loc.Admin("attShiftSummary",
            "Morning: {{morning}} | Night: {{night}} | Full day: {{fullDay}} (morning start → night end) | Late grace: {{grace}} min",
            new Dictionary<string, string>
            {
                ["morning"] = morning,
                ["night"] = night,
                ["fullDay"] = fullDay,
                ["grace"] = grace.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
    }

    private void LocalizeAttendanceGroups(DateTime today)
    {
        foreach (var group in _attendanceSourceGroups)
            AttendanceUiLocalizer.ApplyDayGroup(group, today);
        foreach (var group in AttendanceDayGroups)
            AttendanceUiLocalizer.ApplyDayGroup(group, today);
    }

    private void SetShiftListFilter(ShiftListFilter filter)
    {
        if (_shiftListFilter == filter)
            return;
        _shiftListFilter = filter;
        OnPropertyChanged(nameof(ShiftFilterAllSelected));
        OnPropertyChanged(nameof(ShiftFilterMorningSelected));
        OnPropertyChanged(nameof(ShiftFilterNightSelected));
        OnPropertyChanged(nameof(ShiftFilterFullDaySelected));
        ApplyListFilter();
    }

    private void ApplyListFilter()
    {
        var q = (_searchText ?? string.Empty).Trim();
        AttendanceDayGroups.Clear();
        foreach (var source in _attendanceSourceGroups)
        {
            IEnumerable<AttendanceRowViewModel> rows = source.Rows;
            rows = _shiftListFilter switch
            {
                ShiftListFilter.Morning => rows.Where(r =>
                    !r.IsScheduledOff &&
                    r.ShiftName.Contains("Morning", StringComparison.OrdinalIgnoreCase)),
                ShiftListFilter.Night => rows.Where(r =>
                    !r.IsScheduledOff &&
                    (r.ShiftName.Contains("Night", StringComparison.OrdinalIgnoreCase) ||
                     r.ShiftName.Contains("Evening", StringComparison.OrdinalIgnoreCase))),
                ShiftListFilter.FullDay => rows.Where(r =>
                    !r.IsScheduledOff &&
                    r.ShiftName.Contains("Full", StringComparison.OrdinalIgnoreCase)),
                _ => rows
            };
            var list = q.Length == 0
                ? rows.ToList()
                : rows.Where(r => AttendanceRowMatches(r, q)).ToList();
            if (list.Count == 0)
                continue;

            var vm = new AttendanceDayGroupViewModel
            {
                WorkDate = source.WorkDate,
                DayText = source.DayText,
                IsExpanded = source.IsExpanded,
                IsDayValidated = source.IsDayValidated
            };
            foreach (var r in list)
                vm.Rows.Add(r);
            AttendanceUiLocalizer.ApplyDayGroup(vm, DateTime.Today);
            AttendanceDayGroups.Add(vm);
        }
    }

    private static bool AttendanceRowMatches(AttendanceRowViewModel r, string q)
    {
        bool Hit(string? s)
            => !string.IsNullOrEmpty(s) && s.Contains(q, StringComparison.OrdinalIgnoreCase);

        return Hit(r.EmployeeName)
               || Hit(r.EmployeeUniqueId)
               || Hit(r.ShiftName)
               || Hit(r.StatusText)
               || Hit(r.ClockInText)
               || Hit(r.ClockOutText)
               || Hit(r.PendingSalaryText)
               || Hit(r.AbsenceJustification);
    }

    private async Task ValidateAttendanceAsync(object? parameter)
    {
        if (parameter is not AttendanceDayGroupViewModel group)
            return;

        if (group.IsDayValidated)
        {
            MessageBox.Show(
                Loc.Admin("attDayAlreadyValidated", "This day has already been validated."),
                Loc.Admin("attValidateTitle", "Validate attendance"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var absentWithout = group.Rows
            .Where(r => r.ShowAbsenceJustification && string.IsNullOrWhiteSpace(r.AbsenceJustification))
            .ToList();
        if (absentWithout.Count > 0)
        {
            MessageBox.Show(
                Loc.Admin("attValidateAbsenceRequired", "Fill absence justification for every absent staff member before validating this day."),
                Loc.Admin("attValidateTitle", "Validate attendance"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            var validations = (await _data.GetAttendanceDayValidationsAsync().ConfigureAwait(true)).ToList();
            var attendanceRows = (await _data.GetAttendanceAsync().ConfigureAwait(true)).ToList();
            var (dayStartUtc, dayEndExclusiveUtc) = AttendanceCalendar.DayRangeUtc(group.WorkDate);

            var ops = new List<CloudSyncOperation>();
            foreach (var row in group.Rows.Where(r => r.AttendanceId.HasValue))
            {
                var att = attendanceRows.FirstOrDefault(a => a.Id == row.AttendanceId!.Value);
                if (att is null || !att.IsAbsence)
                    continue;

                var saved = CopyAttendance(att);
                saved.AbsenceJustification = row.AbsenceJustification.Trim();
                ops.Add(AttendanceCloudHelper.ToUpsert(saved));
            }

            if (!validations.Any(v => v.WorkDate >= dayStartUtc && v.WorkDate < dayEndExclusiveUtc))
            {
                ops.Add(AttendanceCloudHelper.ToUpsert(new AttendanceDayValidation
                {
                    WorkDate = dayStartUtc,
                    ValidatedAtUtc = DateTime.UtcNow
                }));
            }

            if (ops.Count == 0)
            {
                MessageBox.Show(
                    Loc.Admin("attNothingToValidate", "Nothing to validate for this day."),
                    Loc.Admin("attValidateTitle", "Validate attendance"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            DesktopCloudPersistence.PushBatchBlocking(ops);
            MessageBox.Show(
                Loc.Admin("attValidateSuccess", "Attendance for {{day}} validated and justifications saved.",
                    new Dictionary<string, string> { ["day"] = group.DayText }),
                Loc.Admin("attValidateTitle", "Validate attendance"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            await LoadAttendanceAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                Loc.Admin("attValidateFailed", "Validate attendance failed"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OpenMarkAbsenceDialog(object? parameter)
    {
        if (parameter is not AttendanceRowViewModel row)
            return;

        if (row.IsDayLocked)
        {
            MessageBox.Show(
                Loc.Admin("attDayLockedAbsence", "This day is validated; absences cannot be changed."),
                Loc.Admin("attTitle", "Attendance"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!row.CanMarkAbsence)
        {
            MessageBox.Show(
                Loc.Admin("attMarkAbsenceUnavailable", "Mark absence is only available when the employee has not clocked in and is scheduled to work."),
                Loc.Admin("attTitle", "Attendance"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _markAbsenceEmployeeId = row.EmployeeId;
        _markAbsenceWorkDate = row.WorkDate.Date;
        _markAbsenceEmployeeName = row.EmployeeName;
        ApplyMarkAbsenceDialogTitle();
        MarkAbsenceJustification = row.AbsenceJustification;
        IsMarkAbsenceDialogOpen = true;
    }

    private async Task SaveMarkAbsenceAsync()
    {
        if (string.IsNullOrWhiteSpace(MarkAbsenceJustification))
        {
            MessageBox.Show(
                Loc.Admin("attEnterAbsenceJustification", "Enter an absence justification."),
                Loc.Admin("attMarkAbsenceTitle", "Mark absence"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            var attendanceRows = (await _data.GetAttendanceAsync().ConfigureAwait(true)).ToList();
            var workDate = _markAbsenceWorkDate.Date;
            var (markDayStartUtc, markDayEndExclusiveUtc) = AttendanceCalendar.DayRangeUtc(workDate);
            var att = attendanceRows.FirstOrDefault(a =>
                a.EmployeeId == _markAbsenceEmployeeId && a.WorkDate >= markDayStartUtc && a.WorkDate < markDayEndExclusiveUtc);
            if (att is null)
            {
                att = new EmployeeAttendance
                {
                    EmployeeId = _markAbsenceEmployeeId,
                    WorkDate = markDayStartUtc
                };
            }
            else
            {
                att = CopyAttendance(att);
            }

            att.ClockInTime = null;
            att.ClockOutTime = null;
            att.IsAbsence = true;
            att.ClockInStatus = "Absent";
            att.AbsenceJustification = MarkAbsenceJustification.Trim();
            att.Justification = string.Empty;

            DesktopCloudPersistence.PushUpsertBlocking(att);
            CloseMarkAbsenceDialog();
            MessageBox.Show(
                Loc.Admin("attAbsenceSaved", "Absence saved."),
                Loc.Admin("attTitle", "Attendance"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            await LoadAttendanceAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                Loc.Admin("attSaveAbsenceFailed", "Save absence failed"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void CloseMarkAbsenceDialog()
    {
        IsMarkAbsenceDialogOpen = false;
        MarkAbsenceJustification = string.Empty;
        _markAbsenceEmployeeId = 0;
        _markAbsenceEmployeeName = string.Empty;
    }

    private void ClockIn(object? parameter)
    {
        if (parameter is not AttendanceRowViewModel row)
            return;

        if (row.WorkDate.Date != DateTime.Today)
        {
            MessageBox.Show(
                Loc.Admin("attClockInTodayOnly", "Clock in can only be recorded for today."),
                Loc.Admin("attTitle", "Attendance"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (row.IsScheduledOff)
        {
            MessageBox.Show(
                Loc.Admin("attClockInOffShift", "This employee is off shift today. Clock-in is disabled for off days."),
                Loc.Admin("attTitle", "Attendance"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var now = DateTime.Now;
        var lateCutoff = row.ShiftStartTime + _lateGraceWindow;
        if (now.TimeOfDay > lateCutoff)
        {
            _pendingLateEmployeeId = row.EmployeeId;
            _lateDialogEmployeeName = row.EmployeeName;
            _lateDialogShiftName = row.ShiftName;
            LateJustification = string.Empty;
            ApplyLateDialogTitle();
            IsJustificationDialogOpen = true;
            return;
        }

        var status = now.TimeOfDay < row.ShiftStartTime ? "Early" : "On Time";
        _ = SaveClockInAsync(row.EmployeeId, status, string.Empty, now);
    }

    private async Task SaveLateClockInAsync()
    {
        if (_pendingLateEmployeeId <= 0)
            return;

        if (string.IsNullOrWhiteSpace(LateJustification))
        {
            MessageBox.Show(
                Loc.Admin("attLateJustificationRequired", "Please provide a justification for late clock-in."),
                Loc.Admin("attTitle", "Attendance"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        await SaveClockInAsync(_pendingLateEmployeeId, "Late", LateJustification.Trim(), DateTime.Now).ConfigureAwait(true);
        CloseLateDialog();
    }

    private async Task SaveClockInAsync(int employeeId, string status, string justification, DateTime timestamp)
    {
        try
        {
            var today = DateTime.Today;
            var (todayStartUtc, todayEndExclusiveUtc) = AttendanceCalendar.DayRangeUtc(today);
            var attendanceRows = (await _data.GetAttendanceAsync().ConfigureAwait(true)).ToList();
            var attendance = attendanceRows.FirstOrDefault(a =>
                a.EmployeeId == employeeId && a.WorkDate >= todayStartUtc && a.WorkDate < todayEndExclusiveUtc);

            if (attendance is null)
            {
                attendance = new EmployeeAttendance
                {
                    EmployeeId = employeeId,
                    WorkDate = todayStartUtc
                };
            }
            else
            {
                attendance = CopyAttendance(attendance);
            }

            attendance.ClockInTime = timestamp;
            attendance.ClockInStatus = status;
            attendance.Justification = justification;
            attendance.IsAbsence = false;
            attendance.AbsenceJustification = string.Empty;

            DesktopCloudPersistence.PushUpsertBlocking(attendance);

            MessageBox.Show(
                Loc.Admin("attClockInSaved", "{{status}} clock-in saved.",
                    new Dictionary<string, string> { ["status"] = AdminTextLocalizer.TranslateAttendanceClockStatus(status) }),
                Loc.Admin("attTitle", "Attendance"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            await LoadAttendanceAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                Loc.Admin("attClockInFailed", "Clock-in failed"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task ClockOutAsync(object? parameter)
    {
        if (parameter is not AttendanceRowViewModel row)
            return;

        if (row.WorkDate.Date != DateTime.Today)
        {
            MessageBox.Show(
                Loc.Admin("attClockOutTodayOnly", "Clock out can only be recorded for today."),
                Loc.Admin("attTitle", "Attendance"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (row.IsScheduledOff)
        {
            MessageBox.Show(
                Loc.Admin("attClockOutOffShift", "This employee is off shift today. Clock-out is not expected."),
                Loc.Admin("attTitle", "Attendance"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var today = DateTime.Today;
            var (todayStartUtc, todayEndExclusiveUtc) = AttendanceCalendar.DayRangeUtc(today);
            var attendanceRows = (await _data.GetAttendanceAsync().ConfigureAwait(true)).ToList();
            var attendance = attendanceRows.FirstOrDefault(a =>
                a.EmployeeId == row.EmployeeId && a.WorkDate >= todayStartUtc && a.WorkDate < todayEndExclusiveUtc);
            if (attendance is null || attendance.ClockInTime is null)
            {
                MessageBox.Show(
                    Loc.Admin("attClockOutMustClockIn", "Employee must clock in before clocking out."),
                    Loc.Admin("attTitle", "Attendance"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (attendance.ClockOutTime is not null)
            {
                MessageBox.Show(
                    Loc.Admin("attClockOutAlready", "Employee is already clocked out for today."),
                    Loc.Admin("attTitle", "Attendance"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (DateTime.Now.TimeOfDay < row.ShiftStartTime)
            {
                MessageBox.Show(
                    Loc.Admin("attClockOutBeforeShift", "Clock-out is not allowed before the shift starts ({{time}}).",
                        new Dictionary<string, string> { ["time"] = FormatShiftTime(row.ShiftStartTime) }),
                    Loc.Admin("attTitle", "Attendance"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (DateTime.Now.TimeOfDay < row.ShiftEndTime)
            {
                var earlyClockOut = MessageBox.Show(
                    Loc.Admin("attClockOutEarlyPrompt", "Current time is before scheduled shift end ({{time}}). Record an early clock-out anyway?",
                        new Dictionary<string, string> { ["time"] = FormatShiftTime(row.ShiftEndTime) }),
                    Loc.Admin("attEarlyClockOutTitle", "Early Clock-Out"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (earlyClockOut != MessageBoxResult.Yes)
                    return;
            }

            var toSave = CopyAttendance(attendance);
            toSave.ClockOutTime = DateTime.Now;

            var employees = await _data.GetEmployeesAsync().ConfigureAwait(true);
            var employee = employees.FirstOrDefault(e => e.Id == row.EmployeeId);

            var ops = new List<CloudSyncOperation> { AttendanceCloudHelper.ToUpsert(toSave) };

            if (employee is not null && toSave.ClockInTime is not null && toSave.ClockOutTime is not null)
            {
                var workedDuration = toSave.ClockOutTime.Value - toSave.ClockInTime.Value;
                var workedHours = Math.Max(0m, (decimal)workedDuration.TotalHours);
                var (monthSchedHours, _, _) =
                    PayrollCalculator.GetHourlyGrossForPayrollMonth(employee, today.Year, today.Month);
                var pendingSalaryAmount = monthSchedHours > 0.0001m && employee.MonthlySalaryUSD > 0m
                    ? Math.Round(employee.MonthlySalaryUSD * (workedHours / monthSchedHours), 2)
                    : 0m;
                var pendingSalaryReference = BuildPendingSalaryReference(employee, today);

                var transactions = await _data.GetMoneyTransactionsAsync().ConfigureAwait(true);
                var existingPendingSalaryEntry = transactions.FirstOrDefault(t =>
                    t.Type == "Expense" &&
                    t.Category == "Salary" &&
                    t.Justification == pendingSalaryReference);

                MoneyTransaction moneyRow;
                if (existingPendingSalaryEntry is null)
                {
                    moneyRow = new MoneyTransaction
                    {
                        Amount = pendingSalaryAmount,
                        AmountUsd = pendingSalaryAmount,
                        AmountFc = CurrencyHelper.ConvertUsdToFc(pendingSalaryAmount),
                        Date = DateTime.Now,
                        Type = "Expense",
                        Category = "Salary",
                        CurrencyCode = CurrencyHelper.Usd,
                        ExchangeRateUsed = CurrencyHelper.FcPerUsd,
                        IsFixed = true,
                        Justification = pendingSalaryReference
                    };
                }
                else
                {
                    moneyRow = new MoneyTransaction
                    {
                        Id = existingPendingSalaryEntry.Id,
                        Amount = pendingSalaryAmount,
                        AmountUsd = pendingSalaryAmount,
                        AmountFc = CurrencyHelper.ConvertUsdToFc(pendingSalaryAmount),
                        Date = DateTime.Now,
                        Type = "Expense",
                        Category = "Salary",
                        CurrencyCode = CurrencyHelper.Usd,
                        ExchangeRateUsed = CurrencyHelper.FcPerUsd,
                        IsFixed = true,
                        Justification = pendingSalaryReference
                    };
                }

                ops.Add(AttendanceCloudHelper.ToUpsert(moneyRow));
            }

            DesktopCloudPersistence.PushBatchBlocking(ops);

            MessageBox.Show(
                Loc.Admin("attClockOutSaved", "Clock-out saved. Pending salary was updated in Money."),
                Loc.Admin("attTitle", "Attendance"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            await LoadAttendanceAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                Loc.Admin("attClockOutFailed", "Clock-out failed"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void CloseLateDialog()
    {
        _pendingLateEmployeeId = 0;
        _lateDialogEmployeeName = string.Empty;
        _lateDialogShiftName = string.Empty;
        IsJustificationDialogOpen = false;
        LateJustification = string.Empty;
    }

    private AttendanceRowViewModel BuildAttendanceRow(Employee employee, EmployeeAttendance? attendance, DateTime day, bool isCurrentDay, decimal pendingSalary, bool dayValidated)
    {
        var shiftDefinition = AttendanceScheduleHelper.ResolveShiftWindow(employee, day, _shiftSchedule);
        var isAbsence = attendance?.IsAbsence == true;
        var status = string.IsNullOrWhiteSpace(attendance?.ClockInStatus) ? "Pending" : attendance!.ClockInStatus;
        if (shiftDefinition.IsOff && attendance?.ClockInTime is null)
            status = "Off Shift";
        else if (isAbsence)
            status = "Absent";
        var isLate = status.Equals("Late", StringComparison.OrdinalIgnoreCase);
        var dayLocked = dayValidated;
        var showAbsenceBlock = isAbsence && !shiftDefinition.IsOff;

        return new AttendanceRowViewModel
        {
            EmployeeId = employee.Id,
            WorkDate = day.Date,
            EmployeeName = employee.Name,
            EmployeeUniqueId = employee.UniqueId,
            ShiftName = shiftDefinition.Name,
            ShiftWindowText = shiftDefinition.WindowText,
            ShiftStartTime = shiftDefinition.Start,
            ShiftEndTime = shiftDefinition.End,
            IsScheduledOff = shiftDefinition.IsOff,
            ScheduledStartText = day.Date.Add(shiftDefinition.Start).ToString("hh:mm tt"),
            ClockInText = attendance?.ClockInTime?.ToString("hh:mm tt") ?? "Not clocked in",
            ClockOutText = attendance?.ClockOutTime?.ToString("hh:mm tt") ?? "Not clocked out",
            StatusText = status,
            IsDayLocked = dayLocked,
            CanClockIn = isCurrentDay && !dayLocked && !shiftDefinition.IsOff && attendance?.ClockInTime is null,
            CanClockOut = isCurrentDay && !dayLocked && !shiftDefinition.IsOff && attendance?.ClockInTime is not null && attendance.ClockOutTime is null,
            CanMarkAbsence = !dayLocked && !shiftDefinition.IsOff && attendance?.ClockInTime is null,
            ShowLateJustification = isLate,
            ShowAbsenceJustification = showAbsenceBlock,
            ShowAbsenceJustificationEditor = showAbsenceBlock && !dayLocked,
            ShowAbsenceJustificationReadOnly = showAbsenceBlock && dayLocked,
            AbsenceJustification = attendance?.AbsenceJustification ?? string.Empty,
            AttendanceId = attendance?.Id,
            PendingSalaryText = $"Pending Salary: $ {pendingSalary:N2}",
            LateJustificationText = isLate
                ? $"Late justification: {FormatLateJustification(attendance?.Justification)}"
                : string.Empty
        };
    }

    private static string FormatLateJustification(string? justification)
        => string.IsNullOrWhiteSpace(justification) ? "No justification provided." : justification.Trim();

    private static EmployeeAttendance CopyAttendance(EmployeeAttendance a) =>
        new()
        {
            Id = a.Id,
            EmployeeId = a.EmployeeId,
            WorkDate = a.WorkDate,
            ClockInTime = a.ClockInTime,
            ClockOutTime = a.ClockOutTime,
            ClockInStatus = a.ClockInStatus,
            Justification = a.Justification,
            IsAbsence = a.IsAbsence,
            AbsenceJustification = a.AbsenceJustification
        };

    private static int? ParseEmployeeIdFromPendingSalaryJustification(string justification)
    {
        const string marker = "EMP-ID:";
        var markerIndex = justification.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return null;

        var start = markerIndex + marker.Length;
        var end = justification.IndexOf(' ', start);
        var token = end < 0 ? justification[start..] : justification[start..end];

        return int.TryParse(token, out var employeeId) ? employeeId : null;
    }

    private static string BuildPendingSalaryReference(Employee employee, DateTime workDate)
        => $"{PendingSalaryReferencePrefix} EMP-ID:{employee.Id} ({employee.UniqueId} - {employee.Name}) @ {workDate:yyyy-MM-dd}";

    private void ApplyLateDialogTitle()
    {
        DialogTitle =
            $"{Loc.Admin("attLateDialogTitle", "Late Clock In")} - {_lateDialogEmployeeName} ({AdminTextLocalizer.TranslateShift(_lateDialogShiftName)})";
    }

    private void ApplyMarkAbsenceDialogTitle()
    {
        var dateText = _markAbsenceWorkDate.ToString("MMMM dd, yyyy", AdminTextLocalizer.UiCulture);
        MarkAbsenceDialogTitle =
            $"{Loc.Admin("attMarkAbsenceTitle", "Mark absence")} — {_markAbsenceEmployeeName} ({dateText})";
    }

    private static string FormatShiftTime(TimeSpan time) =>
        DateTime.Today.Add(time).ToString("t", AdminTextLocalizer.UiCulture);

    protected override void RefreshLocalizedStrings()
    {
        base.RefreshLocalizedStrings();
        RefreshShiftSettingsFromDisk();
        Notify(
            nameof(PageTitle),
            nameof(PageTitleAccent),
            nameof(ShowLabel),
            nameof(FilterAllLabel),
            nameof(FilterMorningLabel),
            nameof(FilterNightLabel),
            nameof(FilterFullDayLabel),
            nameof(SearchTooltip),
            nameof(ValidateLabel),
            nameof(ClockInLabel),
            nameof(ClockOutLabel),
            nameof(AbsenceLabel),
            nameof(AbsenceEditorLabel),
            nameof(AbsenceReadOnlyLabel),
            nameof(MarkAbsenceBody),
            nameof(LateDialogBody),
            nameof(JustificationRequiredLabel),
            nameof(SaveAbsenceLabel),
            nameof(SaveLateClockInLabel),
            nameof(CancelLabel));

        LocalizeAttendanceGroups(DateTime.Today);

        if (IsMarkAbsenceDialogOpen)
            ApplyMarkAbsenceDialogTitle();
        if (IsJustificationDialogOpen)
            ApplyLateDialogTitle();

        ApplyListFilter();
    }
}
