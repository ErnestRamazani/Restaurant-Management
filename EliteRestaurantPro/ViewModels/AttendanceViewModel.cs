using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Linq;
using EliteRestaurantPro.Data;
using EliteRestaurantPro.Models;
using EliteRestaurantPro.Utils;
using Microsoft.EntityFrameworkCore;

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
}

public sealed class AttendanceDayGroupViewModel
{
    public DateTime WorkDate { get; init; }
    public string DayText { get; init; } = string.Empty;
    public bool IsExpanded { get; set; }
    public bool IsDayValidated { get; init; }
    public bool CanValidateAttendance => !IsDayValidated;
    public int RowCount => Rows.Count;
    public ObservableCollection<AttendanceRowViewModel> Rows { get; init; } = [];
}

public class AttendanceViewModel : AdminBaseViewModel
{
    private const string PendingSalaryReferencePrefix = "Pending salary accrual:";
    private static readonly TimeSpan LateGraceWindow = TimeSpan.FromMinutes(30);

    public override string ActivePage => "Attendance";

    public ObservableCollection<AttendanceDayGroupViewModel> AttendanceDayGroups { get; } = [];

    private readonly List<AttendanceDayGroupViewModel> _attendanceSourceGroups = [];
    private string _searchText = string.Empty;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value))
                return;
            if (_attendanceSourceGroups.Count > 0)
                ApplyAttendanceSearchFilter();
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

    private string _dialogTitle = "Late Clock In";
    public string DialogTitle
    {
        get => _dialogTitle;
        set => SetField(ref _dialogTitle, value);
    }

    private int _pendingLateEmployeeId;

    private bool _isMarkAbsenceDialogOpen;
    public bool IsMarkAbsenceDialogOpen
    {
        get => _isMarkAbsenceDialogOpen;
        set => SetField(ref _isMarkAbsenceDialogOpen, value);
    }

    private string _markAbsenceDialogTitle = "Mark absence";
    public string MarkAbsenceDialogTitle
    {
        get => _markAbsenceDialogTitle;
        set => SetField(ref _markAbsenceDialogTitle, value);
    }

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
        ClockOutCommand = new RelayCommand(ClockOut);
        SaveLateClockInCommand = new RelayCommand(_ => SaveLateClockIn());
        CancelLateClockInCommand = new RelayCommand(_ => CloseLateDialog());
        ValidateAttendanceCommand = new RelayCommand(ValidateAttendance);
        MarkAbsenceCommand = new RelayCommand(OpenMarkAbsenceDialog);
        SaveMarkAbsenceCommand = new RelayCommand(_ => SaveMarkAbsence());
        CancelMarkAbsenceCommand = new RelayCommand(_ => CloseMarkAbsenceDialog());
        LoadAttendance();
    }

    private void LoadAttendance()
    {
        var today = DateTime.Today;
        var fromDate = today.AddDays(-13);

        HashSet<DateTime> validatedDates;
        using (var syncDb = new AppDbContext())
        {
            validatedDates = syncDb.AttendanceDayValidations.AsNoTracking()
                .Where(v => v.WorkDate >= fromDate && v.WorkDate <= today.AddDays(1))
                .AsEnumerable()
                .Select(v => v.WorkDate.Date)
                .ToHashSet();
            AttendanceScheduleHelper.EnsureAutoAbsences(syncDb, fromDate, today, validatedDates);
        }

        using var db = new AppDbContext();

        var employees = db.Employees.AsNoTracking().OrderBy(e => e.Name).ToList();
        var todayPendingSalariesByEmployeeId = db.Transactions
            .AsNoTracking()
            .Where(t =>
                t.Type == "Expense" &&
                t.Category == "Salary" &&
                t.Date.Date == today &&
                t.Justification.StartsWith(PendingSalaryReferencePrefix))
            .ToList()
            .GroupBy(t => ParseEmployeeIdFromPendingSalaryJustification(t.Justification))
            .Where(g => g.Key.HasValue)
            .ToDictionary(g => g.Key!.Value, g => g.Sum(x => x.Amount));
        var attendanceHistory = db.EmployeeAttendances
            .AsNoTracking()
            .Include(a => a.Employee)
            .Where(a => a.WorkDate.Date >= fromDate && a.WorkDate.Date <= today)
            .OrderByDescending(a => a.WorkDate)
            .ThenBy(a => a.Employee!.Name)
            .ToList();

        var todayAttendanceByEmployee = attendanceHistory
            .Where(a => a.WorkDate.Date == today)
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        AttendanceDayGroups.Clear();

        var todayValidated = validatedDates.Contains(today);
        var todayGroup = new AttendanceDayGroupViewModel
        {
            WorkDate = today,
            DayText = todayValidated
                ? $"Today - {today:dddd, MMM dd yyyy} · Validated"
                : $"Today - {today:dddd, MMM dd yyyy}",
            IsExpanded = true,
            IsDayValidated = todayValidated
        };

        foreach (var employee in employees)
        {
            todayAttendanceByEmployee.TryGetValue(employee.Id, out var attendance);
            todayPendingSalariesByEmployeeId.TryGetValue(employee.Id, out var pendingSalaryToday);
            todayGroup.Rows.Add(BuildAttendanceRow(employee, attendance, today, isCurrentDay: true, pendingSalaryToday, todayValidated));
        }

        AttendanceDayGroups.Add(todayGroup);

        var historyGroups = attendanceHistory
            .Where(a => a.WorkDate.Date < today)
            .GroupBy(a => a.WorkDate.Date)
            .OrderByDescending(g => g.Key);

        foreach (var dayGroup in historyGroups)
        {
            var dayValidated = validatedDates.Contains(dayGroup.Key);
            var vm = new AttendanceDayGroupViewModel
            {
                WorkDate = dayGroup.Key,
                DayText = dayValidated
                    ? $"{dayGroup.Key:dddd, MMM dd yyyy} · Validated"
                    : dayGroup.Key.ToString("dddd, MMM dd yyyy"),
                IsExpanded = false,
                IsDayValidated = dayValidated
            };

            foreach (var attendance in dayGroup.OrderBy(a => a.Employee?.Name))
            {
                if (attendance.Employee is null)
                    continue;
                vm.Rows.Add(BuildAttendanceRow(attendance.Employee, attendance, dayGroup.Key, isCurrentDay: false, pendingSalary: 0m, dayValidated));
            }

            AttendanceDayGroups.Add(vm);
        }

        SnapshotAttendanceSource();
        ApplyAttendanceSearchFilter();
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

    private void ApplyAttendanceSearchFilter()
    {
        var q = (_searchText ?? string.Empty).Trim();
        AttendanceDayGroups.Clear();
        foreach (var source in _attendanceSourceGroups)
        {
            var list = q.Length == 0
                ? source.Rows.ToList()
                : source.Rows.Where(r => AttendanceRowMatches(r, q)).ToList();
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

    private void ValidateAttendance(object? parameter)
    {
        if (parameter is not AttendanceDayGroupViewModel group)
            return;

        if (group.IsDayValidated)
        {
            MessageBox.Show("This day has already been validated.", "Validate attendance", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var absentWithout = group.Rows
            .Where(r => r.ShowAbsenceJustification && string.IsNullOrWhiteSpace(r.AbsenceJustification))
            .ToList();
        if (absentWithout.Count > 0)
        {
            MessageBox.Show(
                "Fill absence justification for every absent staff member before validating this day.",
                "Validate attendance",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        using var db = new AppDbContext();
        foreach (var row in group.Rows.Where(r => r.AttendanceId.HasValue))
        {
            var att = db.EmployeeAttendances.FirstOrDefault(a => a.Id == row.AttendanceId!.Value);
            if (att is null)
                continue;
            if (att.IsAbsence)
                att.AbsenceJustification = row.AbsenceJustification.Trim();
        }

        var dayStart = group.WorkDate.Date;
        var dayEnd = dayStart.AddDays(1);
        if (!db.AttendanceDayValidations.Any(v => v.WorkDate >= dayStart && v.WorkDate < dayEnd))
        {
            db.AttendanceDayValidations.Add(new AttendanceDayValidation
            {
                WorkDate = dayStart,
                ValidatedAtUtc = DateTime.UtcNow
            });
        }

        db.SaveChanges();
        MessageBox.Show(
            $"Attendance for {group.DayText} validated and justifications saved.",
            "Validate attendance",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        LoadAttendance();
    }

    private void OpenMarkAbsenceDialog(object? parameter)
    {
        if (parameter is not AttendanceRowViewModel row)
            return;

        if (row.IsDayLocked)
        {
            MessageBox.Show("This day is validated; absences cannot be changed.", "Attendance", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!row.CanMarkAbsence)
        {
            MessageBox.Show("Mark absence is only available when the employee has not clocked in and is scheduled to work.", "Attendance", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _markAbsenceEmployeeId = row.EmployeeId;
        _markAbsenceWorkDate = row.WorkDate.Date;
        MarkAbsenceDialogTitle = $"Mark absence — {row.EmployeeName} ({row.WorkDate:MMM dd, yyyy})";
        MarkAbsenceJustification = row.AbsenceJustification;
        IsMarkAbsenceDialogOpen = true;
    }

    private void SaveMarkAbsence()
    {
        if (string.IsNullOrWhiteSpace(MarkAbsenceJustification))
        {
            MessageBox.Show("Enter an absence justification.", "Mark absence", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var db = new AppDbContext();
        var workDate = _markAbsenceWorkDate.Date;
        var att = db.EmployeeAttendances.FirstOrDefault(a => a.EmployeeId == _markAbsenceEmployeeId && a.WorkDate >= workDate && a.WorkDate < workDate.AddDays(1));
        if (att is null)
        {
            att = new EmployeeAttendance
            {
                EmployeeId = _markAbsenceEmployeeId,
                WorkDate = workDate
            };
            db.EmployeeAttendances.Add(att);
        }

        att.ClockInTime = null;
        att.ClockOutTime = null;
        att.IsAbsence = true;
        att.ClockInStatus = "Absent";
        att.AbsenceJustification = MarkAbsenceJustification.Trim();
        att.Justification = string.Empty;
        db.SaveChanges();
        CloseMarkAbsenceDialog();
        MessageBox.Show("Absence saved.", "Attendance", MessageBoxButton.OK, MessageBoxImage.Information);
        LoadAttendance();
    }

    private void CloseMarkAbsenceDialog()
    {
        IsMarkAbsenceDialogOpen = false;
        MarkAbsenceJustification = string.Empty;
        _markAbsenceEmployeeId = 0;
    }

    private void ClockIn(object? parameter)
    {
        if (parameter is not AttendanceRowViewModel row)
            return;

        if (row.WorkDate.Date != DateTime.Today)
        {
            MessageBox.Show("Clock in can only be recorded for today.", "Attendance", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.IsScheduledOff)
        {
            MessageBox.Show("This employee is off shift today. Clock-in is disabled for off days.", "Attendance", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var now = DateTime.Now;
        var lateCutoff = row.ShiftStartTime + LateGraceWindow;
        if (now.TimeOfDay > lateCutoff)
        {
            _pendingLateEmployeeId = row.EmployeeId;
            LateJustification = string.Empty;
            DialogTitle = $"Late Clock In - {row.EmployeeName} ({row.ShiftName})";
            IsJustificationDialogOpen = true;
            return;
        }

        var status = now.TimeOfDay < row.ShiftStartTime ? "Early" : "On Time";
        SaveClockIn(row.EmployeeId, status, string.Empty, now);
    }

    private void SaveLateClockIn()
    {
        if (_pendingLateEmployeeId <= 0)
            return;

        if (string.IsNullOrWhiteSpace(LateJustification))
        {
            MessageBox.Show("Please provide a justification for late clock-in.", "Attendance", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveClockIn(_pendingLateEmployeeId, "Late", LateJustification.Trim(), DateTime.Now);
        CloseLateDialog();
    }

    private void SaveClockIn(int employeeId, string status, string justification, DateTime timestamp)
    {
        using var db = new AppDbContext();
        var today = DateTime.Today;
        var attendance = db.EmployeeAttendances.FirstOrDefault(a => a.EmployeeId == employeeId && a.WorkDate.Date == today);
        if (attendance is null)
        {
            attendance = new EmployeeAttendance
            {
                EmployeeId = employeeId,
                WorkDate = today
            };
            db.EmployeeAttendances.Add(attendance);
        }

        attendance.ClockInTime = timestamp;
        attendance.ClockInStatus = status;
        attendance.Justification = justification;
        attendance.IsAbsence = false;
        attendance.AbsenceJustification = string.Empty;
        db.SaveChanges();

        MessageBox.Show($"{status} clock-in saved.", "Attendance", MessageBoxButton.OK, MessageBoxImage.Information);
        LoadAttendance();
    }

    private void ClockOut(object? parameter)
    {
        if (parameter is not AttendanceRowViewModel row)
            return;

        if (row.WorkDate.Date != DateTime.Today)
        {
            MessageBox.Show("Clock out can only be recorded for today.", "Attendance", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.IsScheduledOff)
        {
            MessageBox.Show("This employee is off shift today. Clock-out is not expected.", "Attendance", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        using var db = new AppDbContext();
        var today = DateTime.Today;
        var attendance = db.EmployeeAttendances.FirstOrDefault(a => a.EmployeeId == row.EmployeeId && a.WorkDate.Date == today);
        if (attendance is null || attendance.ClockInTime is null)
        {
            MessageBox.Show("Employee must clock in before clocking out.", "Attendance", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (attendance.ClockOutTime is not null)
        {
            MessageBox.Show("Employee is already clocked out for today.", "Attendance", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (DateTime.Now.TimeOfDay < row.ShiftStartTime)
        {
            MessageBox.Show(
                $"Clock-out is not allowed before the shift starts ({row.ShiftStartTime:hh\\:mm}).",
                "Attendance",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (DateTime.Now.TimeOfDay < row.ShiftEndTime)
        {
            var earlyClockOut = MessageBox.Show(
                $"Current time is before scheduled shift end ({row.ShiftEndTime:hh\\:mm}). Record an early clock-out anyway?",
                "Early Clock-Out",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (earlyClockOut != MessageBoxResult.Yes)
                return;
        }

        attendance.ClockOutTime = DateTime.Now;

        var employee = db.Employees.SingleOrDefault(e => e.Id == row.EmployeeId);
        if (employee is not null && attendance.ClockInTime is not null && attendance.ClockOutTime is not null)
        {
            var workedDuration = attendance.ClockOutTime.Value - attendance.ClockInTime.Value;
            var workedHours = Math.Max(0m, (decimal)workedDuration.TotalHours);
            var pendingSalaryAmount = Math.Round(employee.HourlyRate * workedHours, 2);
            var pendingSalaryReference = BuildPendingSalaryReference(employee, today);

            var existingPendingSalaryEntry = db.Transactions.FirstOrDefault(t =>
                t.Type == "Expense" &&
                t.Category == "Salary" &&
                t.Justification == pendingSalaryReference);

            if (existingPendingSalaryEntry is null)
            {
                db.Transactions.Add(new MoneyTransaction
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
                });
            }
            else
            {
                existingPendingSalaryEntry.Amount = pendingSalaryAmount;
                existingPendingSalaryEntry.AmountUsd = pendingSalaryAmount;
                existingPendingSalaryEntry.AmountFc = CurrencyHelper.ConvertUsdToFc(pendingSalaryAmount);
                existingPendingSalaryEntry.Date = DateTime.Now;
                existingPendingSalaryEntry.CurrencyCode = CurrencyHelper.Usd;
                existingPendingSalaryEntry.ExchangeRateUsed = CurrencyHelper.FcPerUsd;
            }
        }

        db.SaveChanges();

        MessageBox.Show("Clock-out saved. Pending salary was updated in Money.", "Attendance", MessageBoxButton.OK, MessageBoxImage.Information);
        LoadAttendance();
    }

    private void CloseLateDialog()
    {
        _pendingLateEmployeeId = 0;
        IsJustificationDialogOpen = false;
        LateJustification = string.Empty;
    }

    private AttendanceRowViewModel BuildAttendanceRow(Employee employee, EmployeeAttendance? attendance, DateTime day, bool isCurrentDay, decimal pendingSalary, bool dayValidated)
    {
        var shiftDefinition = AttendanceScheduleHelper.ResolveShiftWindow(employee, day);
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
}
