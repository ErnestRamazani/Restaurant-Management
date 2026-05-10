using System.Text.Json;
using EliteRestaurant.Api.Branding;
using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Api.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/public/menu")]
[AllowAnonymous]
public sealed class PublicMenuController(
    AppDbContext db,
    IWebHostEnvironment environment,
    IOptions<CurrencyPricingOptions> currencyPricingOptions,
    IHubContext<OrderHub> hubContext) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonStoreOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [HttpGet("config")]
    [EnableRateLimiting("PublicMenuRead")]
    [ProducesResponseType(typeof(PublicMenuConfigDto), 200)]
    public ActionResult<PublicMenuConfigDto> GetConfig()
    {
        var all = SettingsManager.Load();
        var business = all.BusinessProfile;
        var pricing = all.CurrencyPricing;
        var cloudSettings = db.PublicMenuSettings.AsNoTracking().FirstOrDefault(s => s.Key == "default");
        var apiPricing = currencyPricingOptions.Value;
        var tax = PricingResolver.ResolveTaxRate(apiPricing.TaxPercent, cloudSettings?.TaxPercent ?? pricing.TaxPercent);
        var service = PricingResolver.ResolveServicePercent(apiPricing.ServicePercent, cloudSettings?.ServicePercent ?? pricing.ServicePercent);
        var name = string.IsNullOrWhiteSpace(cloudSettings?.RestaurantName)
            ? (string.IsNullOrWhiteSpace(business.RestaurantName) ? "Elite Restaurant" : business.RestaurantName.Trim())
            : cloudSettings!.RestaurantName.Trim();
        var mode = string.IsNullOrWhiteSpace(cloudSettings?.DefaultCurrencyDisplayMode)
            ? (string.IsNullOrWhiteSpace(pricing.DefaultCurrencyDisplayMode) ? "Dual" : pricing.DefaultCurrencyDisplayMode.Trim())
            : cloudSettings!.DefaultCurrencyDisplayMode.Trim();
        var rate = cloudSettings?.UsdToFcRate > 0m
            ? cloudSettings.UsdToFcRate
            : (pricing.UsdToFcRate > 0m ? pricing.UsdToFcRate : CurrencyHelper.DefaultFcPerUsd);
        var taglineValue = cloudSettings?.CustomerMenuTagline ?? business.CustomerMenuTagline;
        var tagline = string.IsNullOrWhiteSpace(taglineValue)
            ? null
            : taglineValue.Trim();
        var phone = cloudSettings?.Phone ?? business.Phone;
        var address = cloudSettings?.Address ?? business.Address;
        // Logo URL is always this endpoint; the handler prefers on-disk assets/images/logo, then DB, then LogoPath.
        return Ok(new PublicMenuConfigDto(
            name,
            "/api/public/menu/assets/logo",
            tagline,
            mode,
            rate,
            tax,
            service,
            string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            string.IsNullOrWhiteSpace(address) ? null : address.Trim()));
    }

    [HttpPost("staff-login-code")]
    [EnableRateLimiting("PublicMenuDraft")]
    [ProducesResponseType(typeof(StaffLoginCodeResponse), 200)]
    [ProducesResponseType(typeof(StaffLoginCodeResponse), 400)]
    public ActionResult<StaffLoginCodeResponse> ValidateStaffLoginCode([FromBody] StaffLoginCodeRequest request)
        => ValidateStaffLoginCodeValue(request.Code);

    [HttpPost("staff-login-code/{code}")]
    [EnableRateLimiting("PublicMenuDraft")]
    [ProducesResponseType(typeof(StaffLoginCodeResponse), 200)]
    [ProducesResponseType(typeof(StaffLoginCodeResponse), 400)]
    public ActionResult<StaffLoginCodeResponse> ValidateStaffLoginCodeFromPath(string code)
        => ValidateStaffLoginCodeValue(code);

    [HttpGet("staff-login-code/{code}")]
    [EnableRateLimiting("PublicMenuRead")]
    [ProducesResponseType(typeof(StaffLoginCodeResponse), 200)]
    [ProducesResponseType(typeof(StaffLoginCodeResponse), 400)]
    public ActionResult<StaffLoginCodeResponse> ValidateStaffLoginCodeFromPathGet(string code)
        => ValidateStaffLoginCodeValue(code);

    private ActionResult<StaffLoginCodeResponse> ValidateStaffLoginCodeValue(string? code)
    {
        var configured = db.PublicMenuSettings.AsNoTracking()
                             .Where(s => s.Key == "default")
                             .Select(s => s.StaffLoginPasscode)
                             .FirstOrDefault()
                         ?? SettingsManager.Load().BusinessProfile.StaffLoginPasscode;
        var expected = string.IsNullOrWhiteSpace(configured) ? "er4124" : configured.Trim();
        var submitted = code?.Trim() ?? string.Empty;
        if (!string.Equals(submitted, expected, StringComparison.Ordinal))
        {
            return BadRequest(new StaffLoginCodeResponse(false, "Incorrect staff passcode."));
        }

        return Ok(new StaffLoginCodeResponse(true));
    }

    [HttpGet("products")]
    [EnableRateLimiting("PublicMenuRead")]
    [ProducesResponseType(typeof(IReadOnlyList<PublicProductDto>), 200)]
    public ActionResult<IReadOnlyList<PublicProductDto>> GetProducts()
    {
        var products = db.Products.AsNoTracking()
            .OrderBy(p => p.Category)
            .ThenBy(p => p.SubCategory)
            .ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.UniqueId,
                p.Name,
                p.Category,
                p.SubCategory,
                p.Price,
                p.Description,
                p.Composition
            })
            .ToList();

        var productIds = products.Select(p => p.Id).ToList();
        var photoKeys = productIds.Select(ProductPhotoAssetKey).ToList();
        var photoKeyPresent = db.PublicMenuAssets.AsNoTracking()
            .Where(a => photoKeys.Contains(a.Key) && a.Content.Length > 0)
            .Select(a => a.Key)
            .ToHashSet();
        var ingredientStocks = db.ProductIngredients.AsNoTracking()
            .Where(pi => productIds.Contains(pi.ProductId))
            .Select(pi => new { pi.ProductId, pi.Quantity, Stock = pi.InventoryItem!.StockQuantity })
            .ToList()
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<PublicProductDto>(products.Count);
        foreach (var p in products)
        {
            var inStock = true;
            if (ingredientStocks.TryGetValue(p.Id, out var lines) && lines.Count > 0)
                inStock = lines.All(x => x.Stock >= x.Quantity);

            var sub = string.IsNullOrWhiteSpace(p.SubCategory) ? "General" : p.SubCategory;
            var photoUrl = photoKeyPresent.Contains(ProductPhotoAssetKey(p.Id))
                ? $"/api/public/menu/assets/product/{p.Id}"
                : null;
            rows.Add(new PublicProductDto
            {
                Id = p.Id,
                UniqueId = p.UniqueId,
                Name = p.Name,
                Category = p.Category,
                Subcategory = sub,
                Price = p.Price,
                Description = p.Description,
                Composition = p.Composition,
                PhotoUrl = photoUrl,
                IsAvailable = inStock
            });
        }

        return Ok(rows);
    }

    [HttpGet("assets/logo")]
    [EnableRateLimiting("PublicMenuRead")]
    public IActionResult GetLogo()
    {
        // 1) Repository assets/images/logo (see RestaurantWebLogoResolver) — primary for web when deployed.
        var repoLogo = RestaurantWebLogoResolver.TryResolveRepoLogoPath(environment);
        if (repoLogo is not null && System.IO.File.Exists(repoLogo))
        {
            var bytes = System.IO.File.ReadAllBytes(repoLogo);
            return File(bytes, RestaurantWebLogoResolver.GetContentTypeForPath(repoLogo));
        }

        // 2) Cloud / admin-uploaded logo in DB (desktop "push" profile).
        var asset = db.PublicMenuAssets.AsNoTracking().FirstOrDefault(a => a.Key == "logo");
        if (asset is not null && asset.Content.Length > 0)
        {
            var contentType = string.IsNullOrWhiteSpace(asset.ContentType)
                ? "image/png"
                : asset.ContentType;
            return File(asset.Content, contentType);
        }

        // 3) Legacy absolute path from local settings (e.g. desktop-picked file).
        return ServeImageFromPath(SettingsManager.Load().BusinessProfile.LogoPath?.Trim() ?? string.Empty);
    }

    [HttpGet("assets/product/{id:int}")]
    [EnableRateLimiting("PublicMenuRead")]
    public IActionResult GetProductPhoto(int id)
    {
        var asset = db.PublicMenuAssets.AsNoTracking()
            .FirstOrDefault(a => a.Key == ProductPhotoAssetKey(id));
        if (asset is not { Content.Length: > 0 })
            return NotFound();

        var contentType = string.IsNullOrWhiteSpace(asset.ContentType)
            ? "image/jpeg"
            : asset.ContentType;
        return File(asset.Content, contentType);
    }

    /// <summary>Stored in <c>PublicMenuAssets</c> alongside the logo; upload via API/admin tooling using this key.</summary>
    private static string ProductPhotoAssetKey(int productId) => $"product:{productId}";

    [HttpGet("tables")]
    [EnableRateLimiting("PublicMenuRead")]
    [ProducesResponseType(typeof(IReadOnlyList<PublicTableDto>), 200)]
    public ActionResult<IReadOnlyList<PublicTableDto>> GetTables()
    {
        // Explicit LEFT JOIN — optional navigation inside Select is not always translated to SQL
        // reliably across providers, which can leave AssignedServerName empty in the JSON.
        var rows = (
            from t in db.Tables.AsNoTracking()
            where t.Status != "Maintenance"
                 && (t.Status == "Available" || t.Status == "Occupied")
            join e in db.Employees.AsNoTracking() on t.AssignedServerId equals e.Id into serverGroup
            from e in serverGroup.DefaultIfEmpty()
            orderby t.TableNumber
            select new PublicTableDto
            {
                Id = t.Id,
                TableCode = t.TableNumber.ToString(),
                Name = t.Name,
                Capacity = t.Capacity,
                AssignedServerName = e != null && !string.IsNullOrWhiteSpace(e.Name) ? e.Name.Trim() : null
            }).ToList();
        return Ok(rows);
    }

    [HttpPost("draft")]
    [EnableRateLimiting("PublicMenuDraft")]
    [ProducesResponseType(typeof(PublicMenuDraftSuccessDto), 201)]
    [ProducesResponseType(typeof(PublicMenuDraftErrorDto), 400)]
    public async Task<IActionResult> PostDraft([FromBody] CustomerDraftRequest? body)
    {
        var errors = new List<string>();
        if (body is null)
        {
            errors.Add("Request body is required.");
            return BadRequest(new PublicMenuDraftErrorDto { Errors = errors });
        }

        var name = (body.CustomerName ?? string.Empty).Trim();
        if (name.Length is < 1 or > 60)
            errors.Add("Customer name is required (1–60 characters).");
        if (name.IndexOf('<') >= 0 || name.IndexOf('>') >= 0)
            errors.Add("Customer name may not contain HTML tags.");

        if (body.TableId <= 0)
            errors.Add("A valid table is required.");

        if (body.Items is null || body.Items.Count == 0)
            errors.Add("At least one item is required.");

        var table = body.TableId > 0
            ? await db.Tables.AsNoTracking().FirstOrDefaultAsync(t => t.Id == body.TableId)
            : null;
        if (body.TableId > 0 && table is null)
            errors.Add("Table not found.");
        else if (table is not null
                 && string.Equals(table.Status, "Maintenance", StringComparison.OrdinalIgnoreCase))
            errors.Add("This table is not available for ordering.");

        if (body.Items is not null)
        {
            foreach (var line in body.Items)
            {
                if (line.Quantity is < 1 or > 20)
                {
                    errors.Add("Each line quantity must be between 1 and 20.");
                    break;
                }
            }
        }

        if (errors.Count > 0)
            return BadRequest(new PublicMenuDraftErrorDto { Errors = DeduplicateErrors(errors) });

        var productIds = body!.Items!.Select(i => i.ProductId).Distinct().ToList();

        // Customer drafts use EmployeeId = 0. Table scoping is by SharedOrderDraft.TableId; staff only see a draft
        // when the create-order or server UI selected table matches (see SharedOrderDraftStore.ListServerDrafts).
        const int draftEmployeeId = 0;
        const string draftEmployeeName = "Customer";

        var dbProducts = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var line in body.Items!)
        {
            if (!dbProducts.TryGetValue(line.ProductId, out var p))
            {
                errors.Add($"Product {line.ProductId} was not found.");
                continue;
            }

            if (Math.Abs(line.UnitPrice - p.Price) > 0.02m)
                errors.Add($"Unit price for \"{p.Name}\" does not match the current menu price.");
        }

        if (errors.Count > 0)
            return BadRequest(new PublicMenuDraftErrorDto { Errors = DeduplicateErrors(errors) });

        // Availability (ingredient stock)
        var inStock = await GetAvailabilityMapAsync(productIds);
        foreach (var line in body.Items!)
        {
            if (!inStock.TryGetValue(line.ProductId, out var ok) || !ok)
            {
                errors.Add($"\"{dbProducts[line.ProductId].Name}\" is currently unavailable.");
                break;
            }
        }

        if (errors.Count > 0)
            return BadRequest(new PublicMenuDraftErrorDto { Errors = DeduplicateErrors(errors) });

        var tableCode = table!.TableNumber;
        var kindEmoji = GetOrderKindEmojiForLabel(body!.OrderKind, body.Items!, dbProducts);
        var label = $"{kindEmoji} --- Table {tableCode} --- {name}";
        var json = JsonSerializer.Serialize(body, JsonStoreOptions);
        var now = DateTime.UtcNow;
        var entity = new SharedOrderDraft
        {
            UniqueId = Guid.NewGuid().ToString("N"),
            EmployeeId = draftEmployeeId,
            EmployeeName = draftEmployeeName,
            TableId = table!.Id,
            Portal = SharedOrderDraftStore.ServerPortal,
            DraftLabel = label,
            PayloadJson = json,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.SharedOrderDrafts.Add(entity);
        await db.SaveChangesAsync();

        var prepLines = body.Items
            .Select(i =>
            {
                var p = dbProducts[i.ProductId];
                var sub = string.IsNullOrWhiteSpace(p.SubCategory) ? "General" : p.SubCategory;
                return (i.Quantity, p.Category, sub);
            })
            .ToList();
        var estimatedPrep = OrderPrepTimeEstimator.EstimateTicketPrepMinutes(prepLines);

        var totalUsd = body.Items.Sum(i => i.Quantity * i.UnitPrice);
        await hubContext.Clients.Group("Server").SendAsync("CustomerDraftArrived", new
        {
            draftId = entity.UniqueId,
            label = entity.DraftLabel,
            tableCode = tableCode,
            tableName = table.Name,
            customerName = name,
            itemCount = body.Items.Count,
            totalUsd
        });

        return StatusCode(201, new PublicMenuDraftSuccessDto
        {
            Label = label,
            Message = "Your order has been sent to your server.",
            EstimatedPrepMinutes = estimatedPrep
        });
    }

    private async Task<Dictionary<int, bool>> GetAvailabilityMapAsync(IReadOnlyList<int> productIds)
    {
        if (productIds.Count == 0)
            return new Dictionary<int, bool>();

        var lines = await db.ProductIngredients.AsNoTracking()
            .Where(pi => productIds.Contains(pi.ProductId))
            .Select(pi => new { pi.ProductId, pi.Quantity, Stock = pi.InventoryItem!.StockQuantity })
            .ToListAsync();

        var map = new Dictionary<int, bool>();
        foreach (var id in productIds)
            map[id] = true;

        foreach (var g in lines.GroupBy(x => x.ProductId))
        {
            map[g.Key] = g.All(x => x.Stock >= x.Quantity);
        }

        // Products with no recipe lines: available
        foreach (var id in productIds)
        {
            if (!lines.Any(l => l.ProductId == id))
                map[id] = true;
        }

        return map;
    }

    /// <summary>🍽️ food / 🥤 drink — matches <see cref="CustomerDraftRequest.OrderKind"/> or first item category.</summary>
    private static string GetOrderKindEmojiForLabel(
        string? orderKind,
        List<CustomerDraftItemRequest> items,
        IReadOnlyDictionary<int, Product> dbProducts)
    {
        var ok = orderKind?.Trim();
        if (!string.IsNullOrEmpty(ok))
        {
            if (string.Equals(ok, "drink", StringComparison.OrdinalIgnoreCase)) return "🥤";
            if (string.Equals(ok, "food", StringComparison.OrdinalIgnoreCase)) return "🍽️";
        }
        if (items.Count > 0 && dbProducts.TryGetValue(items[0].ProductId, out var p))
        {
            var c = p.Category?.Trim() ?? string.Empty;
            if (c.Equals("Drink", StringComparison.OrdinalIgnoreCase)
                || c.Equals("Drinks", StringComparison.OrdinalIgnoreCase)
                || c.Equals("Beverage", StringComparison.OrdinalIgnoreCase)
                || c.Equals("Beverages", StringComparison.OrdinalIgnoreCase)
                || c.Equals("Bar", StringComparison.OrdinalIgnoreCase))
                return "🥤";
        }
        return "🍽️";
    }

    private static IReadOnlyList<string> DeduplicateErrors(List<string> errors) =>
        errors.Distinct(StringComparer.Ordinal).ToList();

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
