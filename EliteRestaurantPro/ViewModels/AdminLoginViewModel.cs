using System.Windows.Input;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;

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
        // Demo credentials — any non-empty input proceeds
        if (string.IsNullOrWhiteSpace(AdminId) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter your ID and password.";
            HasError = true;
            return;
        }

        try
        {
            var auth = await _authApiClient.LoginAsync(AdminId.Trim(), Password.Trim(), "Admin");
            if (auth.Response is not null)
                AppSession.SetAdminLoginProfile(auth.Response.Name, null);
            else
                AppSession.SetAdminLoginProfile(AdminId.Trim(), null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cloud admin login skipped: {ex.GetBaseException().Message}");
            AppSession.SetAdminLoginProfile(AdminId.Trim(), null);
        }

        HasError = false;
        _navigate(new AdminDashboardViewModel(_navigate));
    }
}
