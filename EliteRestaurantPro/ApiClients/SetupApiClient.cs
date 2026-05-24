using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using EliteRestaurant.Contracts.Setup;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.ApiClients;

public sealed class SetupApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SetupStatusDto?> GetStatusAsync(string apiBaseUrl, CancellationToken cancellationToken = default)
    {
        var root = CloudEndpoints.NormalizeApiBaseUrl(apiBaseUrl).TrimEnd('/');
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var response = await http.GetAsync($"{root}/api/setup/status", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<SetupStatusDto>(JsonOptions, cancellationToken);
    }

    public Task<SiteSetupOutcome> CreateFirstSiteAsync(
        string apiBaseUrl,
        SiteSetupRequest request,
        CancellationToken cancellationToken = default) =>
        PostSetupAsync(apiBaseUrl, "api/setup/first-site", request, setupSecret: null, cancellationToken);

    public Task<SiteSetupOutcome> CreateNewSiteAsync(
        string apiBaseUrl,
        SiteSetupRequest request,
        string setupPlatformSecret,
        CancellationToken cancellationToken = default) =>
        PostSetupAsync(apiBaseUrl, "api/setup/new-site", request, setupPlatformSecret, cancellationToken);

    private static async Task<SiteSetupOutcome> PostSetupAsync(
        string apiBaseUrl,
        string path,
        SiteSetupRequest request,
        string? setupSecret,
        CancellationToken cancellationToken)
    {
        var root = CloudEndpoints.NormalizeApiBaseUrl(apiBaseUrl).TrimEnd('/');
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{root}/{path.TrimStart('/')}")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        if (!string.IsNullOrWhiteSpace(setupSecret))
            message.Headers.TryAddWithoutValidation("X-Setup-Secret", setupSecret.Trim());

        using var response = await http.SendAsync(message, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<SiteSetupResponse>(JsonOptions, cancellationToken);
            return new SiteSetupOutcome(body, null);
        }

        var errors = await TryReadErrorsAsync(response, cancellationToken);
        if (errors is null)
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            errors = string.IsNullOrWhiteSpace(raw)
                ? [$"Setup failed (HTTP {(int)response.StatusCode})."]
                : [raw.Trim()];
        }

        return new SiteSetupOutcome(null, errors);
    }

    private static async Task<IReadOnlyList<string>?> TryReadErrorsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var err = await response.Content.ReadFromJsonAsync<SiteSetupErrorDto>(JsonOptions, cancellationToken);
            if (err?.Errors is { Count: > 0 })
                return err.Errors;
        }
        catch (JsonException)
        {
            /* ignore */
        }

        try
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("message", out var message))
            {
                var text = message.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return [text];
            }
        }
        catch (JsonException)
        {
            /* ignore */
        }

        return null;
    }
}

public sealed record SiteSetupOutcome(SiteSetupResponse? Response, IReadOnlyList<string>? Errors);
