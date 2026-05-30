using EliteRestaurant.Core.Menu;
using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Orders;

/// <summary>Food vs drink classification for open checks (aligned with server portal and public menu).</summary>
public static class OpenCheckKindHelper
{
    public const string Food = "Food";
    public const string Drink = "Drink";

    public static bool IsDrinkCategory(string? category) =>
        MenuTaxonomyHelper.IsLegacyDrinkCategoryForCategoryString(category);

    public static string GetProductKind(Product product, MenuTaxonomySettings? taxonomy = null) =>
        MenuTaxonomyHelper.IsDrinkProduct(product, taxonomy) ? Drink : Food;

    public static string GetProductKind(string? category) =>
        IsDrinkCategory(category) ? Drink : Food;

    /// <summary>Infer check kind from persisted lines. Returns null when empty or mixed food+drink.</summary>
    public static string? TryInferCheckKindFromProducts(
        IEnumerable<Product> products,
        MenuTaxonomySettings? taxonomy = null)
    {
        var sawFood = false;
        var sawDrink = false;
        foreach (var p in products)
        {
            if (MenuTaxonomyHelper.IsDrinkProduct(p, taxonomy))
                sawDrink = true;
            else
                sawFood = true;
            if (sawFood && sawDrink)
                return null;
        }

        if (sawDrink) return Drink;
        if (sawFood) return Food;
        return null;
    }

    /// <summary>Validate new cart lines against an open check kind. Returns guest-safe error or null.</summary>
    public static string? TryValidateLinesForCheckKind(
        string checkKind,
        IReadOnlyDictionary<int, Product> productsById,
        IEnumerable<(int ProductId, int Quantity)> lines,
        MenuTaxonomySettings? taxonomy = null)
    {
        var normalized = NormalizeCheckKind(checkKind);
        if (normalized is null)
            return "Check type must be Food or Drink.";

        foreach (var (productId, quantity) in lines)
        {
            if (quantity <= 0) continue;
            if (!productsById.TryGetValue(productId, out var product))
                continue;
            var lineKind = GetProductKind(product, taxonomy);
            if (!lineKind.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Equals(Drink, StringComparison.OrdinalIgnoreCase)
                    ? "This is a drinks-only check. Remove food items or select a food check."
                    : "This is a food-only check. Remove drink items or select a drinks check.";
            }
        }

        return null;
    }

    public static string? TryInferCheckKindFromLines(
        IReadOnlyDictionary<int, Product> productsById,
        IEnumerable<(int ProductId, int Quantity)> lines,
        MenuTaxonomySettings? taxonomy = null)
    {
        Product? first = null;
        var sawFood = false;
        var sawDrink = false;
        foreach (var (productId, quantity) in lines)
        {
            if (quantity <= 0) continue;
            if (!productsById.TryGetValue(productId, out var product))
                continue;
            first ??= product;
            if (MenuTaxonomyHelper.IsDrinkProduct(product, taxonomy))
                sawDrink = true;
            else
                sawFood = true;
        }

        if (sawFood && sawDrink)
            return null;
        if (sawDrink) return Drink;
        if (sawFood) return Food;
        return first is null ? null : GetProductKind(first, taxonomy);
    }

    public static string? NormalizeCheckKind(string? raw)
    {
        var k = (raw ?? string.Empty).Trim();
        if (k.Equals(Drink, StringComparison.OrdinalIgnoreCase)) return Drink;
        if (k.Equals(Food, StringComparison.OrdinalIgnoreCase)) return Food;
        return null;
    }
}
