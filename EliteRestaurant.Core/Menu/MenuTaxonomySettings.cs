namespace EliteRestaurant.Core.Menu;

/// <summary>
/// Configurable menu hierarchy: menu type (e.g. Food / Drink) → section (Product.Category) → leaf items (Product.SubCategory).
/// Serialized in app-settings.json and synced to the public menu API for web clients.
/// </summary>
public sealed class MenuTaxonomySettings
{
    public List<MenuTaxonomyType> Types { get; set; } = new();
}

public sealed class MenuTaxonomyType
{
    /// <summary>Top bucket label in the admin menu (e.g. Food, Drink).</summary>
    public string Name { get; set; } = "Food";

    /// <summary>When true, products are drinks: <see cref="MenuTaxonomySection.Name"/> is the Product.Category (usually Drink) and Items are Product.SubCategory values.</summary>
    public bool IsDrink { get; set; }

    public List<MenuTaxonomySection> Sections { get; set; } = new();
}

public sealed class MenuTaxonomySection
{
    /// <summary>Stored as <see cref="Models.Product.Category"/> for items in this section.</summary>
    public string Name { get; set; } = "Main";

    /// <summary>Allowed <see cref="Models.Product.SubCategory"/> values. When empty, any subcategory under this category name is accepted.</summary>
    public List<string> Items { get; set; } = new();
}
