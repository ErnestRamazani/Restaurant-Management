using System.Windows.Input;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Staff;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurantPro.ViewModels;

public class AdminLoginViewModel : BaseViewModel
{
    private string _adminId = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _hasError;

    private readonly Action<BaseViewModel> _navigate;

    public string AdminId
    {
        get => _adminId;
        set => SetField(ref _adminId, value);
    }

    public string Password
    {
        get => _password;
        set => SetField(ref _password, value);
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

    public AdminLoginViewModel(Action<BaseViewModel> navigate)
    {
        _navigate = navigate;
        LoginCommand = new RelayCommand(_ => ExecuteLogin());
        BackCommand = new RelayCommand(_ => navigate(new RoleSelectionViewModel(navigate)));
    }

    private void ExecuteLogin()
    {
        AppSession.Clear();
        // Demo credentials — any non-empty input proceeds
        if (string.IsNullOrWhiteSpace(AdminId) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter your ID and password.";
            HasError = true;
            return;
        }

        HasError = false;

        var idTrim = AdminId.Trim();
        using (var db = new AppDbContext())
        {
            var adminOrManager = StaffPortalAuthentication
                .QueryActiveAdminPortalCandidates(db.Employees.AsNoTracking(), idTrim)
                .FirstOrDefault();

            if (adminOrManager is not null)
                AppSession.SetAdminLoginProfile(adminOrManager.Name, adminOrManager.ProfileImagePath);
            else
                AppSession.SetAdminLoginProfile(null, null);
        }

        _navigate(new AdminDashboardViewModel(_navigate));
    }
}
