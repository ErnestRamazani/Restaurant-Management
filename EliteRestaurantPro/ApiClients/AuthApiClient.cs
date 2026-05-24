using System.Net.Http;
using System.Text.Json;
using EliteRestaurant.Contracts.Auth;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.Services;

namespace EliteRestaurantPro.ApiClients;

public sealed class AuthApiClient(EliteApiClient? apiClient = null)
{
    private readonly EliteApiClient _apiClient = apiClient ?? new EliteApiClient();

    public async Task<CloudAuthResult> LoginAsync(
        string staffId,
        string pin,
        string portal,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.PostWithoutBearerAsync<CloudLoginRequest, CloudLoginResponse>(
                "api/auth/login",
                new CloudLoginRequest(staffId, pin, portal),
                cancellationToken);

            if (response is not null)
            {
                var settings = SettingsManager.Load();
                settings.CloudApi.AccessToken = response.AccessToken;
                settings.CloudApi.TokenExpiresAtUtc = response.ExpiresAtUtc;
                CloudConnectionSettings.ApplyRestaurantIdFromAccessToken(settings, response.AccessToken);
                SettingsManager.Save(settings);
                _apiClient.ReloadFromSettings();
            }

            return new CloudAuthResult(response, response is null ? "Login returned an empty response." : null);
        }
        catch (HttpRequestException ex)
        {
            var message = TryParseApiErrorMessage(ex.Message);
            if (ex.Message.Contains("(401)", StringComparison.Ordinal) ||
                ex.Message.Contains("(403)", StringComparison.Ordinal))
            {
                return new CloudAuthResult(
                    null,
                    message ?? "Sign-in was rejected. Check ID, PIN, and that this account may use the selected portal.");
            }

            return new CloudAuthResult(null, message ?? ex.GetBaseException().Message);
        }
    }

    private static string? TryParseApiErrorMessage(string exMessage)
    {
        var marker = "): ";
        var idx = exMessage.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
            return null;

        var json = exMessage[(idx + marker.Length)..].Trim();
        if (json.Length == 0)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var m))
                return m.GetString();
            if (doc.RootElement.TryGetProperty("title", out var t))
                return t.GetString();
        }
        catch (JsonException)
        {
            /* body was not JSON */
        }

        return null;
    }
}
