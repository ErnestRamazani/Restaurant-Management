using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public class OrderDiscountParserTests
{
    [Theory]
    [InlineData("12.5", 12.5)]
    [InlineData("", 0)]
    [InlineData("  ", 0)]
    public void Parse_AcceptsInvariantDecimals(string? text, decimal expected) =>
        Assert.Equal(expected, OrderDiscountParser.Parse(text));

    [Theory]
    [InlineData("Percent", "10", true)]
    [InlineData("Percent", "0", false)]
    [InlineData("Percent", "101", false)]
    [InlineData("Usd", "5", true)]
    [InlineData("None", "99", false)]
    public void ShouldApplyDiscount_RespectsMode(string mode, string input, bool expected) =>
        Assert.Equal(expected, OrderDiscountParser.ShouldApplyDiscount(mode, input));
}
