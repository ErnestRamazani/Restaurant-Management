using System.Text.Json;
using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Menu;

public static class MenuTaxonomyHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static MenuTaxonomySettings Resolve(MenuTaxonomySettings? raw)
    {
        if (raw?.Types is not { Count: > 0 })
            return MenuTaxonomyDefaults.CreateEliteDefault();
        return raw;
    }

    /// <summary>Cloud JSON from <see cref="Models.PublicMenuSetting.MenuTaxonomyJson"/> wins over desktop app-settings.</summary>
    public static MenuTaxonomySettings ResolveEffective(string? cloudMenuTaxonomyJson, MenuTaxonomySettings? appTaxonomy = null)
    {
        if (!string.IsNullOrWhiteSpace(cloudMenuTaxonomyJson)
            && TryDeserialize(cloudMenuTaxonomyJson.Trim(), out var cloud))
            return Resolve(cloud);
        return Resolve(appTaxonomy);
    }

    /// <summary>
    /// True when the product belongs to a drink menu type in taxonomy (e.g. Category Alcohol / Non-Alcohol),
    /// or matches legacy drink category names.
    /// </summary>
    public static bool IsDrinkProduct(Product product, MenuTaxonomySettings? taxonomy = null)
    {
        var cat = (product.Category ?? string.Empty).Trim();
        var sub = string.IsNullOrWhiteSpace(product.SubCategory) ? string.Empty : product.SubCategory.Trim();
        foreach (var type in Resolve(taxonomy).Types)
        {
            if (!type.IsDrink)
                continue;
            foreach (var section in type.Sections)
            {
                if (SectionMatchesProduct(cat, sub, section, isDrinkType: true))
                    return true;
            }
        }

        return IsLegacyDrinkCategory(cat);
    }

    /// <summary>Legacy category-only check when taxonomy is unavailable (e.g. category string alone).</summary>
    public static bool IsLegacyDrinkCategoryForCategoryString(string? category) =>
        IsLegacyDrinkCategory((category ?? string.Empty).Trim());

    private static bool IsLegacyDrinkCategory(string category) =>
        category.Equals("Drink", StringComparison.OrdinalIgnoreCase)
        || category.Equals("Drinks", StringComparison.OrdinalIgnoreCase)
        || category.Equals("Beverage", StringComparison.OrdinalIgnoreCase)
        || category.Equals("Beverages", StringComparison.OrdinalIgnoreCase)
        || category.Equals("Bar", StringComparison.OrdinalIgnoreCase);

    public static string Serialize(MenuTaxonomySettings taxonomy) =>
        JsonSerializer.Serialize(Resolve(taxonomy), JsonOptions);

    public static bool TryDeserialize(string? json, out MenuTaxonomySettings? settings)
    {
        settings = null;
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            settings = JsonSerializer.Deserialize<MenuTaxonomySettings>(json.Trim(), JsonOptions);
            return settings?.Types is { Count: > 0 };
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Section name → allowed subcategories (for admin product editor).</summary>
    public static Dictionary<string, List<string>> GetCategoryEditorMap(MenuTaxonomySettings taxonomy)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in Resolve(taxonomy).Types)
        {
            foreach (var section in type.Sections)
            {
                var name = (section.Name ?? string.Empty).Trim();
                if (name.Length == 0)
                    continue;
                if (!map.TryGetValue(name, out var list))
                {
                    list = [];
                    map[name] = list;
                }

                foreach (var item in section.Items)
                {
                    var t = (item ?? string.Empty).Trim();
                    if (t.Length > 0 && !list.Contains(t, StringComparer.OrdinalIgnoreCase))
                        list.Add(t);
                }
            }
        }

        foreach (var (_, list) in map)
        {
            if (list.Count == 0)
                list.Add(string.Empty);
        }

        return map;
    }

    public static IReadOnlyList<string> GetOrderedSectionNames(MenuTaxonomySettings taxonomy)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        foreach (var type in Resolve(taxonomy).Types)
        {
            foreach (var section in type.Sections)
            {
                var n = (section.Name ?? string.Empty).Trim();
                if (n.Length == 0 || !seen.Add(n))
                    continue;
                list.Add(n);
            }
        }

        return list;
    }

    /// <summary>
    /// Whether a product belongs to this taxonomy section. Drink types also match legacy rows whose
    /// <see cref="Product.Category"/> is still <c>Drink</c> when the section lists allowed <see cref="Product.SubCategory"/> values.
    /// </summary>
    public static bool SectionMatchesProduct(string category, string? subCategory, MenuTaxonomySection section, bool isDrinkType)
    {
        var cat = (category ?? string.Empty).Trim();
        var sub = string.IsNullOrWhiteSpace(subCategory) ? string.Empty : subCategory.Trim();
        var sec = (section.Name ?? string.Empty).Trim();
        if (sec.Length == 0)
            return false;

        if (!isDrinkType)
        {
            if (!cat.Equals(sec, StringComparison.OrdinalIgnoreCase))
                return false;
            return section.Items.Count == 0 || MatchesItemList(sub, section.Items);
        }

        if (cat.Equals(sec, StringComparison.OrdinalIgnoreCase))
            return section.Items.Count == 0 || MatchesItemList(sub, section.Items);

        if (cat.Equals("Drink", StringComparison.OrdinalIgnoreCase) &&
            !sec.Equals("Drink", StringComparison.OrdinalIgnoreCase))
            return section.Items.Count > 0 && MatchesItemList(sub, section.Items);

        return false;
    }

    /// <summary>Resolves the configured menu type display name for a product (e.g. Food / Drink).</summary>
    public static bool TryGetTypeNameForProduct(Product product, MenuTaxonomySettings taxonomy, out string typeName)
    {
        typeName = string.Empty;
        var cat = (product.Category ?? string.Empty).Trim();
        var sub = string.IsNullOrWhiteSpace(product.SubCategory) ? string.Empty : product.SubCategory.Trim();
        foreach (var type in Resolve(taxonomy).Types)
        {
            foreach (var section in type.Sections)
            {
                if (!SectionMatchesProduct(cat, sub, section, type.IsDrink))
                    continue;
                typeName = type.Name.Trim();
                return typeName.Length > 0;
            }
        }

        return false;
    }

    public static string GetTypeNameForProductOrFallback(Product product, MenuTaxonomySettings taxonomy)
    {
        if (TryGetTypeNameForProduct(product, taxonomy, out var name))
            return name;
        var cat = (product.Category ?? string.Empty).Trim();
        return cat.Equals("Drink", StringComparison.OrdinalIgnoreCase) ? "Drink" : "Food";
    }

    private static bool MatchesItemList(string subCategory, List<string> items)
    {
        if (items.Count == 0)
            return true;
        if (string.IsNullOrWhiteSpace(subCategory))
            return items.Any(i => string.IsNullOrWhiteSpace(i));
        return items.Any(i => i.Equals(subCategory, StringComparison.OrdinalIgnoreCase));
    }
}
