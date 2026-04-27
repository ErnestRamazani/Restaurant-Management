using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using Xunit;

namespace EliteRestaurant.Tests;

public class OrderSubmissionHelperTests
{
    [Fact]
    public void ResolveAssignee_Drink_GoesToBarman()
    {
        var products = new Dictionary<int, Product>
        {
            [1] = new Product { Id = 1, Category = "Drink", Name = "Cola" }
        };
        var staff = new List<Employee>
        {
            new() { Id = 10, Role = "Bartender", Name = "Alex" },
            new() { Id = 11, Role = "Chef", Name = "Sam" }
        };
        var a = OrderSubmissionHelper.ResolveAssignee(products, staff, 1);
        Assert.Equal(10, a.EmployeeId);
        Assert.Equal("Barman", a.Role);
    }

    [Fact]
    public void ResolveAssignee_Main_GoesToChef()
    {
        var products = new Dictionary<int, Product>
        {
            [2] = new Product { Id = 2, Category = "Main", Name = "Steak" }
        };
        var staff = new List<Employee>
        {
            new() { Id = 11, Role = "Chef", Name = "Sam" }
        };
        var a = OrderSubmissionHelper.ResolveAssignee(products, staff, 2);
        Assert.Equal(11, a.EmployeeId);
    }
}
