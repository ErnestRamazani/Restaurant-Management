using System.Globalization;

namespace EliteRestaurant.Core.Utils;

/// <summary>
/// Single restaurant wall-clock timezone (IANA id) for display and calendar boundaries.
/// Store instants in UTC; convert with these helpers on read.
/// </summary>
public static class RestaurantTimeZone
{
    /// <summary>Default when unset — Kinshasa (DRC), matching typical Elite deployments.</summary>
    public const string DefaultId = "Africa/Kinshasa";

    public static string NormalizeId(string? id)
    {
        var trimmed = (id ?? string.Empty).Trim();
        return string.IsNullOrEmpty(trimmed) ? DefaultId : trimmed;
    }

    public static TimeZoneInfo Resolve(string? id)
    {
        var normalized = NormalizeId(id);
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(normalized);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(DefaultId);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(DefaultId);
        }
    }

    public static DateTime UtcToRestaurant(DateTime utc, string? timeZoneId)
    {
        var u = utc.Kind switch
        {
            DateTimeKind.Utc => utc,
            DateTimeKind.Local => utc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc),
        };
        return TimeZoneInfo.ConvertTimeFromUtc(u, Resolve(timeZoneId));
    }

    public static DateTime RestaurantToUtc(DateTime restaurantLocal, string? timeZoneId)
    {
        var unspecified = DateTime.SpecifyKind(restaurantLocal, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, Resolve(timeZoneId));
    }

    public static DateTime RestaurantCalendarDate(DateTime utc, string? timeZoneId) =>
        UtcToRestaurant(utc, timeZoneId).Date;

    public static string FormatUtc(DateTime utc, string? timeZoneId, string format = "g", IFormatProvider? provider = null) =>
        UtcToRestaurant(utc, timeZoneId).ToString(format, provider ?? CultureInfo.CurrentCulture);

    /// <summary>Receipts, tickets, and order lists: format a UTC-stored <see cref="DateTime"/> in restaurant wall time.</summary>
    public static string FormatOrderCreatedAt(DateTime storedUtc, string? timeZoneId, string format = "MMM d, yyyy · HH:mm") =>
        FormatUtc(storedUtc, timeZoneId, format);

    /// <summary>Receipt/ticket date+time block (unspecified kind is treated as UTC).</summary>
    public static DateTime OrderCreatedAtForDisplay(DateTime storedUtc, string? timeZoneId) =>
        UtcToRestaurant(storedUtc, timeZoneId);

    public static string ResolveId(Models.PublicMenuSetting? cloud, BusinessProfileSettings? local) =>
        NormalizeId(cloud?.RestaurantTimeZoneId ?? local?.RestaurantTimeZoneId);
}
