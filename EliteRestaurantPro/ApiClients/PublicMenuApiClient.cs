using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.ApiClients;

public sealed class PublicMenuApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PublicMenuConfigSnapshot?> TryGetConfigAsync(
        string apiBaseUrl,
        int? restaurantId,
        string? restaurantSlug,
        CancellationToken cancellationToken = default)
    {
        var root = CloudEndpoints.NormalizeApiBaseUrl(apiBaseUrl).TrimEnd('/');
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{root}/api/public/menu/config");
        ApplyTenantHeaders(request, restaurantId, restaurantSlug);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<PublicMenuConfigSnapshot>(JsonOptions, cancellationToken);
    }

    public static void ApplyTenantHeaders(HttpRequestMessage request, int? restaurantId, string? restaurantSlug)
    {
        if (restaurantId is int id && id > 0)
            request.Headers.TryAddWithoutValidation("X-Restaurant-Id", id.ToString());
        else if (!string.IsNullOrWhiteSpace(restaurantSlug))
            request.Headers.TryAddWithoutValidation("X-Restaurant-Slug", restaurantSlug.Trim());
    }
}

public sealed record PublicMenuConfigSnapshot(
    [property: JsonPropertyName("restaurantName")] string RestaurantName,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("websiteDomain")] string? WebsiteDomain,
    [property: JsonPropertyName("socialMedia")] string? SocialMedia);
