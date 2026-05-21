using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public class ReservationConfirmationCodeGeneratorTests
{
    [Fact]
    public void Generate_ReturnsSixUppercaseLetters()
    {
        var code = ReservationConfirmationCodeGenerator.Generate();
        Assert.Equal(6, code.Length);
        Assert.True(code.All(c => c is >= 'A' and <= 'Z'));
        Assert.DoesNotContain('I', code);
        Assert.DoesNotContain('O', code);
    }
}
