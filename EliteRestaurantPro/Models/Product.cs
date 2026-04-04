namespace EliteRestaurantPro.Models;

public class Product
{
    public int Id { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public ICollection<ProductIngredient> Ingredients { get; set; } = new List<ProductIngredient>();
}
