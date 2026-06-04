using EliteRestaurant.Api.Branding;
using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Api.Services;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/kitchen")]
[Authorize(Policy = "KitchenOnly")]
public sealed class KitchenPortalController(
    AppDbContext db,
    IWebHostEnvironment environment,
    PublicMenuSettingsCache menuSettings) : ControllerBase
{
    [HttpGet("config")]
    public ActionResult<KitchenPortalConfigDto> GetConfig()
    {
        var allSettings = SettingsManager.Load();
        var business = allSettings.BusinessProfile;
        var cloudSettings = menuSettings.GetDefault();
        var restaurantName = PublicMenuBrandingMerge.RestaurantDisplayName(cloudSettings, business);
        var logoUrl = "/api/kitchen/assets/restaurant-logo";
        return Ok(new KitchenPortalConfigDto(
            restaurantName,
            logoUrl,
            RestaurantTimeZone.ResolveId(cloudSettings, business)));
    }

    [HttpGet("assets/restaurant-logo")]
    public IActionResult GetRestaurantLogo()
    {
        var repoLogo = RestaurantWebLogoResolver.TryResolveRepoLogoPath(environment);

        var asset = db.PublicMenuAssets.AsNoTracking().FirstOrDefault(a => a.Key == "logo");
        if (asset is { Content.Length: > 0 })
        {
            var contentType = string.IsNullOrWhiteSpace(asset.ContentType)
                ? "image/png"
                : asset.ContentType;
            return File(asset.Content, contentType);
        }

        if (repoLogo is not null && System.IO.File.Exists(repoLogo))
        {
            var bytes = System.IO.File.ReadAllBytes(repoLogo);
            return File(bytes, RestaurantWebLogoResolver.GetContentTypeForPath(repoLogo));
        }

        var logoPath = SettingsManager.Load().BusinessProfile.LogoPath?.Trim() ?? string.Empty;
        return ServeImageFromPath(logoPath);
    }

    /// <summary>Full menu catalog for kitchen/bar: products, recipe ingredients, and on-hand stock.</summary>
    [HttpGet("menu-catalog")]
    public async Task<ActionResult<IReadOnlyList<KitchenMenuProductDto>>> GetMenuCatalog(CancellationToken cancellationToken)
    {
        var products = await db.Products.AsNoTracking()
            .OrderBy(p => p.Category)
            .ThenBy(p => p.SubCategory)
            .ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.UniqueId,
                p.Name,
                p.Category,
                SubCategory = string.IsNullOrWhiteSpace(p.SubCategory) ? "General" : p.SubCategory,
                p.Price,
                p.Description,
                p.Composition,
                p.PrepMinutes
            })
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
            return Ok(Array.Empty<KitchenMenuProductDto>());

        var productIds = products.Select(p => p.Id).ToList();
        var photoKeys = productIds.Select(ProductPhotoAssetKey).ToList();
        var photoKeyPresent = (await db.PublicMenuAssets.AsNoTracking()
            .Where(a => photoKeys.Contains(a.Key) && a.Content.Length > 0)
            .Select(a => a.Key)
            .ToListAsync(cancellationToken)).ToHashSet();

        var ingredientRows = await db.ProductIngredients.AsNoTracking()
            .Where(pi => productIds.Contains(pi.ProductId))
            .Select(pi => new
            {
                pi.ProductId,
                pi.InventoryItemId,
                pi.Quantity,
                Name = pi.InventoryItem!.Name,
                Unit = pi.InventoryItem!.Unit,
                Stock = pi.InventoryItem!.StockQuantity
            })
            .ToListAsync(cancellationToken);

        var ingredientsByProduct = ingredientRows
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = products.Select(p =>
        {
            IReadOnlyList<KitchenMenuIngredientDto> ingredientDtos = ingredientsByProduct.TryGetValue(p.Id, out var lines)
                ? lines.Select(x => new KitchenMenuIngredientDto(
                    x.InventoryItemId,
                    string.IsNullOrWhiteSpace(x.Name) ? $"Inventory #{x.InventoryItemId}" : x.Name.Trim(),
                    (x.Unit ?? string.Empty).Trim(),
                    x.Quantity,
                    x.Stock,
                    x.Stock >= x.Quantity)).ToList()
                : Array.Empty<KitchenMenuIngredientDto>();

            var inStock = ingredientDtos.Count == 0 || ingredientDtos.All(i => i.SufficientForRecipe);
            var photoUrl = photoKeyPresent.Contains(ProductPhotoAssetKey(p.Id))
                ? $"/api/public/menu/assets/product/{p.Id}"
                : null;

            return new KitchenMenuProductDto(
                p.Id,
                string.IsNullOrWhiteSpace(p.UniqueId) ? $"#{p.Id}" : p.UniqueId.Trim(),
                p.Name.Trim(),
                string.IsNullOrWhiteSpace(p.Category) ? "Menu" : p.Category.Trim(),
                p.SubCategory,
                p.Price,
                Math.Max(0, p.PrepMinutes),
                inStock,
                photoUrl,
                string.IsNullOrWhiteSpace(p.Description) ? null : p.Description.Trim(),
                string.IsNullOrWhiteSpace(p.Composition) ? null : p.Composition.Trim(),
                ingredientDtos);
        }).ToList();

        return Ok(rows);
    }

    private static string ProductPhotoAssetKey(int productId) => $"product:{productId}";

    private IActionResult ServeImageFromPath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !System.IO.File.Exists(absolutePath))
            return NotFound();

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(absolutePath, out var contentType))
            contentType = "application/octet-stream";

        var bytes = System.IO.File.ReadAllBytes(absolutePath);
        return File(bytes, contentType);
    }
}
