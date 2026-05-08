using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.ApiClients;

public sealed class EliteApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public EliteApiClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient();
        ConfigureFromSettings();
    }

    public bool IsConfigured => _http.BaseAddress is not null;

    public void ConfigureFromSettings()
    {
        var settings = SettingsManager.Load().CloudApi;
        var baseUrl = CloudEndpoints.NormalizeApiBaseUrl(settings.BaseUrl);
        _http.BaseAddress = new Uri(baseUrl + "/");

        _http.Timeout = TimeSpan.FromSeconds(20);
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(settings.AccessToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", settings.AccessToken);
    }

    public async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(() => _http.GetAsync(Normalize(path), cancellationToken), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    /// <summary>
    /// Load-order bundle is absent on older API hosts; those may incorrectly return SPA HTML (200) for unknown routes.
    /// Returns null when the route is missing or the body is not JSON — callers should fall back to legacy list calls.
    /// </summary>
    public async Task<AdminCreateOrderCatalogBundleResponse?> TryGetCreateOrderCatalogBundleAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(
            () => _http.GetAsync(
                Normalize("api/admin/data/bundles/create-order"),
                cancellationToken),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, cancellationToken);

        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
            || !mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            return await response.Content.ReadFromJsonAsync<AdminCreateOrderCatalogBundleResponse>(
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<bool> CanReachApiAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync("api/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest payload, CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(
            () => _http.PostAsJsonAsync(Normalize(path), payload, JsonOptions, cancellationToken),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }

    /// <summary>
    /// POST without <c>Authorization</c>. Required for <c>api/auth/login</c>: a stale Bearer from a prior admin session
    /// can make JWT middleware reject the request before it reaches <c>[AllowAnonymous]</c>, so staff sign-in would fail while web works.
    /// </summary>
    public async Task<TResponse?> PostWithoutBearerAsync<TRequest, TResponse>(string path, TRequest payload, CancellationToken cancellationToken = default)
    {
        using var http = CreateUnauthenticatedClient();
        using var response = await SendWithRetryAsync(
            () => http.PostAsJsonAsync(Normalize(path), payload, JsonOptions, cancellationToken),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }

    private static HttpClient CreateUnauthenticatedClient()
    {
        var settings = SettingsManager.Load().CloudApi;
        var baseUrl = CloudEndpoints.NormalizeApiBaseUrl(settings.BaseUrl);
        return new HttpClient
        {
            BaseAddress = new Uri(baseUrl + "/"),
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    private static string Normalize(string path) => path.TrimStart('/');

    private static async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var response = await send();
                if ((int)response.StatusCode < 500 || attempt == 3)
                    return response;
                response.Dispose();
            }
            catch (HttpRequestException ex) when (attempt < 3)
            {
                last = ex;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested && attempt < 3)
            {
                last = ex;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
        }

        throw last ?? new HttpRequestException("Request failed.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"API request failed ({(int)response.StatusCode}): {body}");
    }
}
