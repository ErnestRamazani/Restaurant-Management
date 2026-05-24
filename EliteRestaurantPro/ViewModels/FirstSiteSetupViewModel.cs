using System.Windows.Input;
using EliteRestaurant.Contracts.Setup;
using EliteRestaurant.Core.Staff;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Services;

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
    private string _setupPlatformSecret = string.Empty;
    private string _statusMessage = "Connect to your cloud API and create the first restaurant site.";
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private bool _isBusy;
    private bool _cloudNeedsFirstSite = true;
    private string _createButtonText = "Create site & sign in";
    private string _signInToExistingSiteButtonText = "Continue to sign-in";

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

    public string SetupPlatformSecret
    {
        get => _setupPlatformSecret;
        set => SetField(ref _setupPlatformSecret, value);
    }

    public string CreateButtonText
    {
        get => _createButtonText;
        private set => SetField(ref _createButtonText, value);
    }

    public string SignInToExistingSiteButtonText
    {
        get => _signInToExistingSiteButtonText;
        private set => SetField(ref _signInToExistingSiteButtonText, value);
    }

    public bool ShowSetupSecretField => !_cloudNeedsFirstSite;

    /// <summary>Only when the cloud database already has an admin (setup completed on the server).</summary>
    public bool CanSignInToExistingSite => !_cloudNeedsFirstSite;

    public ICommand TestConnectionCommand { get; }
    public ICommand CreateSiteCommand { get; }
    public ICommand SignInToExistingSiteCommand { get; }

    public FirstSiteSetupViewModel(Action<BaseViewModel> navigate)
    {
        _navigate = navigate;
        var settings = SettingsManager.Load();
        CloudApiUrl = string.IsNullOrWhiteSpace(settings.CloudApi.BaseUrl)
            ? CloudEndpoints.ProductionApiBaseUrl
            : settings.CloudApi.BaseUrl;
        SetupPlatformSecret = settings.SetupPlatformSecret?.Trim() ?? string.Empty;

        TestConnectionCommand = new RelayCommand(async _ => await TestConnectionAsync(), _ => !IsBusy);
        CreateSiteCommand = new RelayCommand(async _ => await CreateSiteAsync(), _ => !IsBusy);
        SignInToExistingSiteCommand = new RelayCommand(async _ => await ContinueToSignInAsync(), _ => !IsBusy && CanSignInToExistingSite);
        _ = TestConnectionAsync();
    }

    private async Task ContinueToSignInAsync()
    {
        IsBusy = true;
        HasError = false;
        ErrorMessage = string.Empty;
        try
        {
            var normalizedUrl = CloudEndpoints.NormalizeApiBaseUrl(CloudApiUrl);
            var status = await _setupApi.GetStatusAsync(normalizedUrl);
            if (status is null)
            {
                HasError = true;
                ErrorMessage = "Could not reach the cloud API. Check the URL and try again.";
                return;
            }

            ApplyStatus(status);
            if (status.SetupRequired)
            {
                HasError = true;
                ErrorMessage =
                    "This cloud database has no admin yet. Use Create first site & sign in above — you cannot sign in until that finishes.";
                return;
            }

            var settings = SettingsManager.Load();
            CloudConnectionSettings.ApplyFromSetupStatus(settings, normalizedUrl, status);
            settings.FirstSiteSetupCompleted = true;
            settings.SetupPlatformSecret = SetupPlatformSecret.Trim();
            await CloudConnectionSettings.PullPublicBrandingAsync(settings);
            SettingsManager.Save(settings);

            StatusMessage = string.IsNullOrWhiteSpace(settings.BusinessProfile.RestaurantName)
                ? "Connected to your cloud site. Sign in with your existing admin or staff ID and PIN."
                : $"Connected to {settings.BusinessProfile.RestaurantName}. Sign in with your cloud admin or staff credentials.";
            _navigate(new RoleSelectionViewModel(_navigate));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task TestConnectionAsync()
    {
        IsBusy = true;
        HasError = false;
        ErrorMessage = string.Empty;
        try
        {
            var normalizedUrl = CloudEndpoints.NormalizeApiBaseUrl(CloudApiUrl);
            var status = await _setupApi.GetStatusAsync(normalizedUrl);
            if (status is null)
            {
                HasError = true;
                ErrorMessage = "Could not reach the cloud API. Check the URL and try again.";
                return;
            }

            ApplyStatus(status);

            var settings = SettingsManager.Load();
            CloudConnectionSettings.ApplyFromSetupStatus(settings, normalizedUrl, status);
            SettingsManager.Save(settings);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyStatus(SetupStatusDto status)
    {
        _cloudNeedsFirstSite = status.SetupRequired;
        OnPropertyChanged(nameof(ShowSetupSecretField));
        OnPropertyChanged(nameof(CanSignInToExistingSite));

        CreateButtonText = status.SetupRequired
            ? "Create first site & sign in"
            : "Add new restaurant & sign in";

        SignInToExistingSiteButtonText = status.SetupRequired
            ? "Sign-in available after first site is created"
            : "Continue to sign-in (existing cloud site)";

        StatusMessage = status.SetupRequired
            ? status.RestaurantCount == 0
                ? "Cloud API is online. Create the first restaurant site below."
                : $"Cloud API is online. Finish first-site setup to create an admin ({status.RestaurantCount} restaurant placeholder in database)."
            : !string.IsNullOrWhiteSpace(status.PrimaryRestaurantName)
                ? $"Cloud site “{status.PrimaryRestaurantName}” is ready. Continue to sign-in with your existing ID and PIN."
                : $"Your cloud site already exists ({status.RestaurantCount} restaurant). Continue to sign-in with your existing admin ID and PIN.";
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

            SiteSetupOutcome outcome;
            if (_cloudNeedsFirstSite)
            {
                outcome = await _setupApi.CreateFirstSiteAsync(normalizedUrl, request);
            }
            else
            {
                var secret = SetupPlatformSecret.Trim();
                if (secret.Length == 0)
                {
                    HasError = true;
                    ErrorMessage =
                        "This cloud database already has a restaurant. Set Setup__PlatformSecret on DigitalOcean, paste it above, then try again — or use Continue to sign-in if you only need your existing site.";
                    return;
                }

                outcome = await _setupApi.CreateNewSiteAsync(normalizedUrl, request, secret);
            }

            if (outcome.Response is null)
            {
                HasError = true;
                ErrorMessage = outcome.Errors is { Count: > 0 }
                    ? string.Join(" ", outcome.Errors)
                    : "Setup failed.";
                return;
            }

            var settings = SettingsManager.Load();
            CloudConnectionSettings.ApplyFromSiteSetup(settings, normalizedUrl, outcome.Response);
            settings.SetupPlatformSecret = SetupPlatformSecret.Trim();
            settings.BusinessProfile.RestaurantName = RestaurantName.Trim();
            settings.BusinessProfile.PublicMenuBaseUrl = BuildPublicMenuUrl(normalizedUrl, CustomDomain);
            settings.BusinessProfile.WebsiteDomain = string.IsNullOrWhiteSpace(CustomDomain)
                ? settings.BusinessProfile.WebsiteDomain
                : CustomDomain.Trim();
            settings.FirstSiteSetupCompleted = true;
            await CloudConnectionSettings.PullPublicBrandingAsync(settings);
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
