using System.ComponentModel.DataAnnotations.Schema;

namespace EliteRestaurantPro.Models;

public class InventoryItem
{
    public int Id { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal StockQuantity { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string Notes { get; set; } = string.Empty;
    public ICollection<ProductIngredient> ProductIngredients { get; set; } = new List<ProductIngredient>();

    [NotMapped]
    public int? DaysUntilExpiration
    {
        get
        {
            if (!ExpirationDate.HasValue)
                return null;
            return (ExpirationDate.Value.Date - DateTime.Today).Days;
        }
    }

    [NotMapped]
    public string ExpirationStatus
    {
        get
        {
            var days = DaysUntilExpiration;
            if (!days.HasValue)
                return "No Expiry";
            if (days.Value <= 0)
                return "Expired";
            if (days.Value <= 7)
                return "Critical";
            if (days.Value <= 14)
                return "Bad";
            return "Good";
        }
    }

    [NotMapped]
    public string QuantityStatus
    {
        get
        {
            if (StockQuantity <= 0)
                return "Out";
            if (StockQuantity <= 3)
                return "Critical";
            if (StockQuantity <= 10)
                return "Low";
            return "Healthy";
        }
    }
}
