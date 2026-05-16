using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Services;
using Microsoft.Win32;

namespace EliteRestaurantPro.ViewModels;

public class EmployeesViewModel : AdminBaseViewModel
{
    private readonly AdminDataApiClient _data = new();
    private const string PendingSalaryReferencePrefix = "Pending salary accrual:";
    private int? _editingEmployeeId;
    private bool _isDialogOpen;
    private string _dialogTitle = "Add New Employee";
    private string _employeeName = string.Empty;
    private string _selectedRole = "Admin";
    private string _pinCode = string.Empty;
    private string _signInId = string.Empty;
    private string _phoneNumber = string.Empty;
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
    private bool _pinStoredForEdit;
    private readonly List<Employee> _allEmployees = [];
    private bool _isShiftHistoryOpen;
    private string _shiftHistoryTitle = "Shift history";
    private string _shiftHistorySubtitle = string.Empty;
    private string _shiftHistoryBanner = string.Empty;

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

    /// <summary>True when editing an employee who already has a PIN on file (hash or legacy).</summary>
    public bool PinStoredOnAccount
    {
        get => _pinStoredForEdit;
        private set
        {
            if (!SetField(ref _pinStoredForEdit, value))
                return;
            OnPropertyChanged(nameof(PinFieldHelpText));
        }
    }

    /// <summary>Context-sensitive help next to the PIN field (add vs edit, PIN present or not).</summary>
    public string PinFieldHelpText =>
        !_editingEmployeeId.HasValue
            ? "Tablet login PIN — required for roles that sign in on tablets."
            : PinStoredOnAccount
                ? "PIN is set on this account (stored securely). The field is intentionally blank — type a new PIN only to change it."
                : "No PIN on file yet. Enter one if this role requires tablet login.";

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

    public ICommand OpenAddDialogCommand { get; }
    public ICommand EditEmployeeCommand { get; }
    public ICommand DeleteEmployeeCommand { get; }
    public ICommand SaveEmployeeCommand { get; }
    public ICommand CancelDialogCommand { get; }
    public ICommand BrowseProfileImageCommand { get; }
    public ICommand ShowShiftHistoryCommand { get; }
    public ICommand CloseShiftHistoryCommand { get; }

    public EmployeesViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        OpenAddDialogCommand = new RelayCommand(_ => OpenAddDialog());
        EditEmployeeCommand = new RelayCommand(employee => OpenEditDialog(employee as Employee));
        DeleteEmployeeCommand = new RelayCommand(employee => DeleteEmployee(employee as Employee));
        SaveEmployeeCommand = new RelayCommand(_ => _ = SaveEmployeeAsync());
        CancelDialogCommand = new RelayCommand(_ => CloseDialog());
        BrowseProfileImageCommand = new RelayCommand(_ => BrowseProfileImage());
        ShowShiftHistoryCommand = new RelayCommand(employee => _ = ShowShiftHistoryAsync(employee as Employee));
        CloseShiftHistoryCommand = new RelayCommand(_ => CloseShiftHistory());

