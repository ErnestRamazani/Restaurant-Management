using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public class PricingPrecedenceTests
{
    [Fact]
    public void ApiOptions_TakesPrecedenceOverFileSettings_WhenPositive()
    {
        var resolved = PricingResolver.ResolveTaxRate(8m, 7m);
        Assert.Equal(8m, resolved);
    }

    [Fact]
    public void FileSettings_UsedAsFallback_WhenApiOptionsZero()
    {
        var resolved = PricingResolver.ResolveTaxRate(0m, 7m);
        Assert.Equal(7m, resolved);
    }

    [Fact]
    public void ResolveTaxRate_Throws_WhenBothSourcesNonPositive()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PricingResolver.ResolveTaxRate(0m, 0m));
        Assert.Contains("TaxPercent", ex.Message);
    }

    [Fact]
    public void ResolveServicePercent_MatchesTaxPrecedence()
    {
        Assert.Equal(12m, PricingResolver.ResolveServicePercent(12m, 10m));
        Assert.Equal(10m, PricingResolver.ResolveServicePercent(0m, 10m));
        Assert.Throws<InvalidOperationException>(() => PricingResolver.ResolveServicePercent(0m, 0m));
    }
}
