using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Employees;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Localization;
using EliteRestaurantPro.Services;
using EliteRestaurantPro.Views;
using Microsoft.Win32;

namespace EliteRestaurantPro.ViewModels;

public sealed class LocalizedSelectOption
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    /// <summary>EliteComboBox closed state uses <see cref="object.ToString"/> instead of DisplayMemberPath.</summary>
    public override string ToString() => Label;
}

public class EmployeesViewModel : AdminBaseViewModel
{
    private readonly AdminDataApiClient _data = new();
    private const string PendingSalaryReferencePrefix = "Pending salary accrual:";
    private static readonly string[] RoleCanonical =
        ["Admin", "Manager", "Cashier", "Server", "Chef", "Barman", "Sous Chef", "Front desk", "Other"];
    private static readonly string[] EmploymentCanonical = ["Active", "On Leave", "Inactive"];
    private static readonly string[] ShiftCanonical = ["Off", "Morning Shift", "Night Shift", "Full Day"];
    private int? _editingEmployeeId;
    private bool _isDialogOpen;
    private string _dialogTitle = "Add New Employee";
    private string _employeeName = string.Empty;
    private string _selectedRole = "Admin";
    private string _customRoleTitle = string.Empty;
    private string _pinCode = string.Empty;
    private string _signInId = string.Empty;
    private string _phoneNumber = string.Empty;
    private string _monthlySalaryUsdText = string.Empty;
    private string _staffMealDiscountPercentText = "0";
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

    public string PageTitle => Loc.Admin("empTitle", "Employee Management");
    public string PageSubtitle => Loc.Admin("empSubtitle", "Manage staff access, roles, and tablet PINs (stored hashed; never shown here).");
    public string AddNewEmployeeLabel => Loc.Admin("empAddNew", "Add New Employee");
    public string SearchTooltip => Loc.Admin("empSearchTooltip", "Search by name, role, sign-in ID, system ID, phone, or status");
    public string PinMaskedLabel => Loc.Admin("empPinMasked", "PIN ●●●●");
    public string NotesLabel => Loc.Admin("empNotes", "Notes");
    public string ProfileTitle => Loc.Admin("empProfileTitle", "Employee Profile");
    public string PerformanceTitle => Loc.Admin("empPerformance", "Performance");
    public string AttendanceTodayTitle => Loc.Admin("empAttendanceToday", "Attendance (today)");
    public string WorkScheduleTitle => Loc.Admin("empWorkSchedule", "Work schedule");
    public string AttendanceStatusLabel => Loc.Admin("empStatus", "Status");
    public string EditProfileLabel => Loc.Admin("empEditProfile", "Edit Profile");
    public string ShiftHistoryLabel => Loc.Admin("empShiftHistory", "Shift History");
    public string DeleteLabel => Loc.Admin("empDelete", "Delete");
    public string CancelLabel => Loc.Common("cancel", "Cancel");
    public string BrowseLabel => Loc.Admin("empBrowse", "Browse");

