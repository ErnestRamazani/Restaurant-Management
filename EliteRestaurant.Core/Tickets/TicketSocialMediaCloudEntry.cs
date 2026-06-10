using System.Text.Json;

namespace EliteRestaurant.Core.Tickets;

public sealed class TicketSocialMediaCloudEntry
{
    public string PlatformName { get; set; } = string.Empty;
    public string UserText { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;
}

public static class TicketSocialMediaCloudJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(IReadOnlyList<TicketSocialMediaCloudEntry> rows) =>
        JsonSerializer.Serialize(rows, Options);

    public static List<TicketSocialMediaCloudEntry> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<TicketSocialMediaCloudEntry>>(json, Options) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
