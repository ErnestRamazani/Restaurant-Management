using System.Windows.Input;
using EliteRestaurant.Contracts.Setup;
using EliteRestaurant.Core.Staff;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;

namespace EliteRestaurantPro.ViewModels;

public sealed class FirstSiteSetupViewModel : BaseViewModel
{
    private readonly Action<BaseViewModel> _navigate;
    private readonly SetupApiClient _setupApi = new();

    private string _cloudApiUrl = CloudEndpoints.ProductionApiBaseUrl;
    private string _restaurantName = string.Empty;
    private string _slug = string.Empty;
    private string _customDomain = string.Empty;
    private string _adminSignInId = "admin";
    private string _adminPin = string.Empty;
    private string _confirmPin = string.Empty;
    private string _statusMessage = "Connect to your cloud API and create the first restaurant site.";
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private bool _isBusy;

    public string CloudApiUrl
    {
        get => _cloudApiUrl;
        set => SetField(ref _cloudApiUrl, value);
    }

    public string RestaurantName
    {
        get => _restaurantName;
        set
        {
            if (!SetField(ref _restaurantName, value))
                return;
            if (string.IsNullOrWhiteSpace(_slug))
                Slug = RestaurantSlug.Normalize(null, value);
        }
    }

    public string Slug
    {
        get => _slug;
        set => SetField(ref _slug, value);
    }

    public string CustomDomain
    {
        get => _customDomain;
        set => SetField(ref _customDomain, value);
    }

    public string AdminSignInId
    {
        get => _adminSignInId;
        set => SetField(ref _adminSignInId, value);
    }

    public string AdminPin { get => _adminPin; set => SetField(ref _adminPin, value); }
    public string ConfirmPin { get => _confirmPin; set => SetField(ref _confirmPin, value); }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
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

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public ICommand TestConnectionCommand { get; }
    public ICommand CreateSiteCommand { get; }
    public ICommand SkipCommand { get; }

    public FirstSiteSetupViewModel(Action<BaseViewModel> navigate)
    {
        _navigate = navigate;
        var settings = SettingsManager.Load();
        CloudApiUrl = string.IsNullOrWhiteSpace(settings.CloudApi.BaseUrl)
            ? CloudEndpoints.ProductionApiBaseUrl
            : settings.CloudApi.BaseUrl;

        TestConnectionCommand = new RelayCommand(async _ => await TestConnectionAsync(), _ => !IsBusy);
        CreateSiteCommand = new RelayCommand(async _ => await CreateSiteAsync(), _ => !IsBusy);
        SkipCommand = new RelayCommand(_ => _navigate(new RoleSelectionViewModel(_navigate)), _ => !IsBusy);
    }

    private async Task TestConnectionAsync()
    {
        IsBusy = true;
        HasError = false;
        ErrorMessage = string.Empty;
        try
        {
            var status = await _setupApi.GetStatusAsync(CloudApiUrl);
            if (status is null)
            {
                HasError = true;
                ErrorMessage = "Could not reach the cloud API. Check the URL and try again.";
                return;
            }

            StatusMessage = status.SetupRequired
                ? $"Cloud API is online. Ready to create the first site ({status.RestaurantCount} restaurants now)."
                : $"Cloud API is online. Setup is not required ({status.RestaurantCount} restaurant(s) already exist). You can skip to sign in.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CreateSiteAsync()
    {
        IsBusy = true;
        HasError = false;
        ErrorMessage = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(RestaurantName))
            {
                HasError = true;
                ErrorMessage = "Enter a restaurant name.";
                return;
            }

            if (!string.Equals(AdminPin.Trim(), ConfirmPin.Trim(), StringComparison.Ordinal))
            {
                HasError = true;
                ErrorMessage = "PIN and confirmation do not match.";
                return;
            }

            var normalizedUrl = CloudEndpoints.NormalizeApiBaseUrl(CloudApiUrl);
            var request = new SiteSetupRequest(
                RestaurantName.Trim(),
                string.IsNullOrWhiteSpace(Slug) ? null : Slug.Trim(),
                string.IsNullOrWhiteSpace(CustomDomain) ? null : CustomDomain.Trim(),
                AdminSignInId.Trim(),
                AdminPin.Trim(),
                null,
                "en");

            var outcome = await _setupApi.CreateFirstSiteAsync(normalizedUrl, request);
            if (outcome.Response is null)
            {
                HasError = true;
                ErrorMessage = outcome.Errors is { Count: > 0 }
                    ? string.Join(" ", outcome.Errors)
                    : "Setup failed.";
                return;
            }

            var settings = SettingsManager.Load();
            settings.CloudApi.BaseUrl = normalizedUrl;
            settings.CloudApi.AccessToken = outcome.Response.AccessToken;
            settings.CloudApi.TokenExpiresAtUtc = outcome.Response.ExpiresAtUtc;
            settings.BusinessProfile.RestaurantName = RestaurantName.Trim();
            settings.BusinessProfile.PublicMenuBaseUrl = BuildPublicMenuUrl(normalizedUrl, CustomDomain);
            settings.BusinessProfile.WebsiteDomain = string.IsNullOrWhiteSpace(CustomDomain)
                ? settings.BusinessProfile.WebsiteDomain
                : CustomDomain.Trim();
            SettingsManager.Save(settings);

            if (!StaffPortalAuthentication.IsAdminDesktopRole(outcome.Response.Role))
            {
                HasError = true;
                ErrorMessage = StaffPortalAuthentication.AdminDesktopPortalRejectedMessage(outcome.Response.Role);
                return;
            }

            AppSession.SetAdminLoginProfile(outcome.Response.Name, null);
            StatusMessage = $"Site created for {outcome.Response.Slug}. Signed in as {outcome.Response.SignInId}.";
            _navigate(new AdminDashboardViewModel(_navigate));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string BuildPublicMenuUrl(string apiBaseUrl, string? customDomain)
    {
        if (!string.IsNullOrWhiteSpace(customDomain))
        {
            var host = customDomain.Trim();
            if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return CloudEndpoints.NormalizeApiBaseUrl(host);

            return $"https://{host.TrimStart('/')}";
        }

        return apiBaseUrl;
    }
}
