using System.Windows.Input;
using EliteRestaurantPro.Data;
using EliteRestaurantPro.Models;
using EliteRestaurantPro.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurantPro.ViewModels;

public enum StaffPortalKind
{
    Server,
    Cashier,
    KitchenBar
}

public sealed class StaffLoginViewModel : BaseViewModel
{
    private readonly Action<BaseViewModel> _navigate;
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
        StaffPortalKind.KitchenBar => "Receive Waiting tickets, move them to In Kitchen, then mark Ready for pickup. Use Menu and Inventory for reference.",
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
        LoginCommand = new RelayCommand(_ => ExecuteLogin());
        BackCommand = new RelayCommand(_ => navigate(new RoleSelectionViewModel(navigate)));
    }

    private void ExecuteLogin()
    {
        if (string.IsNullOrWhiteSpace(StaffId) || string.IsNullOrWhiteSpace(Pin))
        {
            ErrorMessage = "Enter your sign-in ID and PIN.";
            HasError = true;
            return;
        }

        using var db = new AppDbContext();
        var id = StaffId.Trim();
        var pin = Pin.Trim();
        // EF Core + SQLite cannot translate Trim() or string.Equals(..., StringComparison) to SQL.
        // Active staff sets are small; filter in memory after a narrow DB query.
        var candidates = db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active")
            .AsEnumerable()
            .Where(e => e.PinCode.Trim() == pin)
            .Where(e =>
                (!string.IsNullOrWhiteSpace(e.SignInId) &&
                 e.SignInId.Trim().Equals(id, StringComparison.OrdinalIgnoreCase))
                || e.UniqueId.Trim().Equals(id, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Employee? emp = Kind switch
        {
            StaffPortalKind.Server => candidates.FirstOrDefault(e =>
                e.Role.Equals("Server", StringComparison.OrdinalIgnoreCase)),
            StaffPortalKind.KitchenBar => candidates.FirstOrDefault(e => IsKitchenBarRole(e.Role)),
            _ => candidates.FirstOrDefault(e =>
                e.Role.Equals("Cashier", StringComparison.OrdinalIgnoreCase))
        };

        if (emp is null)
        {
            ErrorMessage = Kind switch
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

        HasError = false;
        if (Kind == StaffPortalKind.Server)
        {
            AppSession.BeginServerSession(emp.Id, emp.Name, emp.ProfileImagePath);
            _navigate(new CreateOrderViewModel(_navigate));
        }
        else if (Kind == StaffPortalKind.KitchenBar)
        {
            AppSession.BeginKitchenBarSession(emp.Id, emp.Name, emp.ProfileImagePath);
            _navigate(new KitchenOrdersViewModel(_navigate));
        }
        else
        {
            AppSession.BeginCashierSession(emp.Id, emp.Name, emp.ProfileImagePath);
            _navigate(new AdminOrdersViewModel(_navigate));
        }
    }

    private static bool IsKitchenBarRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;
        var r = role.Trim();
        return r.Equals("Chef", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Barman", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Bartender", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Sous Chef", StringComparison.OrdinalIgnoreCase);
    }
}
