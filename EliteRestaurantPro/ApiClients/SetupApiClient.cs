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

    public async Task<SiteSetupOutcome> CreateFirstSiteAsync(
        string apiBaseUrl,
        SiteSetupRequest request,
        CancellationToken cancellationToken = default)
    {
        var root = CloudEndpoints.NormalizeApiBaseUrl(apiBaseUrl).TrimEnd('/');
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var response = await http.PostAsJsonAsync($"{root}/api/setup/first-site", request, JsonOptions, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<SiteSetupResponse>(JsonOptions, cancellationToken);
            return new SiteSetupOutcome(body, null);
        }

        var errors = await TryReadErrorsAsync(response, cancellationToken);
        return new SiteSetupOutcome(null, errors ?? [$"Setup failed ({(int)response.StatusCode})."]);
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

        return null;
    }
}

public sealed record SiteSetupOutcome(SiteSetupResponse? Response, IReadOnlyList<string>? Errors);
