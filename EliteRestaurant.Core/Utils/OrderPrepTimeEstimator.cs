using System.Linq;

namespace EliteRestaurant.Core.Utils;

public static class OrderPrepTimeEstimator
{
    public static int MinutesForLineItem(string category, string subCategory)
    {
        var minutes = category switch
        {
            "Drink" => 3,
            "Starter/Appetizer" => 8,
            "Main" => 16,
            "Dessert" => 6,
            _ => 10
        };

        if (subCategory.Equals("Cocktail", StringComparison.OrdinalIgnoreCase))
            minutes += 2;
        if (subCategory.Equals("Seafood", StringComparison.OrdinalIgnoreCase))
            minutes += 3;
        if (subCategory.Equals("Meat Meal", StringComparison.OrdinalIgnoreCase))
            minutes += 4;
        if (subCategory.Equals("Pasta", StringComparison.OrdinalIgnoreCase))
            minutes += 2;
        return minutes;
    }

    /// <summary>Parallel prep model: max item time plus small bump for concurrent items.</summary>
    public static int EstimateTicketPrepMinutes(IReadOnlyList<(int Quantity, string Category, string SubCategory)> lines)
    {
        if (lines.Count == 0)
            return 0;
        var prep = lines.SelectMany(t => Enumerable.Repeat(MinutesForLineItem(t.Category, t.SubCategory), t.Quantity)).ToList();
        return prep.Max() + Math.Min(10, Math.Max(0, prep.Count - 1));
    }
}
