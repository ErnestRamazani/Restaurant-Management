using System;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.Localization;

namespace EliteRestaurantPro.ViewModels;

public abstract class AdminBaseViewModel : LocalizableViewModel
{
    private string _readyPickupBannerText = string.Empty;
    private readonly ImageSource? _sidebarAvatarImage;
    private ImageSource? _businessLogoImage;
    private string _businessName = "EliteResto";
    private string _businessTagline = "PRO";

    protected readonly Action<BaseViewModel> NavigateAction;

    public bool IsServerTabletSession => AppSession.IsServerTablet;

    public bool IsCashierTabletSession => AppSession.IsCashierTablet;

    public bool IsKitchenBarTabletSession => AppSession.IsKitchenBarTablet;

    /// <summary>Hide dashboard, HR, inventory, money, etc. — staff tablets use operational pages only.</summary>
    public bool ShowFullAdminNav => !AppSession.IsStaffTablet;

    /// <summary>Admin order management + cashier register (not server or kitchen tablet).</summary>
    public bool ShowStaffOrdersNav =>
        !AppSession.IsServerTablet && !AppSession.IsKitchenBarTablet;

    /// <summary>Tables and take order: admin, server, cashier.</summary>
    public bool ShowTablesAndTakeOrderNav =>
        ShowFullAdminNav || AppSession.IsServerTablet || AppSession.IsCashierTablet;

    /// <summary>Reservations: admin + cashier.</summary>
    public bool ShowReservationsNav =>
        ShowFullAdminNav || AppSession.IsCashierTablet;

    /// <summary>Client accounts: admin, server, and cashier.</summary>
    public bool ShowClientsNav =>
        ShowFullAdminNav || AppSession.IsServerTablet || AppSession.IsCashierTablet;

    /// <summary>Inventory in sidebar: admin + kitchen/bar tablet.</summary>
    public bool ShowInventorySidebarNav =>
        ShowFullAdminNav || AppSession.IsKitchenBarTablet;

    /// <summary>Kitchen queue (receive / prep / ready).</summary>
    public bool ShowKitchenQueueNav => AppSession.IsKitchenBarTablet;

    /// <summary>Server: confirm Ready → Served for their tickets.</summary>
    public bool ShowServerPickupNav => AppSession.IsServerTablet;

    public string SidebarUserDisplayName =>
        AppSession.IsStaffTablet && !string.IsNullOrWhiteSpace(AppSession.StaffEmployeeName)
            ? AppSession.StaffEmployeeName
            : !string.IsNullOrWhiteSpace(AppSession.AdminLoginDisplayName)
                ? AppSession.AdminLoginDisplayName
                : Loc.Admin("role.admin", "Admin");

    public string SidebarRoleDisplay =>
        AppSession.IsServerTablet ? Loc.Admin("role.server", "Server")
        : AppSession.IsCashierTablet ? Loc.Admin("role.cashier", "Cashier")
        : AppSession.IsKitchenBarTablet ? Loc.Admin("roleKitchenBar", "Kitchen / Bar")
        : Loc.Admin("roleAdministrator", "Administrator");

    public string SidebarCreateOrderLabel =>
        AppSession.IsServerTablet || AppSession.IsCashierTablet
            ? Loc.Admin("navTakeOrder", "Take Order")
            : Loc.Admin("navCreateOrder", "Create Order");

    public string NavSectionLabel => Loc.Admin("navNavigation", "NAVIGATION");
    public string NavDashboardLabel => Loc.Admin("navDashboard", "Dashboard");
    public string NavEmployeesLabel => Loc.Admin("navEmployees", "Employees");
    public string NavMenuLabel => Loc.Admin("navMenu", "Menu");
    public string NavInventoryLabel => Loc.Admin("navInventory", "Inventory");
    public string NavAttendanceLabel => Loc.Admin("navAttendance", "Attendance");
    public string NavKitchenQueueLabel => Loc.Admin("navKitchenQueue", "Kitchen queue");
    public string NavTablesLabel => Loc.Admin("navTables", "Tables");
    public string NavClientsLabel => Loc.Admin("navClients", "Clients");
    public string NavReservationFloorLabel => Loc.Admin("navReservationFloor", "Reservations");
    public string NavPickupServeLabel => Loc.Admin("navPickupServe", "Pick up & serve");
    public string NavOrdersLabel => Loc.Admin("navOrders", "Orders");
    public string NavMoneyLabel => Loc.Admin("navMoney", "Money");
    public string NavSalaryLabel => Loc.Admin("navSalary", "Salary");
    public string NavReportsLabel => Loc.Admin("navReports", "Reports");
    public string NavSettingsLabel => Loc.Admin("settings", "Settings");
    public string NavLogoutLabel => Loc.Admin("signOut", "Logout");

