using System.Windows.Input;
using EliteRestaurant.Core.Staff;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Services;

namespace EliteRestaurantPro.ViewModels;

public class AdminLoginViewModel : BaseViewModel
{
    private string _adminId = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _hasError;

    private readonly Action<BaseViewModel> _navigate;
    private readonly AuthApiClient _authApiClient = new();

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
        LoginCommand = new RelayCommand(async _ => await ExecuteLoginAsync());
        BackCommand = new RelayCommand(_ => navigate(new RoleSelectionViewModel(navigate)));
    }

    private async Task ExecuteLoginAsync()
    {
        AppSession.Clear();
        if (string.IsNullOrWhiteSpace(AdminId) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter your ID and password.";
            HasError = true;
            return;
        }

        var auth = await _authApiClient.LoginAsync(AdminId.Trim(), Password.Trim(), "Admin");
        if (auth.Response is null)
        {
            ErrorMessage = !string.IsNullOrWhiteSpace(auth.ErrorMessage)
                ? auth.ErrorMessage
                : "Sign-in failed. Check your ID and password.";
            HasError = true;
            return;
        }

        if (!StaffPortalAuthentication.IsAdminDesktopRole(auth.Response.Role))
        {
            ErrorMessage = StaffPortalAuthentication.AdminDesktopPortalRejectedMessage(auth.Response.Role);
            HasError = true;
            AppSession.Clear();
            return;
        }

        AppSession.SetAdminLoginProfile(auth.Response.Name, null);
        var settings = SettingsManager.Load();
        CloudConnectionSettings.ApplyRestaurantIdFromAccessToken(settings, auth.Response.AccessToken);
        await CloudConnectionSettings.PullPublicBrandingAsync(settings);
        SettingsManager.Save(settings);
        HasError = false;
        ErrorMessage = string.Empty;
        _navigate(new AdminDashboardViewModel(_navigate));
    }
}
