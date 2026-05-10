using System;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.ViewModels;

public abstract class AdminBaseViewModel : BaseViewModel
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
                : "Admin";

    public string SidebarRoleDisplay =>
        AppSession.IsServerTablet ? "Server"
        : AppSession.IsCashierTablet ? "Cashier"
        : AppSession.IsKitchenBarTablet ? "Kitchen / Bar"
        : "Administrator";

    public string SidebarCreateOrderLabel =>
        AppSession.IsServerTablet || AppSession.IsCashierTablet ? "Take Order" : "Create Order";

    public string SidebarBusinessName => _businessName;
    public string SidebarBusinessTagline => _businessTagline;
    public ImageSource? SidebarBusinessLogoImage => _businessLogoImage;
    public bool SidebarHasBusinessLogo => _businessLogoImage is not null;

    /// <summary>Orders in Ready status relevant to this tablet (pickup reminder).</summary>
    public string ReadyPickupBannerText => _readyPickupBannerText;

    public bool ReadyPickupBannerVisible => !string.IsNullOrWhiteSpace(_readyPickupBannerText);

    public void RefreshReadyPickupBanner()
    {
        _readyPickupBannerText = StaffOrderAlerts.GetBannerText();
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

    private static ImageSource? TryLoadProfileImage(string? path)
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
        _sidebarAvatarImage = TryLoadProfileImage(AppSession.StaffEmployeeProfileImagePath)
            ?? TryLoadProfileImage(AppSession.AdminLoginProfileImagePath);
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
        _businessLogoImage = TryLoadProfileImage(settings.LogoPath);
    }
}
