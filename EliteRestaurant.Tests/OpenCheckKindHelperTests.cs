using EliteRestaurant.Core.Menu;
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
    public void IsDrinkProduct_Alcohol_section_counts_as_drink_with_default_taxonomy()
    {
        var beer = new Product { Category = "Alcohol", SubCategory = "Beer", Name = "Primus" };
        Assert.True(MenuTaxonomyHelper.IsDrinkProduct(beer));
        Assert.Equal(OpenCheckKindHelper.Drink, OpenCheckKindHelper.GetProductKind(beer));
    }

    [Fact]
    public void TryValidateLinesForCheckKind_allows_alcohol_on_drink_check()
    {
        var beer = new Product { Id = 1, Category = "Alcohol", SubCategory = "Beer", Name = "Primus", Price = 3m };
        var map = new Dictionary<int, Product> { [1] = beer };

        var err = OpenCheckKindHelper.TryValidateLinesForCheckKind(
            OpenCheckKindHelper.Drink,
            map,
            [(1, 1)]);

        Assert.Null(err);
    }

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
    public void TryValidateLinesForCheckKind_allows_mixed_lines_on_mixed_check()
    {
        var drink = new Product { Id = 1, Category = "Drink", Name = "Cola", Price = 2m };
        var food = new Product { Id = 2, Category = "Main", Name = "Steak", Price = 20m };
        var map = new Dictionary<int, Product> { [1] = drink, [2] = food };

        var err = OpenCheckKindHelper.TryValidateLinesForCheckKind(
            OpenCheckKindHelper.Mixed,
            map,
            [(1, 1), (2, 1)]);

        Assert.Null(err);
    }

    [Fact]
    public void TryInferCheckKindFromProducts_returns_Mixed_when_mixed()
    {
        var products = new[]
        {
            new Product { Category = "Drink" },
            new Product { Category = "Main" }
        };
        Assert.Equal(OpenCheckKindHelper.Mixed, OpenCheckKindHelper.TryInferCheckKindFromProducts(products));
    }

    [Fact]
    public void TryInferCheckKindFromLines_returns_Mixed_when_mixed()
    {
        var drink = new Product { Id = 1, Category = "Drink", Name = "Cola", Price = 2m };
        var food = new Product { Id = 2, Category = "Main", Name = "Steak", Price = 20m };
        var map = new Dictionary<int, Product> { [1] = drink, [2] = food };

        var kind = OpenCheckKindHelper.TryInferCheckKindFromLines(map, [(1, 1), (2, 1)]);

        Assert.Equal(OpenCheckKindHelper.Mixed, kind);
    }

    [Fact]
    public void NormalizeCheckKind_accepts_mixed()
    {
        Assert.Equal(OpenCheckKindHelper.Mixed, OpenCheckKindHelper.NormalizeCheckKind("mixed"));
    }
}