        _ = LoadEmployeesAsync();
    }

    private async Task LoadEmployeesAsync()
    {
        await Task.Yield();
        Employees.Clear();
        _allEmployees.Clear();

        try
        {
            var today = DateTime.Today;
            var (todayStartUtc, todayEndExclusiveUtc) = AttendanceCalendar.DayRangeUtc(today);

            var employeesTask = _data.GetEmployeesAsync();
            var attendanceTask = _data.GetAttendanceAsync();
            var moneyTask = _data.GetMoneyTransactionsAsync();
            var ordersTask = _data.GetOrdersAsync();
            var productsTask = _data.GetProductsAsync();
            await Task.WhenAll(employeesTask, attendanceTask, moneyTask, ordersTask, productsTask).ConfigureAwait(true);

            var employeeRows = (await employeesTask.ConfigureAwait(true)).OrderBy(e => e.Name).ToList();
            var attendanceRows = await attendanceTask.ConfigureAwait(true);
            var transactions = await moneyTask.ConfigureAwait(true);
            var orders = (await ordersTask.ConfigureAwait(true)).ToList();
            var priceByProductId = (await productsTask.ConfigureAwait(true)).ToDictionary(p => p.Id, p => p.Price);

            foreach (var o in orders)
            {
                foreach (var i in o.Items)
                {
                    if (i.Product is null && priceByProductId.TryGetValue(i.ProductId, out var price))
                        i.Product = new Product { Id = i.ProductId, Price = price };
                }
            }

            var todayAttendance = attendanceRows
                .Where(a => a.WorkDate >= todayStartUtc && a.WorkDate < todayEndExclusiveUtc)
                .GroupBy(a => a.EmployeeId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

            var todayPendingSalariesByEmployeeId = transactions
                .Where(t =>
                    t.Type == "Expense" &&
                    t.Category == "Salary" &&
                    t.Date.Date == today &&
                    t.Justification.StartsWith(PendingSalaryReferencePrefix))
                .GroupBy(t => ParseEmployeeIdFromPendingSalaryJustification(t.Justification))
                .Where(g => g.Key.HasValue)
                .ToDictionary(g => g.Key!.Value, g => g.Sum(x => x.Amount));

            foreach (var employee in employeeRows)
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

                employee.RebuildScheduleDays();
                _allEmployees.Add(employee);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                "Could not load employees",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
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
        PinStoredOnAccount = false;
        IsDialogOpen = true;
    }

    private void OpenEditDialog(Employee? employee)
    {
        if (employee is null) return;

        _editingEmployeeId = employee.Id;
        DialogTitle = "Edit Employee";
        EmployeeName = employee.Name;
        SelectedRole = employee.Role;
        PinCode = string.Empty;
        SignInId = employee.SignInId;
        PhoneNumber = employee.PhoneNumber;
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
        PinStoredOnAccount = !string.IsNullOrWhiteSpace(employee.PinCode);
        IsDialogOpen = true;
    }

    private async Task SaveEmployeeAsync()
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

        var pinRequired = !_editingEmployeeId.HasValue;
        if (string.IsNullOrWhiteSpace(normalizedName) ||
            string.IsNullOrWhiteSpace(normalizedRole) ||
            (pinRequired && string.IsNullOrWhiteSpace(normalizedPin)) ||
            !DateTime.TryParse(JoinDateText, out var joinDate) ||
            string.IsNullOrWhiteSpace(normalizedStatus))
        {
            MessageBox.Show(
                pinRequired
                    ? "Name, role, PIN, join date, and status are required."
                    : "Name, role, join date, and status are required. Enter a new PIN only if you want to change it.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!decimal.TryParse(MonthlySalaryUsdText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var monthlySalaryUsd))
            monthlySalaryUsd = 0m;
        monthlySalaryUsd = Math.Round(Math.Max(0m, monthlySalaryUsd), 2);

        if (monthlySalaryUsd <= 0m)
        {
            MessageBox.Show(
                "Enter a positive monthly salary (USD) — required for payroll.",
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

        var allEmployees = await _data.GetEmployeesAsync().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(normalizedPin))
        {
            var duplicatePinExists = allEmployees
                .Where(e => !_editingEmployeeId.HasValue || e.Id != _editingEmployeeId.Value)
                .Any(stored => EmployeePinHasher.Verify(normalizedPin, stored.PinCode));

            if (duplicatePinExists)
            {
                MessageBox.Show(
                    "This PIN is already used by another employee. Please enter a unique PIN.",
                    "Duplicate PIN",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        if (normalizedSignIn.Length > 0)
        {
            var signInLower = normalizedSignIn.ToLowerInvariant();
            var others = allEmployees.Where(e => !_editingEmployeeId.HasValue || e.Id != _editingEmployeeId.Value).ToList();

            if (others.Any(e => !string.IsNullOrWhiteSpace(e.SignInId) && e.SignInId.Trim().ToLowerInvariant() == signInLower))
            {
                MessageBox.Show(
                    "This Sign-in ID is already used by another employee.",
                    "Duplicate Sign-in ID",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (others.Any(e => e.UniqueId.Trim().ToLowerInvariant() == signInLower))
            {
                MessageBox.Show(
                    "This Sign-in ID matches another employee's system Unique ID. Choose a different Sign-in ID.",
                    "Sign-in ID conflict",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        try
        {
            if (_editingEmployeeId is int employeeId)
            {
                var shell = allEmployees.FirstOrDefault(e => e.Id == employeeId)
                    ?? throw new InvalidOperationException("Employee not found. Refresh and try again.");

                var toSave = new Employee
                {
                    Id = employeeId,
                    UniqueId = shell.UniqueId,
                    Name = normalizedName,
                    Role = normalizedRole,
                    PinCode = !string.IsNullOrWhiteSpace(normalizedPin)
                        ? EmployeePinHasher.HashForStorage(normalizedPin)
                        : shell.PinCode,
                    SignInId = isStaffPortalRole ? normalizedSignIn : string.Empty,
                    PhoneNumber = normalizedPhone,
                    HourlyRate = 0m,
                    MonthlySalaryUSD = monthlySalaryUsd,
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
                };

                DesktopCloudPersistence.PushUpsertBlocking(toSave);
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

                var newEmployee = new Employee
                {
                    UniqueId = UniqueIdGenerator.NewId("EMP"),
                    SignInId = isStaffPortalRole ? normalizedSignIn : string.Empty,
                    Name = normalizedName,
                    Role = normalizedRole,
                    PinCode = EmployeePinHasher.HashForStorage(normalizedPin),
                    PhoneNumber = normalizedPhone,
                    HourlyRate = 0m,
                    MonthlySalaryUSD = monthlySalaryUsd,
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
                };

                DesktopCloudPersistence.PushUpsertBlocking(newEmployee);
            }

            CloseDialog();
            _ = LoadEmployeesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                "Save employee failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
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

        try
        {
            var toDelete = new Employee { Id = employee.Id, UniqueId = employee.UniqueId };
            DesktopCloudPersistence.PushDeleteBlocking(toDelete);
            _ = LoadEmployeesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                "Delete employee failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
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

    private void CloseShiftHistory()
    {
        IsShiftHistoryOpen = false;
        ShiftHistoryBanner = string.Empty;
        ShiftHistoryRows.Clear();
    }

    private async Task ShowShiftHistoryAsync(Employee? employee)
    {
        if (employee is null)
            return;

        ShiftHistoryTitle = $"Shift history — {employee.Name}";
        ShiftHistoryBanner = "Loading…";
        ShiftHistorySubtitle = string.Empty;
        ShiftHistoryRows.Clear();
        IsShiftHistoryOpen = true;

        try
        {
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
