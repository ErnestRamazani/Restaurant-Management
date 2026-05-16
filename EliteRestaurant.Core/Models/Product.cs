using System.ComponentModel.DataAnnotations.Schema;

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
    /// Populated for API/menu payloads (e.g. create-order bundle). Not stored in the database.
    /// When <c>false</c>, the dish cannot be sold for at least one menu unit (ingredient stock).
    /// </summary>
    [NotMapped]
    public bool IsAvailable { get; set; } = true;

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