    public string ShiftHistoryCloseLabel => Loc.Common("close", "Close");
    public string ShiftHistoryColDate => Loc.Admin("empShiftHistoryColDate", "Date");
    public string ShiftHistoryColShift => Loc.Admin("empShiftHistoryColShift", "Shift");
    public string ShiftHistoryColIn => Loc.Admin("empShiftHistoryColIn", "In");
    public string ShiftHistoryColOut => Loc.Admin("empShiftHistoryColOut", "Out");
    public string ShiftHistoryColStatus => Loc.Admin("empShiftHistoryColStatus", "Status");
    public string ShiftHistoryColJustification => Loc.Admin("empShiftHistoryColJustification", "Justification");
    public string ShiftHistoryColNotes => Loc.Admin("empShiftHistoryColNotes", "Notes");
    public string ShiftHistoryDismissHint => Loc.Admin("empShiftHistoryDismissHint", "Tap the dimmed area or Close to dismiss. Rows are newest first.");

    public string SidebarBusinessName => _businessName;
    public string SidebarBusinessTagline => _businessTagline;
    public ImageSource? SidebarBusinessLogoImage => _businessLogoImage;
    public bool SidebarHasBusinessLogo => _businessLogoImage is not null;

    /// <summary>Orders in Ready status relevant to this tablet (pickup reminder).</summary>
    public string ReadyPickupBannerText => _readyPickupBannerText;

    public bool ReadyPickupBannerVisible => !string.IsNullOrWhiteSpace(_readyPickupBannerText);

    public void RefreshReadyPickupBanner()
    {
        _readyPickupBannerText = StaffOrderAlertsUiLocalizer.GetBannerText();
        OnPropertyChanged(nameof(ReadyPickupBannerText));
        OnPropertyChanged(nameof(ReadyPickupBannerVisible));
    }

    public string SidebarAvatarLetter
    {
        get
        {
            var n = SidebarUserDisplayName?.Trim();
            if (string.IsNullOrEmpty(n)) return "?";
            return char.ToUpperInvariant(n[0]).ToString();
        }
    }

    public ImageSource? SidebarAvatarImage => _sidebarAvatarImage;

    public bool SidebarAvatarHasPhoto => _sidebarAvatarImage is not null;

