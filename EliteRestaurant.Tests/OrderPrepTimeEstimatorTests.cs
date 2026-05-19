using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public class OrderPrepTimeEstimatorTests
{
    [Fact]
    public void MinutesForLineItem_Drink_IsFast() =>
        Assert.Equal(3, OrderPrepTimeEstimator.MinutesForLineItem("Drink", "Coffee"));

    [Fact]
    public void MinutesForLineItem_Cocktail_AddsTime() =>
        Assert.True(OrderPrepTimeEstimator.MinutesForLineItem("Drink", "Cocktail") > 3);

    [Fact]
    public void MinutesForLineItem_UsesStoredPrepMinutes_WhenSet() =>
        Assert.Equal(22, OrderPrepTimeEstimator.MinutesForLineItem(22, "Drink", "Coffee"));

    [Fact]
    public void EstimateTicketPrepMinutes_Empty_IsZero() =>
        Assert.Equal(0, OrderPrepTimeEstimator.EstimateTicketPrepMinutes([]));

    [Fact]
    public void EstimateTicketPrepMinutes_ParallelModel_UsesMaxPlusBump()
    {
        var lines = new List<(int Quantity, int PrepMinutes, string Category, string SubCategory)>
        {
            (2, 0, "Main", "Meat Meal"),
            (1, 0, "Drink", "Coffee")
        };
        var m = OrderPrepTimeEstimator.EstimateTicketPrepMinutes(lines);
        Assert.True(m >= 20);
    }
}
