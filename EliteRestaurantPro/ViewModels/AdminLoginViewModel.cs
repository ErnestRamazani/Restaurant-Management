using System.Windows.Input;
using EliteRestaurant.Core.Staff;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Localization;
using EliteRestaurantPro.Services;

namespace EliteRestaurantPro.ViewModels;

public class AdminLoginViewModel : LocalizableViewModel
{
    private string _adminId = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _hasError;

    private readonly Action<BaseViewModel> _navigate;
    private readonly AuthApiClient _authApiClient = new();

    public string PortalTitle => Loc.Admin("proAdminPortal", "Admin Portal");
    public string SignInLead => Loc.Admin("proAdminSignInLead", "Sign in with your administrator credentials");
    public string AdminIdLabel => Loc.Admin("proAdminIdLabel", "ADMIN ID");
    public string AdminIdHint => Loc.Admin("proAdminIdHint", "Sign-in ID, employee code, or your name as shown in Employees.");
    public string PasswordLabel => Loc.Admin("proPasswordLabel", "PASSWORD");
    public string SignInButtonLabel => Loc.Common("signIn", "Sign In");
    public string BackToRolesLabel => Loc.Admin("proBackToRoles", "← Back to Role Selection");

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

    protected override void RefreshLocalizedStrings()
    {
        Notify(
            nameof(PortalTitle),
            nameof(SignInLead),
            nameof(AdminIdLabel),
            nameof(AdminIdHint),
            nameof(PasswordLabel),
            nameof(SignInButtonLabel),
            nameof(BackToRolesLabel));
    }

    private async Task ExecuteLoginAsync()
    {
        AppSession.Clear();
        if (string.IsNullOrWhiteSpace(AdminId) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = Loc.Admin("proEnterIdPassword", "Please enter your ID and password.");
            HasError = true;
            return;
        }

        var auth = await _authApiClient.LoginAsync(AdminId.Trim(), Password.Trim(), "Admin");
        if (auth.Response is null)
        {
            ErrorMessage = !string.IsNullOrWhiteSpace(auth.ErrorMessage)
                ? auth.ErrorMessage
                : Loc.Admin("proSignInFailed", "Sign-in failed. Check your ID and password.");
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
