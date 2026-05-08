using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
