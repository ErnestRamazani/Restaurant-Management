using System.Windows.Input;
using EliteRestaurant.Core.Staff;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Services;

namespace EliteRestaurantPro.ViewModels;

public sealed class StaffLoginViewModel : BaseViewModel
{
    private readonly Action<BaseViewModel> _navigate;
    private readonly AuthApiClient _authApiClient = new();
    private string _staffId = string.Empty;
    private string _pin = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _hasError;

    public StaffPortalKind Kind { get; }

    public string PortalTitle => Kind switch
    {
        StaffPortalKind.Server => "Server tablet",
        StaffPortalKind.KitchenBar => "Kitchen / Bar",
        _ => "Cashier"
    };

    public string Headline => Kind switch
    {
        StaffPortalKind.Server => "Sign in to take orders",
        StaffPortalKind.KitchenBar => "Sign in to kitchen & bar stations",
        _ => "Sign in to run the register & kitchen flow"
    };

    public string Subtitle => Kind switch
    {
        StaffPortalKind.Server => "Orders are sent to the cashier — not the kitchen until validated.",
        StaffPortalKind.KitchenBar =>
            "Receive Waiting tickets, move them to In Kitchen, then mark Ready for pickup. Use Menu and Inventory for reference.",
        _ => "Validate tickets, manage the kitchen queue, complete sales, and print — same tools as admin for orders."
    };

    public string StaffId
    {
        get => _staffId;
        set => SetField(ref _staffId, value);
    }

    public string Pin
    {
        get => _pin;
        set => SetField(ref _pin, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }

    public bool HasError
    {
        get => _hasError;
        set => SetField(ref _hasError, value);
    }

    public ICommand LoginCommand { get; }
    public ICommand BackCommand { get; }

    public StaffLoginViewModel(Action<BaseViewModel> navigate, StaffPortalKind kind)
    {
        _navigate = navigate;
        Kind = kind;
        LoginCommand = new RelayCommand(async _ => await ExecuteLoginAsync());
        BackCommand = new RelayCommand(_ => navigate(new RoleSelectionViewModel(navigate)));
    }

    private async Task ExecuteLoginAsync()
    {
        if (string.IsNullOrWhiteSpace(StaffId) || string.IsNullOrWhiteSpace(Pin))
        {
            ErrorMessage = "Enter your sign-in ID and PIN.";
            HasError = true;
            return;
        }

        var id = StaffId.Trim();
        var pin = Pin.Trim();
        var portal = Kind switch
        {
            StaffPortalKind.Cashier => "Cashier",
            StaffPortalKind.KitchenBar => "KitchenBar",
            _ => "Server"
        };

        EliteRestaurant.Contracts.Auth.CloudAuthResult auth;
        try
        {
            auth = await _authApiClient.LoginAsync(id, pin, portal);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Cloud login failed: {ex.GetBaseException().Message}";
            HasError = true;
            return;
        }

        var login = auth.Response;
        if (login is null)
        {
            ErrorMessage = !string.IsNullOrWhiteSpace(auth.ErrorMessage)
                ? auth.ErrorMessage
                : Kind switch
                {
                    StaffPortalKind.Server =>
                        "No active server matches this sign-in ID and PIN. Set role, Sign-in ID, and PIN in Admin → Employees.",
                    StaffPortalKind.KitchenBar =>
                        "No active Chef / Barman / Sous Chef matches this sign-in ID and PIN. Assign Sign-in ID and PIN in Admin → Employees.",
                    _ =>
                        "No active cashier matches this sign-in ID and PIN. Set role, Sign-in ID, and PIN in Admin → Employees."
                };
            HasError = true;
            return;
        }

        var sessionPortal = string.IsNullOrWhiteSpace(login.Portal)
            ? StaffPortalAuthentication.CanonicalPortalForRole(login.Role)
            : login.Portal.Trim();

        if (!string.Equals(sessionPortal, portal, StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage =
                $"This account is not set up for this workspace. The cloud session is for portal “{sessionPortal}”, but you chose “{portal}”. Use the matching role tile or update the employee’s role in Admin → Employees.";
            HasError = true;
            return;
        }

        HasError = false;
        var settings = SettingsManager.Load();
        CloudConnectionSettings.ApplyRestaurantIdFromAccessToken(settings, login.AccessToken);
        await CloudConnectionSettings.PullPublicBrandingAsync(settings);
        SettingsManager.Save(settings);

        if (Kind == StaffPortalKind.Server)
        {
            AppSession.BeginServerSession(login.EmployeeId, login.Name);
            _navigate(new CreateOrderViewModel(_navigate));
        }
        else if (Kind == StaffPortalKind.KitchenBar)
        {
            AppSession.BeginKitchenBarSession(login.EmployeeId, login.Name);
            _navigate(new KitchenOrdersViewModel(_navigate));
        }
        else
        {
            AppSession.BeginCashierSession(login.EmployeeId, login.Name);
            _navigate(new AdminOrdersViewModel(_navigate));
        }
    }
}
