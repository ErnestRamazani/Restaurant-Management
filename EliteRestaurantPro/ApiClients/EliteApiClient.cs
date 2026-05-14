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
    private string _apiBaseUrl = string.Empty;
    private string? _bearerToken;

    public EliteApiClient(HttpClient? httpClient = null)
    {
        // Do not set BaseAddress or DefaultRequestHeaders on a long-lived client — they cannot be
        // changed after the first request (ReloadFromSettings / token refresh would throw).
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        ReloadFromSettings();
    }

    /// <summary>Re-reads base URL and bearer token from disk. Safe after requests have been sent.</summary>
    public void ReloadFromSettings()
    {
        var appSettings = SettingsManager.Load();
        _apiBaseUrl = ResolveDesktopApiBaseUrl(appSettings).TrimEnd('/') + "/";
        var token = (appSettings.CloudApi.AccessToken ?? string.Empty).Trim();
        _bearerToken = token.Length > 0 ? token : null;
    }

    public bool IsConfigured => _apiBaseUrl.Length > 0;

    private Uri BuildRequestUri(string path)
    {
        var relative = Normalize(path);
        return new Uri(new Uri(_apiBaseUrl, UriKind.Absolute), relative);
    }

    private static void ApplyBearer(HttpRequestMessage request, string? token)
    {
        request.Headers.Authorization = string.IsNullOrEmpty(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(
                () =>
                {
                    var r = new HttpRequestMessage(HttpMethod.Get, BuildRequestUri(path));
                    ApplyBearer(r, _bearerToken);
                    return r;
                },
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Load-order bundle is absent on older API hosts; those may incorrectly return SPA HTML (200) for unknown routes.
    /// Returns null when the route is missing or the body is not JSON — callers should fall back to legacy list calls.
    /// </summary>
    public async Task<AdminCreateOrderCatalogBundleResponse?> TryGetCreateOrderCatalogBundleAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(
                () =>
                {
                    var r = new HttpRequestMessage(HttpMethod.Get, BuildRequestUri("api/admin/data/bundles/create-order"));
                    ApplyBearer(r, _bearerToken);
                    return r;
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
            || !mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            return await response.Content.ReadFromJsonAsync<AdminCreateOrderCatalogBundleResponse>(
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
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
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildRequestUri("api/health"));
            ApplyBearer(request, _bearerToken);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
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
                () =>
                {
                    var r = new HttpRequestMessage(HttpMethod.Post, BuildRequestUri(path))
                    {
                        Content = JsonContent.Create(payload, options: JsonOptions)
                    };
                    ApplyBearer(r, _bearerToken);
                    return r;
                },
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// POST without <c>Authorization</c>. Required for <c>api/auth/login</c>: a stale Bearer from a prior admin session
    /// can make JWT middleware reject the request before it reaches <c>[AllowAnonymous]</c>, so staff sign-in would fail while web works.
    /// </summary>
    public async Task<TResponse?> PostWithoutBearerAsync<TRequest, TResponse>(string path, TRequest payload, CancellationToken cancellationToken = default)
    {
        using var http = CreateUnauthenticatedClient();
        using var response = await SendWithRetryDelegateAsync(
            () => http.PostAsJsonAsync(Normalize(path), payload, JsonOptions, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static HttpClient CreateUnauthenticatedClient()
    {
        var appSettings = SettingsManager.Load();
        var baseUrl = ResolveDesktopApiBaseUrl(appSettings).TrimEnd('/') + "/";
        return new HttpClient
        {
            BaseAddress = new Uri(baseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    /// <summary>
    /// Keep desktop sync aligned with local dev/runtime by default:
    /// when DB host is local and cloud base is still production default, route API calls to localhost.
    /// </summary>
    private static string ResolveDesktopApiBaseUrl(AppSettings appSettings)
    {
        var configured = CloudEndpoints.NormalizeApiBaseUrl(appSettings.CloudApi.BaseUrl);
        var dbHost = (appSettings.Database?.PostgreSqlHost ?? string.Empty).Trim().ToLowerInvariant();
        var localDbHost = dbHost is "localhost" or "127.0.0.1" or "::1";
        if (localDbHost && string.Equals(configured, CloudEndpoints.ProductionApiBaseUrl, StringComparison.OrdinalIgnoreCase))
            return CloudEndpoints.LocalApiBaseUrl;

        return configured;
    }

    private static string Normalize(string path) => path.TrimStart('/');

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> buildRequest,
        CancellationToken cancellationToken)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var request = buildRequest();
                var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
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

            await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken).ConfigureAwait(false);
        }

        throw last ?? new HttpRequestException("Request failed.");
    }

    /// <summary>Retry helper for short-lived <see cref="HttpClient"/> calls (e.g. login) where each attempt creates a new request.</summary>
    private static async Task<HttpResponseMessage> SendWithRetryDelegateAsync(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var response = await send().ConfigureAwait(false);
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

            await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken).ConfigureAwait(false);
        }

        throw last ?? new HttpRequestException("Request failed.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var settings = SettingsManager.Load();
            settings.CloudApi.AccessToken = string.Empty;
            settings.CloudApi.TokenExpiresAtUtc = null;
            SettingsManager.Save(settings);
        }

        throw new HttpRequestException($"API request failed ({(int)response.StatusCode}): {body}");
    }
}
