using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public sealed class RestaurantSlugTests
{
    [Theory]
    [InlineData("Etoile Gourmande", null, "etoile-gourmande")]
    [InlineData(null, "My Place!", "my-place")]
    [InlineData("  Hello   World  ", null, "hello-world")]
    public void Normalize_ProducesExpectedSlug(string? name, string? slug, string expected)
    {
        var result = RestaurantSlug.Normalize(slug, name);
        Assert.Equal(expected, result);
        Assert.True(RestaurantSlug.IsValid(result));
    }
}