    public string EmpEmployeeNameLabel => Loc.Admin("empFieldEmployeeName", "EMPLOYEE NAME");
    public string EmpRoleLabel => Loc.Admin("empFieldRole", "ROLE");
    public string EmpCustomRoleTitleLabel => Loc.Admin("empFieldCustomRoleTitle", "JOB TITLE");
    public string EmpCustomRoleTitleHint => Loc.Admin("empFieldCustomRoleTitleHint", "e.g. Janitor, Security, Maintenance");
    public string EmpSignInIdLabel => Loc.Admin("empSignInIdLabel", "Sign-in ID");
    public string EmpSignInIdHint => Loc.Admin("empFieldSignInIdHint", "(required for floor + kitchen tablets)");
    public string EmpSignInIdTooltip => Loc.Admin("empFieldSignInIdTooltip", "Short ID for tablet login with PIN (letters, numbers, - or _). Not the long system Unique ID.");
    public string EmpPinCodeLabel => Loc.Admin("empFieldPinCode", "PIN CODE");
    public string EmpPhoneNumberLabel => Loc.Admin("empFieldPhoneNumber", "PHONE NUMBER");
    public string EmpMonthlySalaryLabel => Loc.Admin("empFieldMonthlySalary", "MONTHLY SALARY (USD) — PAYROLL");
    public string EmpMonthlySalaryTooltip => Loc.Admin("empFieldMonthlySalaryTooltip", "Required for payroll. Gross monthly amount in USD (calendar-prorated after join date).");
    public string EmpStaffMealDiscountLabel => Loc.Admin("empFieldStaffMealDiscount", "STAFF MEAL DISCOUNT (%)");
    public string EmpStaffMealDiscountTooltip => Loc.Admin("empFieldStaffMealDiscountTooltip", "Auto-applied when this employee is linked as a client on an order (0–100).");
    public string EmpJoinDateLabel => Loc.Admin("empFieldJoinDate", "JOIN DATE (YYYY-MM-DD)");
    public string EmpEmploymentStatusLabel => Loc.Admin("empFieldEmploymentStatus", "EMPLOYMENT STATUS");
    public string EmpProfileImageLabel => Loc.Admin("empFieldProfileImage", "PROFILE IMAGE (OPTIONAL)");
    public string EmpDialogNotesLabel => Loc.Admin("empFieldNotes", "NOTES");
    public string EmpWeeklyScheduleLabel => Loc.Admin("empFieldWeeklySchedule", "WEEKLY WORK SCHEDULE");
    public string EmpSaveEmployeeLabel => Loc.Admin("empSaveEmployee", "Save Employee");
    public string EmpMondayLabel => Loc.Admin("empDay.monday", "Monday");
    public string EmpTuesdayLabel => Loc.Admin("empDay.tuesday", "Tuesday");
    public string EmpWednesdayLabel => Loc.Admin("empDay.wednesday", "Wednesday");
    public string EmpThursdayLabel => Loc.Admin("empDay.thursday", "Thursday");
    public string EmpFridayLabel => Loc.Admin("empDay.friday", "Friday");
    public string EmpSaturdayLabel => Loc.Admin("empDay.saturday", "Saturday");
    public string EmpSundayLabel => Loc.Admin("empDay.sunday", "Sunday");

    public string SystemIdLabel => Loc.Admin("empSystemId", "System ID");

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
    public ObservableCollection<LocalizedSelectOption> RoleOptions { get; } = new();
    public ObservableCollection<LocalizedSelectOption> EmploymentStatusOptions { get; } = new();
    public ObservableCollection<LocalizedSelectOption> ShiftOptions { get; } = new();

    private LocalizedSelectOption? _selectedRoleOption;
    private LocalizedSelectOption? _selectedEmploymentStatusOption;
    private LocalizedSelectOption? _selectedMondayShiftOption;
    private LocalizedSelectOption? _selectedTuesdayShiftOption;
    private LocalizedSelectOption? _selectedWednesdayShiftOption;
    private LocalizedSelectOption? _selectedThursdayShiftOption;
    private LocalizedSelectOption? _selectedFridayShiftOption;
    private LocalizedSelectOption? _selectedSaturdayShiftOption;
    private LocalizedSelectOption? _selectedSundayShiftOption;

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

    public LocalizedSelectOption? SelectedRoleOption
    {
        get => _selectedRoleOption;
        set
        {
            if (!SetField(ref _selectedRoleOption, value) || value is null)
                return;
            _selectedRole = value.Value;
            OnPropertyChanged(nameof(SelectedRole));
            OnPropertyChanged(nameof(IsOtherRoleSelected));
            OnPropertyChanged(nameof(IsPortalLoginVisible));
        }
    }

    public string CustomRoleTitle
    {
        get => _customRoleTitle;
        set => SetField(ref _customRoleTitle, value);
    }

    public bool IsOtherRoleSelected =>
        EmployeeRoleHelper.IsOtherRole(SelectedRole);

    public bool IsPortalLoginVisible =>
        EmployeeRoleHelper.AllowsPortalCredentials(SelectedRole);

