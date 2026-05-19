using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public class OrderTotalsCalculatorTests
{
    private readonly OrderTotalsCalculator _calculator = new();

    [Fact]
    public void ComputeTicket_AggregatesDiscountTaxServicePrep()
    {
        var prepLines = new List<(int Quantity, int PrepMinutes, string Category, string SubCategory)>
        {
            (2, 0, "Main", "Pasta")
        };
        var result = _calculator.ComputeTicket(100m, 2, "Percent", "10", prepLines);

        Assert.Equal(2, result.LiveItemCount);
        Assert.Equal(100m, result.TicketSubtotal);
        Assert.True(result.DiscountApplied > 0m);
        Assert.True(result.TaxAmount >= 0m);
        Assert.True(result.ServiceAmount >= 0m);
        Assert.True(result.GrandTotal > 0m);
        Assert.True(result.EstimatedPrepMinutes > 0);

        var raw = OrderDiscountParser.Parse("10");
        var expected = OrderTotalsHelper.ComputeTotals(100m, "Percent", raw);
        Assert.Equal(expected.DiscountApplied, result.DiscountApplied);
        Assert.Equal(expected.Tax, result.TaxAmount);
        Assert.Equal(expected.Service, result.ServiceAmount);
        Assert.Equal(expected.GrandTotal, result.GrandTotal);
    }
}
