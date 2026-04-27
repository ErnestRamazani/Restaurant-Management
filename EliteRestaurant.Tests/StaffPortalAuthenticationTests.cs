using EliteRestaurant.Core.Staff;
using Xunit;

namespace EliteRestaurant.Tests;

public class StaffPortalAuthenticationTests
{
    [Theory]
    [InlineData("Chef", true)]
    [InlineData("Bartender", true)]
    [InlineData("Server", false)]
    public void IsKitchenBarRole_Classifies(string role, bool expected) =>
        Assert.Equal(expected, StaffPortalAuthentication.IsKitchenBarRole(role));
}
