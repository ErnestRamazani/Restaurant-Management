namespace EliteRestaurant.Api.Dtos;

public sealed record KitchenPortalConfigDto(
    string RestaurantName,
    string RestaurantLogoUrl,
    string RestaurantTimeZoneId);

public sealed record KitchenMenuIngredientDto(
    int InventoryItemId,
    string Name,
    string Unit,
    decimal RecipeQuantity,
    decimal StockQuantity,
    bool SufficientForRecipe);

public sealed record KitchenMenuProductDto(
    int Id,
    string UniqueId,
    string Name,
    string Category,
    string SubCategory,
    decimal Price,
    int PrepMinutes,
    bool InStock,
    string? PhotoUrl,
    string? Description,
    string? Composition,
    IReadOnlyList<KitchenMenuIngredientDto> Ingredients);
