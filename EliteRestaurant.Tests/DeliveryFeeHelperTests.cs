using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public class DeliveryFeeHelperTests
{
    [Fact]
    public void ResolveFeeUsd_UsesConfiguredPercent()
    {
        var pricing = new PublicMenuSetting { DeliveryFeePercent = 15m };
        Assert.Equal(15m, DeliveryFeeHelper.ResolveFeeUsd(100m, pricing));
    }

    [Fact]
    public void ResolvePercent_UsesCloudPricingWhenSet()
    {
        Assert.Equal(15m, DeliveryFeeHelper.ResolvePercent(new PublicMenuSetting { DeliveryFeePercent = 15m }));
    }

    [Fact]
    public void DefaultPercent_IsTwenty()
    {
        Assert.Equal(20m, DeliveryFeeHelper.DefaultPercent);
    }

    [Fact]
    public void ResolveFeeUsd_ReturnsZeroForEmptySubtotal()
    {
        Assert.Equal(0m, DeliveryFeeHelper.ResolveFeeUsd(0m));
    }
}
