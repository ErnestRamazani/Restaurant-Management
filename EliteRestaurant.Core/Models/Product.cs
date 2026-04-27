namespace EliteRestaurant.Core.Models;

public class Product
{
    public int Id { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
    public decimal Price { get; set; }

    /// <summary>
    /// Customer-facing description of the dish. Max recommended: 350 characters.
    /// Optional — if null, section is hidden on the public menu.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Customer-facing ingredient names only (comma-separated). For display, not inventory.
    /// </summary>
    public string? Composition { get; set; }

    public ICollection<ProductIngredient> Ingredients { get; set; } = new List<ProductIngredient>();
}
