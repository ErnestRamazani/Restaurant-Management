using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Staff;
using Xunit;

namespace EliteRestaurant.Tests;

public class StaffPortalAuthenticationTests
{
    [Theory]
    [InlineData("AdminWeb", "AdminWeb")]
    [InlineData("Chef", "Kitchen")]
    [InlineData("Barman", "Bar")]
    [InlineData("Front desk", "Reception")]
    public void CanonicalPortalForRole_Maps(string role, string expected) =>
        Assert.Equal(expected, StaffPortalAuthentication.CanonicalPortalForRole(role));

    [Theory]
    [InlineData("Chef", true)]
    [InlineData("Bartender", true)]
    [InlineData("Server", false)]
    public void IsKitchenBarRole_Classifies(string role, bool expected) =>
        Assert.Equal(expected, StaffPortalAuthentication.IsKitchenBarRole(role));

    [Fact]
    public void ResolvePortalCandidate_Bar_accepts_barman_not_chef()
    {
        var chef = new Employee { Role = "Chef", PinCode = "x", EmploymentStatus = "Active", UniqueId = "E1" };
        var barman = new Employee { Role = "Barman", PinCode = "x", EmploymentStatus = "Active", UniqueId = "E2" };
        Assert.Null(StaffPortalAuthentication.ResolvePortalCandidate([chef], "Bar"));
        var resolved = StaffPortalAuthentication.ResolvePortalCandidate([chef, barman], "Bar");
        Assert.NotNull(resolved);
        Assert.Equal("Barman", resolved!.Role);
    }

    [Fact]
    public void ResolvePortalCandidate_Kitchen_accepts_chef_not_barman()
    {
        var chef = new Employee { Role = "Chef", PinCode = "x", EmploymentStatus = "Active", UniqueId = "E1" };
        var barman = new Employee { Role = "Barman", PinCode = "x", EmploymentStatus = "Active", UniqueId = "E2" };
        Assert.Null(StaffPortalAuthentication.ResolvePortalCandidate([barman], "Kitchen"));
        var resolved = StaffPortalAuthentication.ResolvePortalCandidate([chef, barman], "Kitchen");
        Assert.Equal("Chef", resolved!.Role);
    }

    [Fact]
    public void ResolvePortalCandidate_Reception_accepts_front_desk()
    {
        var desk = new Employee { Role = "Front desk", PinCode = "x", EmploymentStatus = "Active", UniqueId = "E1" };
        var resolved = StaffPortalAuthentication.ResolvePortalCandidate([desk], "Reception");
        Assert.NotNull(resolved);
        Assert.Equal("Front desk", resolved!.Role);
    }

    [Theory]
    [InlineData("Server", false)]
    [InlineData("Cashier", false)]
    [InlineData("Admin", true)]
    [InlineData("Manager", true)]
    [InlineData("AdminWeb", false)]
    public void IsAdminDesktopRole_Classifies(string role, bool expected) =>
        Assert.Equal(expected, StaffPortalAuthentication.IsAdminDesktopRole(role));

    [Fact]
    public void ResolvePortalCandidate_Admin_rejects_server_with_same_pin()
    {
        var server = new Employee { Role = "Server", PinCode = "1234", EmploymentStatus = "Active", UniqueId = "E1" };
        var admin = new Employee { Role = "Admin", PinCode = "1234", EmploymentStatus = "Active", UniqueId = "E2" };
        Assert.Null(StaffPortalAuthentication.ResolvePortalCandidate([server], "Admin"));
        var resolved = StaffPortalAuthentication.ResolvePortalCandidate([server, admin], "Admin");
        Assert.NotNull(resolved);
        Assert.Equal("Admin", resolved!.Role);
    }

    [Theory]
    [InlineData("KitchenBar")]
    public void ResolvePortalCandidate_accepts_legacy_kitchen_bar_portal(string portal)
    {
        var chef = new Employee { Role = "Chef", PinCode = "x", EmploymentStatus = "Active", UniqueId = "E1" };
        var resolved = StaffPortalAuthentication.ResolvePortalCandidate([chef], portal);
        Assert.NotNull(resolved);
        Assert.Equal("Chef", resolved!.Role);
    }
}
