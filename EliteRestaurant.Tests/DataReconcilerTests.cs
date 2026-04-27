using EliteRestaurant.Core.Data;
using Xunit;

namespace EliteRestaurant.Tests;

public class DataReconcilerTests
{
    [Theory]
    [InlineData("Waiting", true)]
    [InlineData("READY", true)]
    [InlineData("Completed", false)]
    public void IsActiveOrderStatus_MatchesKitchenPipeline(string status, bool expected) =>
        Assert.Equal(expected, DataReconciler.IsActiveOrderStatus(status));
}