    /// <summary>Loads a bitmap from a local file path (absolute or relative to the process directory).</summary>
    protected static ImageSource? TryLoadBitmapFromFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        var full = path.Trim();
        try
        {
            if (!Path.IsPathRooted(full))
                full = Path.GetFullPath(full);
            if (!File.Exists(full))
                return null;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(full, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    public ICommand NavigateToDashboardCommand { get; }
    public ICommand NavigateToEmployeesCommand { get; }
    public ICommand NavigateToMenuCommand { get; }
    public ICommand NavigateToInventoryCommand { get; }
    public ICommand NavigateToAttendanceCommand { get; }
    public ICommand NavigateToTablesCommand { get; }
    public ICommand NavigateToReservationsCommand { get; }
    public ICommand NavigateToOrdersCommand { get; }
    public ICommand NavigateToClientsCommand { get; }
    public ICommand NavigateToKitchenQueueCommand { get; }
    public ICommand NavigateToServerPickupCommand { get; }
    public ICommand NavigateToCreateOrderCommand { get; }
    public ICommand NavigateToMoneyCommand { get; }
    public ICommand MapsSalaryCommand { get; }
    public ICommand NavigateToReportsCommand { get; }
    public ICommand NavigateToAppearanceCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand PlaceholderCommand { get; }

    public abstract string ActivePage { get; }

    protected AdminBaseViewModel(Action<BaseViewModel> navigate)
    {
        LoadBusinessProfileSettings();
        _sidebarAvatarImage = TryLoadBitmapFromFilePath(AppSession.StaffEmployeeProfileImagePath)
            ?? TryLoadBitmapFromFilePath(AppSession.AdminLoginProfileImagePath);
        NavigateAction = navigate;
        NavigateToDashboardCommand = new RelayCommand(_ => navigate(new AdminDashboardViewModel(navigate)));
        NavigateToEmployeesCommand = new RelayCommand(_ => navigate(new EmployeesViewModel(navigate)));
        NavigateToMenuCommand = new RelayCommand(_ => navigate(new MenuViewModel(navigate)));
        NavigateToInventoryCommand = new RelayCommand(_ => navigate(new InventoryViewModel(navigate)));
        NavigateToAttendanceCommand = new RelayCommand(_ => navigate(new AttendanceViewModel(navigate)));
        NavigateToTablesCommand = new RelayCommand(_ => navigate(new TablesViewModel(navigate)));
        NavigateToReservationsCommand = new RelayCommand(
            _ => navigate(new ReservationFloorWebViewModel(navigate)),
            _ => ShowReservationsNav);
        NavigateToOrdersCommand = new RelayCommand(_ => navigate(new AdminOrdersViewModel(navigate)));
        NavigateToClientsCommand = new RelayCommand(
            _ => navigate(new ClientsViewModel(navigate)),
            _ => ShowClientsNav);
        NavigateToKitchenQueueCommand = new RelayCommand(_ => navigate(new KitchenOrdersViewModel(navigate)));
        NavigateToServerPickupCommand = new RelayCommand(_ => navigate(new ServerPickupViewModel(navigate)));
        NavigateToCreateOrderCommand = new RelayCommand(_ => navigate(new CreateOrderViewModel(navigate)));
        NavigateToMoneyCommand = new RelayCommand(_ => navigate(new MoneyViewModel(navigate)));
        MapsSalaryCommand = new RelayCommand(_ => navigate(new SalaryViewModel(navigate)));
        NavigateToReportsCommand = new RelayCommand(_ => navigate(new ReportsViewModel(navigate)));
        NavigateToAppearanceCommand = new RelayCommand(
            _ => navigate(new AppearanceSettingsViewModel(navigate)),
            _ => ShowFullAdminNav);
        LogoutCommand = new RelayCommand(_ =>
        {
            AppSession.Clear();
            navigate(new RoleSelectionViewModel(navigate));
        });
        PlaceholderCommand = new RelayCommand(_ => { });
        RefreshReadyPickupBanner();
    }

    protected override void RefreshLocalizedStrings()
    {
        Notify(
            nameof(SidebarRoleDisplay),
            nameof(SidebarCreateOrderLabel),
            nameof(NavSectionLabel),
            nameof(NavDashboardLabel),
            nameof(NavEmployeesLabel),
            nameof(NavMenuLabel),
            nameof(NavInventoryLabel),
            nameof(NavAttendanceLabel),
            nameof(NavKitchenQueueLabel),
            nameof(NavTablesLabel),
            nameof(NavClientsLabel),
            nameof(NavReservationFloorLabel),
            nameof(NavPickupServeLabel),
            nameof(NavOrdersLabel),
            nameof(NavMoneyLabel),
            nameof(NavSalaryLabel),
            nameof(NavReportsLabel),
            nameof(NavSettingsLabel),
            nameof(NavLogoutLabel),
            nameof(ShiftHistoryCloseLabel),
            nameof(ShiftHistoryColDate),
            nameof(ShiftHistoryColShift),
            nameof(ShiftHistoryColIn),
            nameof(ShiftHistoryColOut),
            nameof(ShiftHistoryColStatus),
            nameof(ShiftHistoryColJustification),
            nameof(ShiftHistoryColNotes),
            nameof(ShiftHistoryDismissHint));
    }

    protected void RefreshBusinessProfileBindings()
    {
        LoadBusinessProfileSettings();
        OnPropertyChanged(nameof(SidebarBusinessName));
        OnPropertyChanged(nameof(SidebarBusinessTagline));
        OnPropertyChanged(nameof(SidebarBusinessLogoImage));
        OnPropertyChanged(nameof(SidebarHasBusinessLogo));
    }

    private void LoadBusinessProfileSettings()
    {
        var settings = SettingsManager.Load().BusinessProfile;
        _businessName = string.IsNullOrWhiteSpace(settings.RestaurantName) ? "EliteResto" : settings.RestaurantName.Trim();
        _businessTagline = "PRO";
        _businessLogoImage = TryLoadBitmapFromFilePath(settings.LogoPath);
    }
}
