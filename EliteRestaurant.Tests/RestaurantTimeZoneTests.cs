using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public sealed class RestaurantTimeZoneTests
{
    [Fact]
    public void NormalizeId_uses_default_when_blank()
    {
        Assert.Equal(RestaurantTimeZone.DefaultId, RestaurantTimeZone.NormalizeId(null));
        Assert.Equal(RestaurantTimeZone.DefaultId, RestaurantTimeZone.NormalizeId("  "));
    }

    [Fact]
    public void UtcToRestaurant_kinshasa_is_utc_plus_one_in_winter()
    {
        var utc = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var local = RestaurantTimeZone.UtcToRestaurant(utc, "Africa/Kinshasa");
        Assert.Equal(11, local.Hour);
        Assert.Equal(1, local.Day);
    }

    [Fact]
    public void RestaurantToUtc_round_trips_wall_clock()
    {
        var wall = new DateTime(2026, 6, 1, 12, 30, 0);
        var utc = RestaurantTimeZone.RestaurantToUtc(wall, "Africa/Kinshasa");
        var back = RestaurantTimeZone.UtcToRestaurant(utc, "Africa/Kinshasa");
        Assert.Equal(wall, back);
    }

    [Fact]
    public void FormatOrderCreatedAt_shows_restaurant_wall_time_not_utc()
    {
        var utc = new DateTime(2026, 6, 1, 7, 33, 0, DateTimeKind.Utc);
        var text = RestaurantTimeZone.FormatOrderCreatedAt(utc, "Africa/Kinshasa");
        Assert.Contains("08:33", text);
        Assert.DoesNotContain("07:33", text);
    }
}
