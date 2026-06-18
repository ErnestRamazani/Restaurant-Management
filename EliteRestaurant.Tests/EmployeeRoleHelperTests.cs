using EliteRestaurant.Core.Employees;
using EliteRestaurant.Core.Models;
using Xunit;

namespace EliteRestaurant.Tests;

public sealed class EmployeeRoleHelperTests
{
    [Fact]
    public void DisplayRole_shows_custom_title_for_other()
    {
        var employee = new Employee
        {
            Role = "Other",
            CustomRoleTitle = "Janitor"
        };

        Assert.Equal("Janitor", EmployeeRoleHelper.DisplayRole(employee));
    }

    [Fact]
    public void ResolveSignInIdForSave_keeps_id_for_admin_and_server()
    {
        Assert.Equal("adm01", EmployeeRoleHelper.ResolveSignInIdForSave(false, "adm01", null));
        Assert.Equal("srv1", EmployeeRoleHelper.ResolveSignInIdForSave(false, "srv1", "old"));
        Assert.Equal("kept", EmployeeRoleHelper.ResolveSignInIdForSave(false, "", "kept"));
    }

    [Fact]
    public void ResolveSignInIdForSave_clears_for_other() =>
        Assert.Equal(string.Empty, EmployeeRoleHelper.ResolveSignInIdForSave(true, "janitor-id", "old"));

    [Fact]
    public void IsOtherRole_is_case_insensitive() =>
        Assert.True(EmployeeRoleHelper.IsOtherRole("other"));
}
