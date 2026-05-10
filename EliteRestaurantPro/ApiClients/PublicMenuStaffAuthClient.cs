using System.Net.Http;
using System.Text.Json;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.ApiClients;

/// <summary>Anonymous public menu endpoints used by elite-menu staff gate.</summary>
public sealed class PublicMenuStaffAuthClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<(bool Ok, string? AccessToken, string? Error)> PostStaffLoginCodeAsync(
        string? apiBaseUrl,
        string code,
        CancellationToken cancellationToken = default)
    {
        var root = CloudEndpoints.NormalizeApiBaseUrl(apiBaseUrl);
        var trimmed = (code ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return (false, null, "Enter the staff passcode.");

        var url =
            $"{root}/api/public/menu/staff-login-code/{Uri.EscapeDataString(trimmed)}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        try
        {
            using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var rootEl = doc.RootElement;
            var success = rootEl.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
            var token = rootEl.TryGetProperty("accessToken", out var t) ? t.GetString() : null;
            var message = rootEl.TryGetProperty("message", out var m) ? m.GetString() : null;

            if (!response.IsSuccessStatusCode || !success)
                return (false, null, message ?? $"Staff login failed ({(int)response.StatusCode}).");

            if (string.IsNullOrWhiteSpace(token))
                return (false, null, "Staff login returned no token.");

            return (true, token, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.GetBaseException().Message);
        }
    }
}
