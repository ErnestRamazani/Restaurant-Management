using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Staff;
using Xunit;

namespace EliteRestaurant.Core.Tests;

public class AdminWebPortalAuthTests
{
    [Fact]
    public void QueryActiveAdminWebPortalCandidates_finds_adminweb_not_admin_only()
    {
        var employees = new List<Employee>
        {
            new()
            {
                SignInId = "er4124",
                Role = "AdminWeb",
                EmploymentStatus = "Active",
                UniqueId = "EMP-SEED-ADMINWEB",
                Name = "Web Admin (seed)"
            },
            new()
            {
                SignInId = "er4124",
                Role = "Admin",
                EmploymentStatus = "Active",
                UniqueId = "EMP-ADM",
                Name = "Other Admin"
            }
        }.AsQueryable();

        var matches = StaffPortalAuthentication.QueryActiveAdminWebPortalCandidates(employees, "er4124").ToList();
        Assert.Single(matches);
        Assert.Equal("AdminWeb", matches[0].Role);
    }
}
