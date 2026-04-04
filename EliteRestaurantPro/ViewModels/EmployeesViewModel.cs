using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using EliteRestaurantPro.Data;
using EliteRestaurantPro.Models;
using EliteRestaurantPro.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace EliteRestaurantPro.ViewModels;

public class EmployeesViewModel : AdminBaseViewModel
{
    private const string PendingSalaryReferencePrefix = "Pending salary accrual:";
    private int? _editingEmployeeId;
    private bool _isDialogOpen;
    private string _dialogTitle = "Add New Employee";
    private string _employeeName = string.Empty;
    private string _selectedRole = "Admin";
    private string _pinCode = string.Empty;
    private string _signInId = string.Empty;
    private string _phoneNumber = string.Empty;
    private string _hourlyRateText = string.Empty;
    private string _monthlySalaryUsdText = string.Empty;
    private string _joinDateText = string.Empty;
    private string _selectedEmploymentStatus = "Active";
    private string _profileImagePath = string.Empty;
    private string _employeeNotes = string.Empty;
    private string _mondayShift = "Off";
    private string _tuesdayShift = "Off";
    private string _wednesdayShift = "Off";
    private string _thursdayShift = "Off";
    private string _fridayShift = "Off";
    private string _saturdayShift = "Off";
    private string _sundayShift = "Off";
    private string _searchText = string.Empty;
    private readonly List<Employee> _allEmployees = [];

