using Microsoft.AspNetCore.Http;

namespace EliteRestaurant.Api.Security;

public static class HttpAuthExtensions
{
    public static string? ReadBearerToken(this HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out var values))
            return null;

        var header = values.ToString();
        if (string.IsNullOrWhiteSpace(header))
            return null;

        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        return header[prefix.Length..].Trim();
    }
}
