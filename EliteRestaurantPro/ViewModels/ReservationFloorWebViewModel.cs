using System.Text.Json;
using System.Windows.Input;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;

namespace EliteRestaurantPro.ViewModels;

public sealed class ReservationFloorWebViewModel : AdminBaseViewModel
{
    private readonly PublicMenuStaffAuthClient _staffAuth = new();
    private string _passcode = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _showPasscodeGate;

    public override string ActivePage => "Reservations";

    /// <summary>Public menu origin (no trailing slash), same source as elite-menu base URL.</summary>
    public string FloorPageUrl
    {
        get
        {
            var settings = SettingsManager.Load();
            var raw = settings.BusinessProfile.PublicMenuBaseUrl;
            if (string.IsNullOrWhiteSpace(raw))
                raw = settings.CloudApi.BaseUrl;
            var baseUrl = CloudEndpoints.NormalizeApiBaseUrl(raw);
            return $"{baseUrl.TrimEnd('/')}/staff/floor";
        }
    }

    public string Passcode
    {
        get => _passcode;
        set => SetField(ref _passcode, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool ShowPasscodeGate
    {
        get => _showPasscodeGate;
        private set => SetField(ref _showPasscodeGate, value);
    }

    public ICommand SubmitPasscodeCommand { get; }
    public ICommand RetryCloudSessionCommand { get; }

    /// <summary>Raised when the WebView should re-apply token script and navigate.</summary>
    public event EventHandler<string>? FloorSessionTokenReady;

    public ReservationFloorWebViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        SubmitPasscodeCommand = new RelayCommand(async _ => await SubmitPasscodeAsync());
        RetryCloudSessionCommand = new RelayCommand(_ => OnWebViewHostReady());
    }

    /// <summary>Cloud session JWT from admin or cashier <c>api/auth/login</c> (persisted in settings).</summary>
    public string? ReadPersistedCloudAccessToken()
    {
        var token = (SettingsManager.Load().CloudApi.AccessToken ?? string.Empty).Trim();
        return token.Length > 0 ? token : null;
    }

    /// <summary>Called when the view is ready; opens floor if a persisted token exists, otherwise shows passcode gate.</summary>
    public void OnWebViewHostReady()
    {
        if (ReadPersistedCloudAccessToken() is { } jwt)
        {
            ShowPasscodeGate = false;
            StatusMessage = string.Empty;
            FloorSessionTokenReady?.Invoke(this, jwt);
            return;
        }

        ShowPasscodeGate = true;
        StatusMessage = "Sign in with the restaurant staff passcode (same as elite-menu staff hub), or log in as admin/cashier to use your cloud session token.";
    }

    private async Task SubmitPasscodeAsync()
    {
        StatusMessage = string.Empty;
        var code = (Passcode ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            StatusMessage = "Enter the staff passcode.";
            return;
        }

        var settings = SettingsManager.Load();
        var apiBase = settings.BusinessProfile.PublicMenuBaseUrl;
        if (string.IsNullOrWhiteSpace(apiBase))
            apiBase = settings.CloudApi.BaseUrl;

        var (ok, token, error) = await _staffAuth.PostStaffLoginCodeAsync(apiBase, code).ConfigureAwait(true);
        if (!ok || string.IsNullOrEmpty(token))
        {
            StatusMessage = error ?? "Could not validate passcode.";
            return;
        }

        Passcode = string.Empty;
        ShowPasscodeGate = false;
        StatusMessage = string.Empty;
        FloorSessionTokenReady?.Invoke(this, token);
    }

    /// <summary>JavaScript snippet executed before document scripts run (elite-menu reads sessionStorage <c>elite_access_token</c>).</summary>
    public static string SessionStorageTokenScript(string jwt)
        => "sessionStorage.setItem('elite_access_token', " + JsonSerializer.Serialize(jwt) + ");";
}