    public override string ActivePage => "Employees";

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value))
                return;
            ApplyEmployeeFilter();
        }
    }

    public ObservableCollection<Employee> Employees { get; } = new();
    public ObservableCollection<string> Roles { get; } =
        new(["Admin", "Manager", "Cashier", "Server", "Chef", "Barman", "Sous Chef"]);
    public ObservableCollection<string> EmploymentStatuses { get; } =
        new(["Active", "On Leave", "Inactive"]);
    public ObservableCollection<string> ShiftOptions { get; } =
        new(["Off", "Morning Shift", "Night Shift"]);

    public bool IsDialogOpen
    {
        get => _isDialogOpen;
        set => SetField(ref _isDialogOpen, value);
    }

    public string DialogTitle
    {
        get => _dialogTitle;
        set => SetField(ref _dialogTitle, value);
    }

    public string EmployeeName
    {
        get => _employeeName;
        set => SetField(ref _employeeName, value);
    }

    public string SelectedRole
    {
        get => _selectedRole;
        set => SetField(ref _selectedRole, value);
    }

    public string PinCode
    {
        get => _pinCode;
        set => SetField(ref _pinCode, value);
    }

    /// <summary>Short ID for server/cashier tablet login (required for those roles).</summary>
    public string SignInId
    {
        get => _signInId;
        set => SetField(ref _signInId, value);
    }

    public string PhoneNumber
    {
        get => _phoneNumber;
        set => SetField(ref _phoneNumber, value);
    }

    public string HourlyRateText
    {
        get => _hourlyRateText;
        set => SetField(ref _hourlyRateText, value);
    }

    public string MonthlySalaryUsdText
    {
        get => _monthlySalaryUsdText;
        set => SetField(ref _monthlySalaryUsdText, value);
    }

    public string JoinDateText
    {
        get => _joinDateText;
        set => SetField(ref _joinDateText, value);
    }

    public string SelectedEmploymentStatus
    {
        get => _selectedEmploymentStatus;
        set => SetField(ref _selectedEmploymentStatus, value);
    }

    public string ProfileImagePath
    {
        get => _profileImagePath;
        set
        {
            if (!SetField(ref _profileImagePath, value))
                return;
            OnPropertyChanged(nameof(SelectedImageFileName));
        }
    }

    public string EmployeeNotes
    {
        get => _employeeNotes;
        set => SetField(ref _employeeNotes, value);
    }

    public string MondayShift
    {
        get => _mondayShift;
        set => SetField(ref _mondayShift, value);
    }

    public string TuesdayShift
    {
        get => _tuesdayShift;
        set => SetField(ref _tuesdayShift, value);
    }

    public string WednesdayShift
    {
        get => _wednesdayShift;
        set => SetField(ref _wednesdayShift, value);
    }

    public string ThursdayShift
    {
        get => _thursdayShift;
        set => SetField(ref _thursdayShift, value);
    }

    public string FridayShift
    {
        get => _fridayShift;
        set => SetField(ref _fridayShift, value);
    }

    public string SaturdayShift
    {
        get => _saturdayShift;
        set => SetField(ref _saturdayShift, value);
    }

    public string SundayShift
    {
        get => _sundayShift;
        set => SetField(ref _sundayShift, value);
    }

    public string SelectedImageFileName =>
        string.IsNullOrWhiteSpace(ProfileImagePath) ? "No image selected" : Path.GetFileName(ProfileImagePath);

    public ICommand OpenAddDialogCommand { get; }
    public ICommand EditEmployeeCommand { get; }
    public ICommand DeleteEmployeeCommand { get; }
    public ICommand SaveEmployeeCommand { get; }
    public ICommand CancelDialogCommand { get; }
    public ICommand BrowseProfileImageCommand { get; }
    public ICommand ShowShiftHistoryCommand { get; }

    public EmployeesViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        OpenAddDialogCommand = new RelayCommand(_ => OpenAddDialog());
        EditEmployeeCommand = new RelayCommand(employee => OpenEditDialog(employee as Employee));
        DeleteEmployeeCommand = new RelayCommand(employee => DeleteEmployee(employee as Employee));
        SaveEmployeeCommand = new RelayCommand(_ => SaveEmployee());
        CancelDialogCommand = new RelayCommand(_ => CloseDialog());
        BrowseProfileImageCommand = new RelayCommand(_ => BrowseProfileImage());
        ShowShiftHistoryCommand = new RelayCommand(employee => ShowShiftHistory(employee as Employee));

        LoadEmployees();
    }

    private void LoadEmployees()
    {
        Employees.Clear();
        _allEmployees.Clear();

        using var db = new AppDbContext();
        var today = DateTime.Today;
        var todayAttendance = db.EmployeeAttendances
            .AsNoTracking()
            .Where(a => a.WorkDate == today)
            .ToDictionary(a => a.EmployeeId, a => a);
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
        var orders = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .ToList();

        foreach (var employee in db.Employees.AsNoTracking().OrderBy(e => e.Name))
        {
            employee.TotalOrdersServed = orders.Count(o => o.ServerId == employee.Id);
            employee.TotalSalesGenerated = orders
                .Where(o => o.ServerId == employee.Id && o.Status != "Cancelled")
                .Sum(o => o.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity));

            if (todayAttendance.TryGetValue(employee.Id, out var attendance))
            {
                var baseClockIn = attendance.ClockInTime?.ToString("HH:mm") ?? "Not clocked in";
                employee.TodayClockInText = string.IsNullOrWhiteSpace(attendance.ClockInStatus)
                    ? baseClockIn
                    : $"{baseClockIn} ({attendance.ClockInStatus})";
                employee.TodayClockOutText = attendance.ClockOutTime?.ToString("HH:mm") ?? "Not clocked out";
                employee.CanClockIn = attendance.ClockInTime is null;
                employee.CanClockOut = attendance.ClockInTime is not null && attendance.ClockOutTime is null;
            }
            else
            {
                employee.TodayClockInText = "Not clocked in";
                employee.TodayClockOutText = "Not clocked out";
                employee.CanClockIn = true;
                employee.CanClockOut = false;
            }

            if (todayPendingSalariesByEmployeeId.TryGetValue(employee.Id, out var pendingSalary))
                employee.PendingSalaryToday = pendingSalary;
            else
                employee.PendingSalaryToday = 0m;

            _allEmployees.Add(employee);
        }

        ApplyEmployeeFilter();
    }

    private void ApplyEmployeeFilter()
    {
        var q = (_searchText ?? string.Empty).Trim();
        Employees.Clear();
        foreach (var e in _allEmployees)
        {
            if (q.Length == 0 || EmployeeMatchesSearch(e, q))
                Employees.Add(e);
        }
    }

    private static bool EmployeeMatchesSearch(Employee e, string q)
    {
        bool Hit(string? s)
            => !string.IsNullOrEmpty(s) && s.Contains(q, StringComparison.OrdinalIgnoreCase);

        return Hit(e.Name)
               || Hit(e.Role)
               || Hit(e.PinCode)
               || Hit(e.SignInId)
               || Hit(e.EmploymentStatus)
               || Hit(e.UniqueId)
               || Hit(e.PhoneNumber);
    }

    private void OpenAddDialog()
    {
        _editingEmployeeId = null;
        DialogTitle = "Add New Employee";
        EmployeeName = string.Empty;
        SelectedRole = Roles.First();
        PinCode = string.Empty;
        SignInId = string.Empty;
        PhoneNumber = string.Empty;
        HourlyRateText = string.Empty;
        MonthlySalaryUsdText = string.Empty;
        JoinDateText = DateTime.Today.ToString("yyyy-MM-dd");
        SelectedEmploymentStatus = EmploymentStatuses.First();
        ProfileImagePath = string.Empty;
        EmployeeNotes = string.Empty;
        MondayShift = "Off";
        TuesdayShift = "Off";
        WednesdayShift = "Off";
        ThursdayShift = "Off";
        FridayShift = "Off";
        SaturdayShift = "Off";
        SundayShift = "Off";
        IsDialogOpen = true;
    }

    private void OpenEditDialog(Employee? employee)
    {
        if (employee is null) return;

        _editingEmployeeId = employee.Id;
        DialogTitle = "Edit Employee";
        EmployeeName = employee.Name;
        SelectedRole = employee.Role;
        PinCode = employee.PinCode;
        SignInId = employee.SignInId;
        PhoneNumber = employee.PhoneNumber;
        HourlyRateText = employee.HourlyRate.ToString("0.##", CultureInfo.InvariantCulture);
        MonthlySalaryUsdText = employee.MonthlySalaryUSD.ToString("0.##", CultureInfo.InvariantCulture);
        JoinDateText = employee.JoinDate.ToString("yyyy-MM-dd");
        SelectedEmploymentStatus = string.IsNullOrWhiteSpace(employee.EmploymentStatus) ? "Active" : employee.EmploymentStatus;
        ProfileImagePath = employee.ProfileImagePath;
        EmployeeNotes = employee.Notes;
        MondayShift = string.IsNullOrWhiteSpace(employee.MondayShift) ? "Off" : employee.MondayShift;
        TuesdayShift = string.IsNullOrWhiteSpace(employee.TuesdayShift) ? "Off" : employee.TuesdayShift;
        WednesdayShift = string.IsNullOrWhiteSpace(employee.WednesdayShift) ? "Off" : employee.WednesdayShift;
        ThursdayShift = string.IsNullOrWhiteSpace(employee.ThursdayShift) ? "Off" : employee.ThursdayShift;
        FridayShift = string.IsNullOrWhiteSpace(employee.FridayShift) ? "Off" : employee.FridayShift;
        SaturdayShift = string.IsNullOrWhiteSpace(employee.SaturdayShift) ? "Off" : employee.SaturdayShift;
        SundayShift = string.IsNullOrWhiteSpace(employee.SundayShift) ? "Off" : employee.SundayShift;
        IsDialogOpen = true;
    }

    private void SaveEmployee()
    {
        var normalizedName = EmployeeName.Trim();
        var normalizedRole = SelectedRole.Trim();
        var normalizedPin = PinCode.Trim();
        var normalizedSignIn = SignInId.Trim();
        var normalizedPhone = PhoneNumber.Trim();
        var normalizedStatus = SelectedEmploymentStatus.Trim();
        var normalizedImage = ProfileImagePath.Trim();
        var normalizedNotes = EmployeeNotes.Trim();
        var mondayShift = MondayShift.Trim();
        var tuesdayShift = TuesdayShift.Trim();
        var wednesdayShift = WednesdayShift.Trim();
        var thursdayShift = ThursdayShift.Trim();
        var fridayShift = FridayShift.Trim();
        var saturdayShift = SaturdayShift.Trim();
        var sundayShift = SundayShift.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName) ||
            string.IsNullOrWhiteSpace(normalizedRole) ||
            string.IsNullOrWhiteSpace(normalizedPin) ||
            !decimal.TryParse(HourlyRateText, NumberStyles.Number, CultureInfo.InvariantCulture, out var hourlyRate) ||
            !DateTime.TryParse(JoinDateText, out var joinDate) ||
            string.IsNullOrWhiteSpace(normalizedStatus))
        {
            MessageBox.Show(
                "Name, role, PIN, hourly rate, join date, and status are required.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var isStaffPortalRole = normalizedRole.Equals("Server", StringComparison.OrdinalIgnoreCase)
                                || normalizedRole.Equals("Cashier", StringComparison.OrdinalIgnoreCase)
                                || normalizedRole.Equals("Chef", StringComparison.OrdinalIgnoreCase)
                                || normalizedRole.Equals("Barman", StringComparison.OrdinalIgnoreCase)
                                || normalizedRole.Equals("Bartender", StringComparison.OrdinalIgnoreCase)
                                || normalizedRole.Equals("Sous Chef", StringComparison.OrdinalIgnoreCase);
        if (isStaffPortalRole && string.IsNullOrWhiteSpace(normalizedSignIn))
        {
            MessageBox.Show(
                "Server, Cashier, Chef, Barman, and Sous Chef need a Sign-in ID for tablet login (letters/numbers; easy to type).",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        const int signInMaxLen = 32;
        if (normalizedSignIn.Length > signInMaxLen)
        {
            MessageBox.Show(
                $"Sign-in ID must be at most {signInMaxLen} characters.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (normalizedSignIn.Length > 0)
        {
            foreach (var c in normalizedSignIn)
            {
                if (!char.IsLetterOrDigit(c) && c is not '_' and not '-')
                {
                    MessageBox.Show(
                        "Sign-in ID may only contain letters, digits, underscores, and hyphens.",
                        "Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
            }
        }

        var monthlySalaryUsd = 0m;
        if (!string.IsNullOrWhiteSpace(MonthlySalaryUsdText) &&
            !decimal.TryParse(MonthlySalaryUsdText, NumberStyles.Number, CultureInfo.InvariantCulture, out monthlySalaryUsd))
        {
            MessageBox.Show(
                "Monthly salary (USD) must be a valid number or left blank for zero.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        using var db = new AppDbContext();
        var duplicatePinExists = db.Employees.Any(e =>
            e.PinCode == normalizedPin &&
            (!_editingEmployeeId.HasValue || e.Id != _editingEmployeeId.Value));

        if (duplicatePinExists)
        {
            MessageBox.Show(
                "This PIN is already used by another employee. Please enter a unique PIN.",
                "Duplicate PIN",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (normalizedSignIn.Length > 0)
        {
            var others = db.Employees.AsEnumerable()
                .Where(e => !_editingEmployeeId.HasValue || e.Id != _editingEmployeeId.Value)
                .ToList();
            var signInComparer = StringComparer.OrdinalIgnoreCase;
            if (others.Any(e => signInComparer.Equals(e.SignInId?.Trim(), normalizedSignIn)))
            {
                MessageBox.Show(
                    "This Sign-in ID is already used by another employee.",
                    "Duplicate Sign-in ID",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (others.Any(e => signInComparer.Equals(e.UniqueId?.Trim(), normalizedSignIn)))
            {
                MessageBox.Show(
                    "This Sign-in ID matches another employee's system Unique ID. Choose a different Sign-in ID.",
                    "Sign-in ID conflict",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        if (_editingEmployeeId is int employeeId)
        {
            var existing = db.Employees.Single(e => e.Id == employeeId);
            existing.Name = normalizedName;
            existing.Role = normalizedRole;
            existing.PinCode = normalizedPin;
            existing.SignInId = isStaffPortalRole ? normalizedSignIn : string.Empty;
            existing.PhoneNumber = normalizedPhone;
            existing.HourlyRate = hourlyRate;
            existing.MonthlySalaryUSD = Math.Round(Math.Max(0m, monthlySalaryUsd), 2);
            existing.JoinDate = joinDate.Date;
            existing.EmploymentStatus = normalizedStatus;
            existing.ProfileImagePath = normalizedImage;
            existing.Notes = normalizedNotes;
            existing.MondayShift = mondayShift;
            existing.TuesdayShift = tuesdayShift;
            existing.WednesdayShift = wednesdayShift;
            existing.ThursdayShift = thursdayShift;
            existing.FridayShift = fridayShift;
            existing.SaturdayShift = saturdayShift;
            existing.SundayShift = sundayShift;
        }
        else
        {
            var confirmAdd = MessageBox.Show(
                "Add this employee?",
                "Confirm Add Employee",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmAdd != MessageBoxResult.Yes)
                return;

            db.Employees.Add(new Employee
            {
                UniqueId = UniqueIdGenerator.NewId("EMP"),
                SignInId = isStaffPortalRole ? normalizedSignIn : string.Empty,
                Name = normalizedName,
                Role = normalizedRole,
                PinCode = normalizedPin,
                PhoneNumber = normalizedPhone,
                HourlyRate = hourlyRate,
                MonthlySalaryUSD = Math.Round(Math.Max(0m, monthlySalaryUsd), 2),
                JoinDate = joinDate.Date,
                EmploymentStatus = normalizedStatus,
                ProfileImagePath = normalizedImage,
                Notes = normalizedNotes,
                MondayShift = mondayShift,
                TuesdayShift = tuesdayShift,
                WednesdayShift = wednesdayShift,
                ThursdayShift = thursdayShift,
                FridayShift = fridayShift,
                SaturdayShift = saturdayShift,
                SundayShift = sundayShift
            });
        }

        db.SaveChanges();
        CloseDialog();
        LoadEmployees();
    }

    private void DeleteEmployee(Employee? employee)
    {
        if (employee is null) return;

        var confirmDelete = MessageBox.Show(
            $"Delete employee '{employee.Name}'?",
            "Confirm Delete Employee",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmDelete != MessageBoxResult.Yes)
            return;

        using var db = new AppDbContext();
        var existing = db.Employees.SingleOrDefault(e => e.Id == employee.Id);
        if (existing is null) return;

        db.Employees.Remove(existing);
        db.SaveChanges();
        LoadEmployees();
    }

    private void CloseDialog()
    {
        IsDialogOpen = false;
        _editingEmployeeId = null;
    }

    private void BrowseProfileImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Employee Photo",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.webp;*.bmp",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            ProfileImagePath = dialog.FileName;
        }
    }

    private void ShowShiftHistory(Employee? employee)
    {
        if (employee is null)
            return;

        using var db = new AppDbContext();
        var fromDate = DateTime.Today.AddDays(-6);
        var history = db.EmployeeAttendances
            .AsNoTracking()
            .Where(a => a.EmployeeId == employee.Id && a.WorkDate.Date >= fromDate && a.WorkDate.Date <= DateTime.Today)
            .OrderByDescending(a => a.WorkDate)
            .ToList();

        if (history.Count == 0)
        {
            MessageBox.Show("No attendance records in the last 7 days.", "Shift History", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var lines = history.Select(a =>
        {
            var inText = a.ClockInTime?.ToString("HH:mm") ?? "--:--";
            var outText = a.ClockOutTime?.ToString("HH:mm") ?? "--:--";
            var statusText = string.IsNullOrWhiteSpace(a.ClockInStatus) ? "Pending" : a.ClockInStatus;
            return $"{a.WorkDate:ddd yyyy-MM-dd} | In {inText} | Out {outText} | {statusText}";
        });

        MessageBox.Show(
            string.Join(Environment.NewLine, lines),
            $"Shift History - {employee.Name}",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static int? ParseEmployeeIdFromPendingSalaryJustification(string justification)
    {
        const string marker = "EMP-ID:";
        var markerIndex = justification.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return null;

        var start = markerIndex + marker.Length;
        var end = justification.IndexOf(' ', start);
        var token = end < 0 ? justification[start..] : justification[start..end];

        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var employeeId)
            ? employeeId
            : null;
    }

}