    public string SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (!SetField(ref _selectedRole, value))
                return;
            SyncSelectOption(ref _selectedRoleOption, RoleOptions, value, nameof(SelectedRoleOption));
            OnPropertyChanged(nameof(IsOtherRoleSelected));
            OnPropertyChanged(nameof(IsPortalLoginVisible));
        }
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
            ? Loc.Admin("empPinHelpAdd", "Tablet login PIN — required for roles that sign in on tablets.")
            : PinStoredOnAccount
                ? Loc.Admin("empPinHelpEditSet", "PIN is set on this account (stored securely). The field is intentionally blank — type a new PIN only to change it.")
                : Loc.Admin("empPinHelpEditUnset", "No PIN on file yet. Enter one if this role requires tablet login.");

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

    public string StaffMealDiscountPercentText
    {
        get => _staffMealDiscountPercentText;
        set => SetField(ref _staffMealDiscountPercentText, value);
    }

    public string JoinDateText
    {
        get => _joinDateText;
        set => SetField(ref _joinDateText, value);
    }

    public LocalizedSelectOption? SelectedEmploymentStatusOption
    {
        get => _selectedEmploymentStatusOption;
        set
        {
            if (!SetField(ref _selectedEmploymentStatusOption, value) || value is null)
                return;
            _selectedEmploymentStatus = value.Value;
            OnPropertyChanged(nameof(SelectedEmploymentStatus));
        }
    }

    public string SelectedEmploymentStatus
    {
        get => _selectedEmploymentStatus;
        set
        {
            if (!SetField(ref _selectedEmploymentStatus, value))
                return;
            SyncSelectOption(ref _selectedEmploymentStatusOption, EmploymentStatusOptions, value, nameof(SelectedEmploymentStatusOption));
        }
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

    public LocalizedSelectOption? SelectedMondayShiftOption
    {
        get => _selectedMondayShiftOption;
        set => SetShiftOption(ref _selectedMondayShiftOption, value, ref _mondayShift, nameof(MondayShift), nameof(SelectedMondayShiftOption));
    }

    public string MondayShift
    {
        get => _mondayShift;
        set => SetShiftCanonical(ref _mondayShift, value, ref _selectedMondayShiftOption, nameof(SelectedMondayShiftOption));
    }

    public LocalizedSelectOption? SelectedTuesdayShiftOption
    {
        get => _selectedTuesdayShiftOption;
        set => SetShiftOption(ref _selectedTuesdayShiftOption, value, ref _tuesdayShift, nameof(TuesdayShift), nameof(SelectedTuesdayShiftOption));
    }

    public string TuesdayShift
    {
        get => _tuesdayShift;
        set => SetShiftCanonical(ref _tuesdayShift, value, ref _selectedTuesdayShiftOption, nameof(SelectedTuesdayShiftOption));
    }

    public LocalizedSelectOption? SelectedWednesdayShiftOption
    {
        get => _selectedWednesdayShiftOption;
        set => SetShiftOption(ref _selectedWednesdayShiftOption, value, ref _wednesdayShift, nameof(WednesdayShift), nameof(SelectedWednesdayShiftOption));
    }

    public string WednesdayShift
    {
        get => _wednesdayShift;
        set => SetShiftCanonical(ref _wednesdayShift, value, ref _selectedWednesdayShiftOption, nameof(SelectedWednesdayShiftOption));
    }

    public LocalizedSelectOption? SelectedThursdayShiftOption
    {
        get => _selectedThursdayShiftOption;
        set => SetShiftOption(ref _selectedThursdayShiftOption, value, ref _thursdayShift, nameof(ThursdayShift), nameof(SelectedThursdayShiftOption));
    }

    public string ThursdayShift
    {
        get => _thursdayShift;
        set => SetShiftCanonical(ref _thursdayShift, value, ref _selectedThursdayShiftOption, nameof(SelectedThursdayShiftOption));
    }

    public LocalizedSelectOption? SelectedFridayShiftOption
    {
        get => _selectedFridayShiftOption;
        set => SetShiftOption(ref _selectedFridayShiftOption, value, ref _fridayShift, nameof(FridayShift), nameof(SelectedFridayShiftOption));
    }

    public string FridayShift
    {
        get => _fridayShift;
        set => SetShiftCanonical(ref _fridayShift, value, ref _selectedFridayShiftOption, nameof(SelectedFridayShiftOption));
    }

    public LocalizedSelectOption? SelectedSaturdayShiftOption
    {
        get => _selectedSaturdayShiftOption;
        set => SetShiftOption(ref _selectedSaturdayShiftOption, value, ref _saturdayShift, nameof(SaturdayShift), nameof(SelectedSaturdayShiftOption));
    }

    public string SaturdayShift
    {
        get => _saturdayShift;
        set => SetShiftCanonical(ref _saturdayShift, value, ref _selectedSaturdayShiftOption, nameof(SelectedSaturdayShiftOption));
    }

    public LocalizedSelectOption? SelectedSundayShiftOption
    {
        get => _selectedSundayShiftOption;
        set => SetShiftOption(ref _selectedSundayShiftOption, value, ref _sundayShift, nameof(SundayShift), nameof(SelectedSundayShiftOption));
    }

    public string SundayShift
    {
        get => _sundayShift;
        set => SetShiftCanonical(ref _sundayShift, value, ref _selectedSundayShiftOption, nameof(SelectedSundayShiftOption));
    }

    public string SelectedImageFileName =>
        string.IsNullOrWhiteSpace(ProfileImagePath)
            ? Loc.Admin("empNoImageSelected", "No image selected")
            : Path.GetFileName(ProfileImagePath);

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

        RebuildLocalizedSelectLists();
        _ = LoadEmployeesAsync();
    }

    private void SetShiftOption(
        ref LocalizedSelectOption? field,
        LocalizedSelectOption? value,
        ref string canonical,
        string canonicalPropertyName,
        string optionPropertyName)
    {
        if (ReferenceEquals(field, value))
            return;
        field = value;
        OnPropertyChanged(optionPropertyName);
        if (value is null)
            return;
        canonical = value.Value;
        OnPropertyChanged(canonicalPropertyName);
    }

    private void SetShiftCanonical(
        ref string canonical,
        string value,
        ref LocalizedSelectOption? field,
        string optionPropertyName)
    {
        if (!SetField(ref canonical, value))
            return;
        SyncSelectOption(ref field, ShiftOptions, value, optionPropertyName);
    }

    private void RebuildLocalizedSelectLists()
    {
        RebuildOptionList(RoleOptions, RoleCanonical, AdminTextLocalizer.TranslateRole);
        RebuildOptionList(EmploymentStatusOptions, EmploymentCanonical, AdminTextLocalizer.TranslateEmploymentStatus);
        RebuildOptionList(ShiftOptions, ShiftCanonical, AdminTextLocalizer.TranslateShift);

        SyncSelectOption(ref _selectedRoleOption, RoleOptions, SelectedRole, nameof(SelectedRoleOption));
        SyncSelectOption(ref _selectedEmploymentStatusOption, EmploymentStatusOptions, SelectedEmploymentStatus, nameof(SelectedEmploymentStatusOption));
        SyncSelectOption(ref _selectedMondayShiftOption, ShiftOptions, MondayShift, nameof(SelectedMondayShiftOption));
        SyncSelectOption(ref _selectedTuesdayShiftOption, ShiftOptions, TuesdayShift, nameof(SelectedTuesdayShiftOption));
        SyncSelectOption(ref _selectedWednesdayShiftOption, ShiftOptions, WednesdayShift, nameof(SelectedWednesdayShiftOption));
        SyncSelectOption(ref _selectedThursdayShiftOption, ShiftOptions, ThursdayShift, nameof(SelectedThursdayShiftOption));
        SyncSelectOption(ref _selectedFridayShiftOption, ShiftOptions, FridayShift, nameof(SelectedFridayShiftOption));
        SyncSelectOption(ref _selectedSaturdayShiftOption, ShiftOptions, SaturdayShift, nameof(SelectedSaturdayShiftOption));
        SyncSelectOption(ref _selectedSundayShiftOption, ShiftOptions, SundayShift, nameof(SelectedSundayShiftOption));
    }

    private static void RebuildOptionList(
        ObservableCollection<LocalizedSelectOption> target,
        IReadOnlyList<string> canonicalValues,
        Func<string?, string> translate)
    {
        target.Clear();
        foreach (var value in canonicalValues)
            target.Add(new LocalizedSelectOption { Value = value, Label = translate(value) });
    }

    private void SyncSelectOption(
        ref LocalizedSelectOption? field,
        IEnumerable<LocalizedSelectOption> options,
        string canonical,
        string propertyName)
    {
        var match = options.FirstOrDefault(o => o.Value.Equals(canonical, StringComparison.OrdinalIgnoreCase))
                    ?? options.FirstOrDefault();
        if (ReferenceEquals(field, match))
            return;
        field = match;
        OnPropertyChanged(propertyName);
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
                EmployeeUiLocalizer.Apply(employee);
                _allEmployees.Add(employee);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                Loc.Admin("empLoadFailed", "Could not load employees"),
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
               || Hit(e.CustomRoleTitle)
               || Hit(e.SignInId)
               || Hit(e.EmploymentStatus)
               || Hit(e.UniqueId)
               || Hit(e.PhoneNumber);
    }

    private void OpenAddDialog()
    {
        _editingEmployeeId = null;
        DialogTitle = Loc.Admin("empAddDialog", "Add New Employee");
        EmployeeName = string.Empty;
        SelectedRole = RoleCanonical[0];
        PinCode = string.Empty;
        SignInId = string.Empty;
        PhoneNumber = string.Empty;
        MonthlySalaryUsdText = string.Empty;
        JoinDateText = DateTime.Today.ToString("yyyy-MM-dd");
        SelectedEmploymentStatus = EmploymentCanonical[0];
        ProfileImagePath = string.Empty;
        EmployeeNotes = string.Empty;
        MondayShift = "Off";
        TuesdayShift = "Off";
        WednesdayShift = "Off";
        ThursdayShift = "Off";
        FridayShift = "Off";
        SaturdayShift = "Off";
        SundayShift = "Off";
        CustomRoleTitle = string.Empty;
        PinStoredOnAccount = false;
        IsDialogOpen = true;
    }

    private void OpenEditDialog(Employee? employee)
    {
        if (employee is null) return;

        _editingEmployeeId = employee.Id;
        DialogTitle = Loc.Admin("empEditDialog", "Edit Employee");
        EmployeeName = employee.Name;
        SelectedRole = employee.Role;
        CustomRoleTitle = employee.CustomRoleTitle ?? string.Empty;
        PinCode = string.Empty;
        SignInId = employee.SignInId;
        PhoneNumber = employee.PhoneNumber;
        MonthlySalaryUsdText = employee.MonthlySalaryUSD.ToString("0.##", CultureInfo.InvariantCulture);
        StaffMealDiscountPercentText = employee.StaffMealDiscountPercent.ToString("0.##", CultureInfo.InvariantCulture);
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
        var normalizedCustomRoleTitle = CustomRoleTitle.Trim();
        var mondayShift = MondayShift.Trim();
        var tuesdayShift = TuesdayShift.Trim();
        var wednesdayShift = WednesdayShift.Trim();
        var thursdayShift = ThursdayShift.Trim();
        var fridayShift = FridayShift.Trim();
        var saturdayShift = SaturdayShift.Trim();
        var sundayShift = SundayShift.Trim();

        var isOtherRole = EmployeeRoleHelper.IsOtherRole(normalizedRole);
        var pinRequired = !_editingEmployeeId.HasValue && !isOtherRole;
        if (string.IsNullOrWhiteSpace(normalizedName) ||
            string.IsNullOrWhiteSpace(normalizedRole) ||
            (pinRequired && string.IsNullOrWhiteSpace(normalizedPin)) ||
            !DateTime.TryParse(JoinDateText, out var joinDate) ||
            string.IsNullOrWhiteSpace(normalizedStatus))
        {
            MessageBox.Show(
                pinRequired
                    ? Loc.Admin("empValidationRequiredWithPin", "Name, role, PIN, join date, and status are required.")
                    : Loc.Admin("empValidationRequired", "Name, role, join date, and status are required. Enter a new PIN only if you want to change it."),
                Loc.Admin("empValidationTitle", "Validation"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (isOtherRole && string.IsNullOrWhiteSpace(normalizedCustomRoleTitle))
        {
            MessageBox.Show(
                Loc.Admin("empValidationCustomRoleTitle", "Enter a job title for the Other role (e.g. Janitor, Security)."),
                Loc.Admin("empValidationTitle", "Validation"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!decimal.TryParse(MonthlySalaryUsdText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var monthlySalaryUsd))
            monthlySalaryUsd = 0m;
        monthlySalaryUsd = Math.Round(Math.Max(0m, monthlySalaryUsd), 2);

        if (!decimal.TryParse(StaffMealDiscountPercentText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var staffDiscountPct))
            staffDiscountPct = 0m;
        staffDiscountPct = Math.Clamp(Math.Round(staffDiscountPct, 2), 0m, 100m);

        if (monthlySalaryUsd <= 0m)
        {
            MessageBox.Show(
                "Enter a positive monthly salary (USD) — required for payroll.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var isStaffPortalRole = EmployeeRoleHelper.RequiresTabletPortalSignInId(normalizedRole);
        if (isStaffPortalRole && string.IsNullOrWhiteSpace(normalizedSignIn))
        {
            MessageBox.Show(
                "Server, Cashier, Front desk, Chef, Barman, and Sous Chef need a Sign-in ID for tablet login (letters/numbers; easy to type).",
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

        if (_editingEmployeeId is int editingId && !isOtherRole && string.IsNullOrWhiteSpace(normalizedPin))
        {
            var existingForPin = allEmployees.FirstOrDefault(e => e.Id == editingId);
            if (existingForPin is not null && string.IsNullOrWhiteSpace(existingForPin.PinCode))
            {
                MessageBox.Show(
                    Loc.Admin("empValidationPinRequiredForRole", "Enter a PIN for this role. Staff in the Other role have no sign-in PIN until you assign a portal or admin role."),
                    Loc.Admin("empValidationTitle", "Validation"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
        }

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
                    CustomRoleTitle = isOtherRole ? normalizedCustomRoleTitle : null,
                    PinCode = isOtherRole
                        ? string.Empty
                        : !string.IsNullOrWhiteSpace(normalizedPin)
                            ? EmployeePinHasher.HashForStorage(normalizedPin)
                            : shell.PinCode,
                    SignInId = EmployeeRoleHelper.ResolveSignInIdForSave(isOtherRole, normalizedSignIn, shell.SignInId),
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
                    SundayShift = sundayShift,
                    StaffMealDiscountPercent = staffDiscountPct
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
                    SignInId = EmployeeRoleHelper.ResolveSignInIdForSave(isOtherRole, normalizedSignIn, null),
                    Name = normalizedName,
                    Role = normalizedRole,
                    CustomRoleTitle = isOtherRole ? normalizedCustomRoleTitle : null,
                    PinCode = isOtherRole
                        ? string.Empty
                        : EmployeePinHasher.HashForStorage(normalizedPin),
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
                    SundayShift = sundayShift,
                    StaffMealDiscountPercent = staffDiscountPct
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

        if (employee.Role.Equals("AdminWeb", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                Loc.Admin("empDeleteAdminWebBlocked", "The read-only admin web account cannot be deleted here. Change it in Appearance settings."),
                Loc.Admin("empDeleteTitle", "Delete employee"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var owner = Application.Current.MainWindow;
        var passcode = EmployeeDeletePasscodeDialog.Prompt(
            owner,
            Loc.Admin("empDeletePasscodeTitle", "Employee delete passcode"),
            Loc.Admin("empDeletePasscodeBody", "Enter the employee delete passcode to remove {{name}}.", new Dictionary<string, string> { ["name"] = employee.Name }),
            Loc.Admin("empDeleteConfirm", "Delete employee"),
            Loc.Common("back", "Back"),
            Loc.Admin("empDeletePasscodeEmpty", "Enter the employee delete passcode."));

        if (passcode is null)
            return;

        var configuredPasscode = SettingsManager.Load().BusinessProfile.EmployeeDeletePasscode.Trim();
        if (string.IsNullOrEmpty(configuredPasscode))
        {
            MessageBox.Show(
                Loc.Admin("empDeletePasscodeNotConfigured", "Employee delete passcode is not configured. Set it in Appearance → Business profile, then push to cloud."),
                Loc.Admin("empDeleteTitle", "Delete employee"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!string.Equals(passcode.Trim(), configuredPasscode, StringComparison.Ordinal))
        {
            MessageBox.Show(
                Loc.Admin("empDeletePasscodeWrong", "Incorrect employee delete passcode."),
                Loc.Admin("empDeleteTitle", "Delete employee"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        string? confirmSignInId = null;
        string? confirmPin = null;

        if (EmployeeDeleteVerification.IsAdminDesktopRole(employee.Role))
        {
            var isAdmin = employee.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
            var warning = isAdmin
                ? Loc.Admin("empDeleteAdminWarning", "Deleting this Admin account may lock you out of Elite Pro if no other Admin or Manager remains. This cannot be undone.")
                : Loc.Admin("empDeleteManagerWarning", "Deleting this Manager removes Elite Pro admin access for this person. This cannot be undone.");

            if (!EmployeeDeleteAdminWarningDialog.Confirm(
                    owner,
                    Loc.Admin("empDeleteTitle", "Delete employee"),
                    warning,
                    Loc.Admin("empDeleteAnyway", "Delete anyway"),
                    Loc.Common("back", "Back")))
            {
                return;
            }

            while (true)
            {
                (confirmSignInId, confirmPin) = EmployeeDeleteCredentialsDialog.Prompt(
                    owner,
                    Loc.Admin("empDeleteCredentialsTitle", "Confirm admin credentials"),
                    Loc.Admin("empDeleteCredentialsBody", "Enter the sign-in ID and PIN for {{name}} to confirm deletion.", new Dictionary<string, string> { ["name"] = employee.Name }),
                    Loc.Admin("proAdminIdLabel", "ADMIN ID"),
                    Loc.Admin("proPasswordLabel", "PASSWORD"),
                    Loc.Admin("empDeleteConfirm", "Delete employee"),
                    Loc.Common("back", "Back"),
                    Loc.Admin("empDeleteCredentialsEmpty", "Enter sign-in ID and PIN."));

                if (confirmSignInId is null || confirmPin is null)
                    return;

                if (EmployeeDeleteVerification.CredentialsMatchEmployee(employee, confirmSignInId, confirmPin))
                    break;

                MessageBox.Show(
                    Loc.Admin("empDeleteCredentialsWrong", "Sign-in ID or PIN does not match this employee."),
                    Loc.Admin("empDeleteTitle", "Delete employee"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        try
        {
            DesktopCloudPersistence.PushEmployeeDeleteBlocking(
                employee,
                passcode,
                confirmSignInId,
                confirmPin);
            _ = LoadEmployeesAsync();
        }
        catch (Exception ex)
        {
            var message = ex.GetBaseException().Message ?? string.Empty;
            if (message.Contains("employee delete passcode", StringComparison.OrdinalIgnoreCase)
                || message.Contains("passcode is not configured", StringComparison.OrdinalIgnoreCase))
            {
                message = Loc.Admin(
                    "empDeletePasscodeCloudMismatch",
                    "Delete was rejected by the cloud API. Save Appearance → Business profile (employee delete passcode) and push to cloud, then try again.");
            }

            MessageBox.Show(
                message,
                Loc.Admin("empDeleteFailed", "Delete employee failed"),
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

        ShiftHistoryTitle = AdminTextLocalizer.FormatShiftHistoryTitle(employee.Name);
        ShiftHistoryBanner = AdminTextLocalizer.ShiftHistoryLoadingText;
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

    protected override void RefreshLocalizedStrings()
    {
        base.RefreshLocalizedStrings();
        RebuildLocalizedSelectLists();
        Notify(
            nameof(PageTitle),
            nameof(PageSubtitle),
            nameof(AddNewEmployeeLabel),
            nameof(SearchTooltip),
            nameof(PinMaskedLabel),
            nameof(NotesLabel),
            nameof(ProfileTitle),
            nameof(PerformanceTitle),
            nameof(AttendanceTodayTitle),
            nameof(WorkScheduleTitle),
            nameof(AttendanceStatusLabel),
            nameof(EditProfileLabel),
            nameof(ShiftHistoryLabel),
            nameof(DeleteLabel),
            nameof(CancelLabel),
            nameof(BrowseLabel),
            nameof(SystemIdLabel),
            nameof(EmpEmployeeNameLabel),
            nameof(EmpRoleLabel),
            nameof(EmpSignInIdLabel),
            nameof(EmpSignInIdHint),
            nameof(EmpSignInIdTooltip),
            nameof(EmpPinCodeLabel),
            nameof(EmpPhoneNumberLabel),
            nameof(EmpMonthlySalaryLabel),
            nameof(EmpMonthlySalaryTooltip),
            nameof(EmpStaffMealDiscountLabel),
            nameof(EmpStaffMealDiscountTooltip),
            nameof(EmpJoinDateLabel),
            nameof(EmpEmploymentStatusLabel),
            nameof(EmpProfileImageLabel),
            nameof(EmpDialogNotesLabel),
            nameof(EmpWeeklyScheduleLabel),
            nameof(EmpSaveEmployeeLabel),
            nameof(EmpMondayLabel),
            nameof(EmpTuesdayLabel),
            nameof(EmpWednesdayLabel),
            nameof(EmpThursdayLabel),
            nameof(EmpFridayLabel),
            nameof(EmpSaturdayLabel),
            nameof(EmpSundayLabel),
            nameof(SelectedImageFileName),
            nameof(PinFieldHelpText));

        foreach (var employee in _allEmployees)
            EmployeeUiLocalizer.Apply(employee);

        if (IsDialogOpen)
            DialogTitle = _editingEmployeeId.HasValue
                ? Loc.Admin("empEditDialog", "Edit Employee")
                : Loc.Admin("empAddDialog", "Add New Employee");

        ApplyEmployeeFilter();
    }

}
