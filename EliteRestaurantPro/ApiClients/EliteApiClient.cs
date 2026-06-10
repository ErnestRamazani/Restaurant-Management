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

    private Uri BuildRequestUri(string path) => BuildAbsoluteRequestUri(_apiBaseUrl, path);

    private static Uri BuildAbsoluteRequestUri(string apiBaseUrl, string path)
    {
        var baseNorm = CloudEndpoints.NormalizeApiBaseUrl(apiBaseUrl).TrimEnd('/') + "/";
        var relative = Normalize(path);
        return new Uri(new Uri(baseNorm, UriKind.Absolute), relative);
    }

    private static void ApplyBearer(HttpRequestMessage request, string? token)
    {
        request.Headers.Authorization = string.IsNullOrEmpty(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    private static void ApplyTenantHeaders(HttpRequestMessage request)
    {
        var cloud = SettingsManager.Load().CloudApi;
        PublicMenuApiClient.ApplyTenantHeaders(request, cloud.RestaurantId, cloud.RestaurantSlug);
    }

    public async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        return await GetAsync<T>(_apiBaseUrl, path, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> GetAsync<T>(string apiBaseUrl, string path, CancellationToken cancellationToken = default)
    {
        var uri = BuildAbsoluteRequestUri(apiBaseUrl, path);
        using var response = await SendWithRetryAsync(
                () =>
                {
                    var r = new HttpRequestMessage(HttpMethod.Get, uri);
                    ApplyBearer(r, _bearerToken);
                    ApplyTenantHeaders(r);
                    return r;
                },
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> PutAsync<TRequest>(string path, TRequest payload, CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(
                () =>
                {
                    var r = new HttpRequestMessage(HttpMethod.Put, BuildRequestUri(path))
                    {
                        Content = JsonContent.Create(payload, options: JsonOptions)
                    };
                    ApplyBearer(r, _bearerToken);
                    ApplyTenantHeaders(r);
                    return r;
                },
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(
                () =>
                {
                    var r = new HttpRequestMessage(HttpMethod.Delete, BuildRequestUri(path));
                    ApplyBearer(r, _bearerToken);
                    ApplyTenantHeaders(r);
                    return r;
                },
                cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return false;

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return true;
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
                    ApplyTenantHeaders(r);
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
            ApplyTenantHeaders(request);
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
        return await PostAsync<TRequest, TResponse>(_apiBaseUrl, path, payload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POST to an explicit API host (e.g. production public menu URL) instead of <see cref="ResolveDesktopApiBaseUrl"/>.</summary>
    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string apiBaseUrl,
        string path,
        TRequest payload,
        CancellationToken cancellationToken = default)
    {
        var uri = BuildAbsoluteRequestUri(apiBaseUrl, path);
        using var response = await SendWithRetryAsync(
                () =>
                {
                    var r = new HttpRequestMessage(HttpMethod.Post, uri)
                    {
                        Content = JsonContent.Create(payload, options: JsonOptions)
                    };
                    ApplyBearer(r, _bearerToken);
                    ApplyTenantHeaders(r);
                    return r;
                },
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// POST where <c>400 Bad Request</c> may return the same JSON shape as success (e.g. validation / insufficient inventory).
    /// Other non-success responses use <see cref="EnsureSuccessAsync"/> (clears token on 401, throws with body text).
    /// </summary>
    public async Task<TResponse?> PostAsyncOrBadRequestAsync<TRequest, TResponse>(
        string path,
        TRequest payload,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(
                () =>
                {
                    var r = new HttpRequestMessage(HttpMethod.Post, BuildRequestUri(path))
                    {
                        Content = JsonContent.Create(payload, options: JsonOptions)
                    };
                    ApplyBearer(r, _bearerToken);
                    ApplyTenantHeaders(r);
                    return r;
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
            }
            catch (JsonException)
            {
                throw new HttpRequestException($"API request failed ({(int)response.StatusCode}): {body}");
            }
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// POST without <c>Authorization</c>. Required for <c>api/auth/login</c>: a stale Bearer from a prior admin session
    /// can make JWT middleware reject the request before it reaches <c>[AllowAnonymous]</c>, so staff sign-in would fail while web works.
    /// </summary>
    public async Task<TResponse?> PostWithoutBearerAsync<TRequest, TResponse>(string path, TRequest payload, CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(
                () =>
                {
                    var r = new HttpRequestMessage(HttpMethod.Post, BuildRequestUri(path))
                    {
                        Content = JsonContent.Create(payload, options: JsonOptions)
                    };
                    ApplyTenantHeaders(r);
                    return r;
                },
                cancellationToken)
            .ConfigureAwait(false);
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
    /// Base URL for pushing Business Profile / public menu settings to the hosted API.
    /// Uses <see cref="BusinessProfileSettings.PublicMenuBaseUrl"/> and never applies the localhost dev redirect.
    /// </summary>
    public static string ResolvePublicMenuCloudBaseUrl(AppSettings appSettings)
    {
        var url = string.IsNullOrWhiteSpace(appSettings.BusinessProfile.PublicMenuBaseUrl)
            ? appSettings.CloudApi.BaseUrl
            : appSettings.BusinessProfile.PublicMenuBaseUrl;
        return CloudEndpoints.NormalizeApiBaseUrl(url);
    }

    /// <summary>
    /// API base for admin/sync and other desktop HTTP calls. When <see cref="BusinessProfileSettings.PublicMenuBaseUrl"/>
    /// points at hosted production, use that host so menu/products and settings land in the same cloud database.
    /// Localhost redirect applies only when both the public menu URL and cloud base are still dev/local targets.
    /// </summary>
    public static string ResolveDesktopApiBaseUrl(AppSettings appSettings)
    {
        // Login, sync, and admin calls must use CloudApi.BaseUrl — not the public menu / custom domain,
        // which may be a different host or not expose /api/* until DNS is fully configured.
        var cloudApi = CloudEndpoints.NormalizeApiBaseUrl(appSettings.CloudApi.BaseUrl);
        if (!CloudEndpoints.IsLocalDevelopmentApiUrl(cloudApi))
            return cloudApi;

        var publicMenuTarget = ResolvePublicMenuCloudBaseUrl(appSettings);
        if (!CloudEndpoints.IsLocalDevelopmentApiUrl(publicMenuTarget))
            return publicMenuTarget;

        var dbHost = (appSettings.Database?.PostgreSqlHost ?? string.Empty).Trim().ToLowerInvariant();
        var localDbHost = dbHost is "localhost" or "127.0.0.1" or "::1";
        if (localDbHost && string.Equals(cloudApi, CloudEndpoints.ProductionApiBaseUrl, StringComparison.OrdinalIgnoreCase))
            return CloudEndpoints.LocalApiBaseUrl;

        return cloudApi;
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
