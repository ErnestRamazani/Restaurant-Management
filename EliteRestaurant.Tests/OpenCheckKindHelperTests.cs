using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using Xunit;

namespace EliteRestaurant.Tests;

public class OpenCheckKindHelperTests
{
    [Theory]
    [InlineData("Drink", true)]
    [InlineData("Bar", true)]
    [InlineData("Food", false)]
    [InlineData("Main", false)]
    public void IsDrinkCategory_matches_expected(string category, bool isDrink) =>
        Assert.Equal(isDrink, OpenCheckKindHelper.IsDrinkCategory(category));

    [Fact]
    public void TryValidateLinesForCheckKind_rejects_food_on_drink_check()
    {
        var drink = new Product { Id = 1, Category = "Drink", Name = "Cola", Price = 2m };
        var food = new Product { Id = 2, Category = "Main", Name = "Steak", Price = 20m };
        var map = new Dictionary<int, Product> { [1] = drink, [2] = food };

        var err = OpenCheckKindHelper.TryValidateLinesForCheckKind(
            OpenCheckKindHelper.Drink,
            map,
            [(2, 1)]);

        Assert.NotNull(err);
        Assert.Contains("drinks-only", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidateLinesForCheckKind_rejects_drink_on_food_check()
    {
        var drink = new Product { Id = 1, Category = "Drink", Name = "Cola", Price = 2m };
        var food = new Product { Id = 2, Category = "Main", Name = "Steak", Price = 20m };
        var map = new Dictionary<int, Product> { [1] = drink, [2] = food };

        var err = OpenCheckKindHelper.TryValidateLinesForCheckKind(
            OpenCheckKindHelper.Food,
            map,
            [(1, 1)]);

        Assert.NotNull(err);
        Assert.Contains("food-only", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryInferCheckKindFromProducts_returns_null_when_mixed()
    {
        var products = new[]
        {
            new Product { Category = "Drink" },
            new Product { Category = "Main" }
        };
        Assert.Null(OpenCheckKindHelper.TryInferCheckKindFromProducts(products));
    }
}
