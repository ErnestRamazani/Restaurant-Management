using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public class OrderConfirmationCodeGeneratorTests
{
    [Fact]
    public void Generate_ReturnsSixDigits()
    {
        var code = OrderConfirmationCodeGenerator.Generate();
        Assert.Equal(6, code.Length);
        Assert.True(code.All(char.IsDigit));
    }
}
