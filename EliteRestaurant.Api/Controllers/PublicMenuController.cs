using System.Text.Json;
using EliteRestaurant.Api;
using EliteRestaurant.Api.Branding;
using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Api.Hubs;
using EliteRestaurant.Api.Security;
using EliteRestaurant.Contracts.PublicMenu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Npgsql;
using Serilog;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Staff;
using EliteRestaurant.Core.Menu;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/public/menu")]
[AllowAnonymous]
public sealed class PublicMenuController(
    AppDbContext db,
    IWebHostEnvironment environment,
    IOptions<CurrencyPricingOptions> currencyPricingOptions,
    IHubContext<OrderHub> hubContext,
    JwtTokenService jwtTokenService) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonStoreOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const string OnlinePromoAssetKey = "online-promo";

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
        var name = PublicMenuBrandingMerge.RestaurantDisplayName(cloudSettings, business);
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
        var phone = PublicMenuBrandingMerge.ProfileString(
            cloudSettings is not null,
            cloudSettings?.Phone,
            business.Phone);
        var address = PublicMenuBrandingMerge.ProfileString(
            cloudSettings is not null,
            cloudSettings?.Address,
            business.Address);
        var website = PublicMenuBrandingMerge.ProfileString(
            cloudSettings is not null,
            cloudSettings?.WebsiteDomain,
            business.WebsiteDomain);
        var socialMedia = PublicMenuBrandingMerge.ProfileString(
            cloudSettings is not null,
            cloudSettings?.SocialMedia,
            business.SocialMedia);
        var ticketFooter = PublicMenuBrandingMerge.ProfileString(
            cloudSettings is not null,
            cloudSettings?.TicketFooterText,
            business.TicketFooterText);
        var taxId = PublicMenuBrandingMerge.ProfileString(
            cloudSettings is not null,
            cloudSettings?.TaxIdLegalInfo,
            business.TaxIdLegalInfo);
        var onlinePromoUrl = db.PublicMenuAssets.AsNoTracking()
            .Any(a => a.Key == OnlinePromoAssetKey && a.Content.Length > 0)
            ? "/api/public/menu/assets/online-promo"
            : null;
        var onlineTableId = cloudSettings?.OnlineOrdersTableId;
        var reservationLeadDays = Math.Clamp(SettingsManager.Load().BusinessProfile.ReservationLeadDays, 0, 30);
        var reservationMaxMonthsAhead = Math.Clamp(SettingsManager.Load().BusinessProfile.ReservationMaxMonthsAhead, 1, 24);
        var promoTitle = string.IsNullOrWhiteSpace(cloudSettings?.OnlinePromoTitle)
            ? null
            : cloudSettings!.OnlinePromoTitle.Trim();
        var promoSubtitle = string.IsNullOrWhiteSpace(cloudSettings?.OnlinePromoSubtitle)
            ? null
            : cloudSettings!.OnlinePromoSubtitle.Trim();
        var promoCta = string.IsNullOrWhiteSpace(cloudSettings?.OnlinePromoCtaLabel)
            ? null
            : cloudSettings!.OnlinePromoCtaLabel.Trim();
        var menuTaxonomyJson = !string.IsNullOrWhiteSpace(cloudSettings?.MenuTaxonomyJson)
            ? cloudSettings!.MenuTaxonomyJson!.Trim()
            : MenuTaxonomyHelper.Serialize(MenuTaxonomyHelper.Resolve(all.MenuTaxonomy));
        // Logo URL is always this endpoint; the handler prefers cloud DB blob, then on-disk repo assets/images/logo,
        // then legacy LogoPath.
        return Ok(new PublicMenuConfigDto(
            name,
            "/api/public/menu/assets/logo",
            tagline,
            mode,
            rate,
            tax,
            service,
            phone,
            address,
            website,
            socialMedia,
            ticketFooter,
            taxId,
            onlineTableId,
            reservationLeadDays,
            reservationMaxMonthsAhead,
            promoTitle,
            promoSubtitle,
            promoCta,
            onlinePromoUrl,
            menuTaxonomyJson));
    }


    [HttpPost("staff-login-code")]
    [EnableRateLimiting("PublicMenuDraft")]
    [ProducesResponseType(typeof(StaffLoginCodeResponse), 200)]
    [ProducesResponseType(typeof(StaffLoginCodeResponse), 400)]
    public ActionResult<StaffLoginCodeResponse> ValidateStaffLoginCode([FromBody] StaffLoginCodeRequest request)
        => ValidateStaffLoginCodeValue(request.Code, request.SignInId, request.Pin);

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

    private ActionResult<StaffLoginCodeResponse> ValidateStaffLoginCodeValue(string? code, string? signInId = null, string? pin = null)
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

        var hasTabletId = !string.IsNullOrWhiteSpace(signInId);
        var hasTabletPin = !string.IsNullOrWhiteSpace(pin);
        if (hasTabletId != hasTabletPin)
        {
            return BadRequest(new StaffLoginCodeResponse(false, "Enter both sign-in ID and PIN (or leave both blank for generic staff access)."));
        }

        AuthenticatedStaffSession session;
        if (hasTabletId && hasTabletPin)
        {
            var idMatches = StaffPortalAuthentication
                .QueryActiveEmployeesMatchingStaffId(db.Employees.AsNoTracking(), signInId!)
                .ToList();
            var candidates = StaffPortalAuthentication.FilterPinMatches(idMatches, pin!.Trim());
            var employee = StaffPortalAuthentication.ResolvePortalCandidate(candidates, "elite-menu");
            if (employee is null)
            {
                return BadRequest(new StaffLoginCodeResponse(false, "Unknown staff sign-in ID or incorrect PIN."));
            }

            session = new AuthenticatedStaffSession(
                Token: string.Empty,
                EmployeeId: employee.Id,
                EmployeeUniqueId: string.IsNullOrWhiteSpace(employee.UniqueId) ? "EMP" : employee.UniqueId.Trim(),
                Name: string.IsNullOrWhiteSpace(employee.Name) ? "Staff" : employee.Name.Trim(),
                Role: employee.Role,
                SignInId: string.IsNullOrWhiteSpace(employee.SignInId) ? signInId!.Trim() : employee.SignInId.Trim(),
                Portal: "elite-menu",
                ExpiresAtUtc: DateTime.UtcNow.AddHours(12));
        }
        else
        {
            // Shared passcode only — role is generic "Server"; reservation floor API remains unavailable (CashierOrAdmin policy).
            session = new AuthenticatedStaffSession(
                Token: string.Empty,
                EmployeeId: 0,
                EmployeeUniqueId: "MENU-STAFF",
                Name: "Menu Staff",
                Role: "Server",
                SignInId: "menu-staff",
                Portal: "elite-menu",
                ExpiresAtUtc: DateTime.UtcNow.AddHours(12));
        }

        var jwt = jwtTokenService.CreateToken(session, out var expiresAtUtc);
        return Ok(new StaffLoginCodeResponse(true, null, jwt, expiresAtUtc));
    }

    [HttpGet("products")]
    [EnableRateLimiting("PublicMenuRead")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
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
        // 1) Cloud logo (desktop push) — authoritative when deployed without replacing repo files each time.
        var asset = db.PublicMenuAssets.AsNoTracking().FirstOrDefault(a => a.Key == "logo");
        if (asset is not null && asset.Content.Length > 0)
        {
            var contentType = string.IsNullOrWhiteSpace(asset.ContentType)
                ? "image/png"
                : asset.ContentType;
            return File(asset.Content, contentType);
        }

        // 2) Repository assets/images/logo (see RestaurantWebLogoResolver) — default marketing asset when no cloud blob.
        var repoLogo = RestaurantWebLogoResolver.TryResolveRepoLogoPath(environment);
        if (repoLogo is not null && System.IO.File.Exists(repoLogo))
        {
            var bytes = System.IO.File.ReadAllBytes(repoLogo);
            return File(bytes, RestaurantWebLogoResolver.GetContentTypeForPath(repoLogo));
        }

        // 3) Legacy absolute path from local settings (e.g. desktop-picked file on API host).
        return ServeImageFromPath(SettingsManager.Load().BusinessProfile.LogoPath?.Trim() ?? string.Empty);
    }

    [HttpGet("assets/online-promo")]
    [EnableRateLimiting("PublicMenuRead")]
    public IActionResult GetOnlinePromoHero()
    {
        var asset = db.PublicMenuAssets.AsNoTracking()
            .FirstOrDefault(a => a.Key == OnlinePromoAssetKey);
        if (asset is not { Content.Length: > 0 })
            return NotFound();

        var contentType = string.IsNullOrWhiteSpace(asset.ContentType)
            ? "image/jpeg"
            : asset.ContentType;
        return File(asset.Content, contentType);
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
            foreach (var line in body.Items!)
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

    [HttpPost("orders/submit")]
    [EnableRateLimiting("PublicMenuDraft")]
    [ProducesResponseType(typeof(PublicOrderSubmitResponse), 201)]
    [ProducesResponseType(typeof(PublicMenuDraftErrorDto), 400)]
    public async Task<IActionResult> PostSubmitOrder([FromBody] PublicOrderSubmitRequest? body)
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
            ? await db.Tables.Include(t => t.AssignedServer).FirstOrDefaultAsync(t => t.Id == body.TableId)
            : null;
        if (body.TableId > 0 && table is null)
            errors.Add("Table not found.");
        else if (table is not null
                 && string.Equals(table.Status, "Maintenance", StringComparison.OrdinalIgnoreCase))
            errors.Add("This table is not available for ordering.");
        else if (table is not null && (table.AssignedServerId is null || table.AssignedServer is null))
            errors.Add("This table does not have an assigned server yet.");

        if (body.Items is not null)
        {
            foreach (var line in body.Items!)
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

        var productIds = body.Items!.Select(i => i.ProductId).Distinct().ToList();
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

        var normalized = body.Items
            .GroupBy(i => i.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(x => x.Quantity)))
            .ToList();

        var stockMessage = OrderInventoryDeduction.TryValidateInventoryForProductQuantities(
            db,
            normalized,
            OrderInventoryDeduction.InventoryValidationKind.FullOrder);
        if (stockMessage is not null)
            return BadRequest(new PublicMenuDraftErrorDto { Errors = new[] { stockMessage } });

        var orderTable = table!;
        var activeStaff = await db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active")
            .ToListAsync();

        var notesParts = new List<string> { $"Guest: {name}" };
        if (!string.IsNullOrWhiteSpace(body.Notes))
            notesParts.Add(body.Notes!.Trim());
        var customerNotes = string.Join(" · ", notesParts.Where(s => s.Length > 0));
        var allergyNotes = (body.AllergyNotes ?? string.Empty).Trim();

        var order = new OrderRecord
        {
            UniqueId = UniqueIdGenerator.NewId("ORD"),
            TableId = orderTable.Id,
            TableCode = $"Table {orderTable.TableNumber}",
            TableName = string.IsNullOrWhiteSpace(orderTable.Name) ? $"Table {orderTable.TableNumber}" : orderTable.Name,
            ServerId = orderTable.AssignedServerId,
            ServerName = orderTable.AssignedServer is { } srv ? srv.Name : string.Empty,
            Status = OrderWorkflow.PendingApproval,
            CustomerNotes = customerNotes,
            AllergyNotes = allergyNotes,
            DiscountMode = "None",
            DiscountValue = 0m,
            PaymentCurrencyCode = CurrencyHelper.Usd,
            CreatedAt = DateTime.Now,
            OrderSource = "WalkIn",
            OrderOrigin = OrderOrigin.Online
        };

        foreach (var line in normalized)
        {
            var assignee = OrderSubmissionHelper.ResolveAssignee(dbProducts, activeStaff, line.ProductId);
            order.Items.Add(new OrderItem
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                PreparedByEmployeeId = assignee.EmployeeId,
                PreparedByRole = assignee.Role,
                PreparedByName = assignee.Name
            });
        }

        OrderSubmissionHelper.SyncPaymentFields(order, dbProducts);
        db.Orders.Add(order);
        orderTable.Status = "Occupied";
        DataReconciler.ReconcileTableStatusesWithOrders(db);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            return HandlePublicMenuOrderSaveFailure(ex, "orders/submit");
        }

        await TryNotifyCashierOrderBoardChangedAsync(hubContext, order.Id, "online-order-submitted");

        var code = order.UniqueId;
        return StatusCode(201, new PublicOrderSubmitResponse(
            code,
            order.Id,
            order.Status,
            order.OrderOrigin));
    }

    /// <summary>Guest online order: mixed cart, pickup or delivery, payment intent; <see cref="OrderWorkflow.PendingApproval"/>.</summary>
    [HttpPost("orders/online")]
    [EnableRateLimiting("PublicMenuDraft")]
    [ProducesResponseType(typeof(PublicOrderSubmitResponse), 201)]
    [ProducesResponseType(typeof(PublicMenuDraftErrorDto), 400)]
    [ProducesResponseType(typeof(PublicMenuDraftErrorDto), 503)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    public async Task<IActionResult> PostOnlineOrder([FromBody] PublicOnlineOrderSubmitRequest? body)
    {
        var errors = new List<string>();
        if (body is null)
        {
            errors.Add("Request body is required.");
            return BadRequest(new PublicMenuDraftErrorDto { Errors = errors });
        }

        try
        {
        var name = (body.CustomerName ?? string.Empty).Trim();
        if (name.Length is < 1 or > 60)
            errors.Add("Customer name is required (1–60 characters).");
        if (name.IndexOf('<') >= 0 || name.IndexOf('>') >= 0)
            errors.Add("Customer name may not contain HTML tags.");

        var mode = (body.FulfillmentMode ?? string.Empty).Trim();
        var isDelivery = string.Equals(mode, "Delivery", StringComparison.OrdinalIgnoreCase);
        var isPickup = string.Equals(mode, "Pickup", StringComparison.OrdinalIgnoreCase);
        if (!isDelivery && !isPickup)
            errors.Add("Fulfillment mode must be Pickup or Delivery.");

        if (isDelivery)
        {
            var addr = (body.DeliveryAddress ?? string.Empty).Trim();
            if (addr.Length is < 5 or > 500)
                errors.Add("Delivery address is required (5–500 characters) for delivery orders.");
        }

        if (body.Items is null || body.Items.Count == 0)
            errors.Add("At least one item is required.");

        if (body.Items is not null)
        {
            foreach (var line in body.Items!)
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

        var productIds = body.Items!.Select(i => i.ProductId).Distinct().ToList();
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

        var normalized = body.Items
            .GroupBy(i => i.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(x => x.Quantity)))
            .ToList();

        var stockMessage = OrderInventoryDeduction.TryValidateInventoryForProductQuantities(
            db,
            normalized,
            OrderInventoryDeduction.InventoryValidationKind.FullOrder);
        if (stockMessage is not null)
            return BadRequest(new PublicMenuDraftErrorDto { Errors = new[] { stockMessage } });

        var tableResolution = await ResolveOnlineOrdersTableAsync();
        if (tableResolution.Table is null)
        {
            return BadRequest(new PublicMenuDraftErrorDto
            {
                Errors = new[]
                {
                    tableResolution.ErrorMessage
                        ?? "Online ordering is not available: configure an online orders table in Appearance settings, or add at least one non-maintenance table."
                }
            });
        }

        var orderTable = tableResolution.Table;

        var activeStaff = await db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active")
            .ToListAsync();

        var merchSubtotal = normalized.Sum(x =>
            dbProducts.TryGetValue(x.ProductId, out var p) ? p.Price * x.Quantity : 0m);
        var deliveryFee = isDelivery ? Math.Round(merchSubtotal * 0.20m, 2) : 0m;
        var orderSource = isDelivery ? "Delivery" : "TakeOut";

        var notesParts = new List<string> { $"Guest: {name}", $"Online · {(isDelivery ? "Delivery" : "Pickup")}" };
        if (isDelivery)
        {
            notesParts.Add($"Address: {(body.DeliveryAddress ?? string.Empty).Trim()}");
            var instr = (body.DeliveryInstructions ?? string.Empty).Trim();
            if (instr.Length > 0)
                notesParts.Add($"Instructions: {instr}");
        }

        if (!string.IsNullOrWhiteSpace(body.Notes))
            notesParts.Add(body.Notes!.Trim());

        var payLabel = NormalizeGuestPaymentIntent(body.PaymentMethod);
        notesParts.Add($"Pay: {payLabel}");

        var paymentTiming = string.IsNullOrWhiteSpace(body.PaymentTiming)
            ? OrderPaymentTiming.Deferred
            : body.PaymentTiming!.Trim();
        if (!string.Equals(paymentTiming, OrderPaymentTiming.Deferred, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(paymentTiming, OrderPaymentTiming.Immediate, StringComparison.OrdinalIgnoreCase))
            paymentTiming = OrderPaymentTiming.Deferred;

        var customerNotes = string.Join(" · ", notesParts.Where(s => s.Length > 0));
        var allergyNotes = (body.AllergyNotes ?? string.Empty).Trim();

        var order = new OrderRecord
        {
            UniqueId = UniqueIdGenerator.NewId("ORD"),
            TableId = orderTable.Id,
            TableCode = $"Table {orderTable.TableNumber}",
            TableName = string.IsNullOrWhiteSpace(orderTable.Name) ? $"Table {orderTable.TableNumber}" : orderTable.Name,
            ServerId = null,
            ServerName = string.Empty,
            Status = OrderWorkflow.PendingApproval,
            CustomerNotes = customerNotes,
            AllergyNotes = allergyNotes,
            DiscountMode = "None",
            DiscountValue = 0m,
            PaymentCurrencyCode = CurrencyHelper.Usd,
            CreatedAt = DateTime.Now,
            OrderSource = orderSource,
            OrderOrigin = OrderOrigin.Online,
            DeliveryFeeUsd = deliveryFee,
            PaymentTiming = paymentTiming,
            GuestPaymentMethod = payLabel
        };

        foreach (var line in normalized)
        {
            var assignee = OrderSubmissionHelper.ResolveAssignee(dbProducts, activeStaff, line.ProductId);
            order.Items.Add(new OrderItem
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                PreparedByEmployeeId = assignee.EmployeeId,
                PreparedByRole = assignee.Role,
                PreparedByName = assignee.Name
            });
        }

        OrderSubmissionHelper.SyncPaymentFields(order, dbProducts);
        db.Orders.Add(order);
        orderTable.Status = "Occupied";
        DataReconciler.ReconcileTableStatusesWithOrders(db);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            return HandlePublicMenuOrderSaveFailure(ex, "orders/online");
        }

        await TryNotifyCashierOrderBoardChangedAsync(hubContext, order.Id, "online-order-submitted");

        return StatusCode(201, new PublicOrderSubmitResponse(
            order.UniqueId,
            order.Id,
            order.Status,
            order.OrderOrigin));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error(
                ex,
                "PostOnlineOrder failed outside of SaveChanges (CorrelationId={CorrelationId})",
                HttpContext.Response.Headers["X-Correlation-ID"].ToString());
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Could not place online order",
                detail: "An unexpected error occurred. Please try again or contact the restaurant.");
        }
    }

    private sealed record OnlineOrdersTableResolution(Table? Table, string? ErrorMessage);

    /// <summary>
    /// When <see cref="PublicMenuSetting.OnlineOrdersTableId"/> is set, it must exist and be usable — otherwise guests get a clear 400 (no silent fallback to another table).
    /// </summary>
    private async Task<OnlineOrdersTableResolution> ResolveOnlineOrdersTableAsync()
    {
        var settingsRow = await db.PublicMenuSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "default");

        if (settingsRow?.OnlineOrdersTableId is int configuredId and > 0)
        {
            var configured = await db.Tables
                .Include(x => x.AssignedServer)
                .FirstOrDefaultAsync(x => x.Id == configuredId);

            if (configured is null)
            {
                return new OnlineOrdersTableResolution(null,
                    $"Online ordering is not available: the configured online orders table (id {configuredId}) was not found. Update the online orders table id in Appearance settings or sync tables from the back office.");
            }

            if (string.Equals(configured.Status, "Maintenance", StringComparison.OrdinalIgnoreCase))
            {
                return new OnlineOrdersTableResolution(null,
                    "Online ordering is not available: the configured online orders table is under maintenance. Clear maintenance on that table or choose another online orders table.");
            }

            return new OnlineOrdersTableResolution(configured, null);
        }

        var fallback = await db.Tables
            .AsNoTracking()
            .Where(t => t.Status == null || t.Status.ToLower() != "maintenance")
            .OrderBy(t => t.TableNumber)
            .FirstOrDefaultAsync();

        if (fallback is null)
        {
            return new OnlineOrdersTableResolution(null,
                "Online ordering is not available: add at least one table, or set an online orders table in Appearance settings.");
        }

        return new OnlineOrdersTableResolution(fallback, null);
    }

    private static string NormalizeGuestPaymentIntent(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(s))
            return "Cash";
        if (string.Equals(s, "Cash", StringComparison.OrdinalIgnoreCase))
            return "Cash";
        if (string.Equals(s, "Card", StringComparison.OrdinalIgnoreCase))
            return "Card";
        if (string.Equals(s, "MobileMoney", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "Mobile Money", StringComparison.OrdinalIgnoreCase))
            return "MobileMoney";
        return s;
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

    /// <summary>
    /// Maps common PostgreSQL failures (missing migrations, bad FK data) to a guest-safe message instead of HTTP 500.
    /// </summary>
    private static string? TryTranslatePublicMenuOrderSaveFailure(DbUpdateException ex)
    {
        var pg = FindPostgresException(ex);
        if (pg is null)
            return null;

        return pg.SqlState switch
        {
            "42703" or "42P01" or "42P02" =>
                "Online ordering is temporarily unavailable: the restaurant database needs the latest update. Ask the owner to deploy API migrations, then retry.",
            "23502" =>
                "Online ordering is temporarily unavailable: a required value or column is missing on the restaurant database. Ask the owner to run API database migrations, then retry.",
            "23514" =>
                "Online ordering cannot be completed: the order could not be validated against the restaurant database. Try different items or ask the owner to check data settings.",
            "23503" =>
                "Online ordering cannot be completed: table or staff assignment in the restaurant configuration is invalid. A manager should verify the online orders table and assigned server, then retry.",
            "23505" =>
                "Could not place your order due to a duplicate reference. Please try again.",
            "40001" or "40P01" =>
                "The order could not be saved because the database was busy. Please try again.",
            "08006" or "08003" or "57P01" =>
                "The order could not be saved due to a database connection issue. Please try again in a moment.",
            _ => null
        };
    }

    private static PostgresException? FindPostgresException(Exception? ex)
    {
        if (ex is null)
            return null;
        if (ex is PostgresException pg)
            return pg;
        if (ex is AggregateException agg)
        {
            foreach (var inner in agg.Flatten().InnerExceptions)
            {
                var found = FindPostgresException(inner);
                if (found is not null)
                    return found;
            }
        }

        return FindPostgresException(ex.InnerException);
    }

    private IActionResult HandlePublicMenuOrderSaveFailure(DbUpdateException ex, string endpoint)
    {
        var hint = TryTranslatePublicMenuOrderSaveFailure(ex);
        if (hint is not null)
            return BadRequest(new PublicMenuDraftErrorDto { Errors = new[] { hint } });

        var pg = FindPostgresException(ex);
        if (pg is not null)
        {
            Log.Warning(
                ex,
                "Public menu order save ({Endpoint}): unmapped Postgres SqlState={SqlState} Message={Message}",
                endpoint,
                pg.SqlState,
                pg.MessageText);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new PublicMenuDraftErrorDto
                {
                    Errors = new[]
                    {
                        "We could not save your order right now because of a database error. Please try again shortly, or contact the restaurant if this keeps happening."
                    }
                });
        }

        Log.Error(ex, "Public menu order save ({Endpoint}): DbUpdateException without Postgres inner", endpoint);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Could not save order",
            detail: "An unexpected error occurred while saving. Please try again.");
    }

    private static async Task TryNotifyCashierOrderBoardChangedAsync(
        IHubContext<OrderHub> hubContext,
        int orderId,
        string reason)
    {
        try
        {
            var payload = new { reason, orderId };
            await hubContext.Clients.Group("Cashier").SendAsync("CashierOrderBoardChanged", payload);
            await hubContext.Clients.Group("Server").SendAsync("CashierOrderBoardChanged", payload);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SignalR CashierOrderBoardChanged failed ({Reason}, order {OrderId})", reason, orderId);
        }
    }

    /// <summary>Guest-visible status for an order (kitchen / payment stage + fulfillment line for online).</summary>
    [HttpGet("orders/{orderCode}/status")]
    [EnableRateLimiting("PublicMenuRead")]
    [ProducesResponseType(typeof(PublicOrderStatusDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<PublicOrderStatusDto>> GetOrderStatus(string orderCode)
    {
        var raw = (orderCode ?? string.Empty).Trim();
        if (raw.Length is < 3 or > 80)
            return NotFound();

        var norm = raw.ToUpperInvariant();
        OrderRecord? row = null;
        if (norm.StartsWith("#", StringComparison.Ordinal) && int.TryParse(norm.AsSpan(1), out var legacyId))
        {
            row = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == legacyId);
        }
        else
        {
            row = await db.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.UniqueId.ToUpper() == norm);
        }

        if (row is null)
            return NotFound();

        var code = string.IsNullOrWhiteSpace(row.UniqueId) ? $"#{row.Id:000}" : row.UniqueId;
        var table = string.IsNullOrWhiteSpace(row.TableCode)
            ? null
            : (string.IsNullOrWhiteSpace(row.TableName) ? row.TableCode : $"{row.TableCode} · {row.TableName}");

        string? display = string.IsNullOrWhiteSpace(row.CustomerFulfillmentStatus)
            ? null
            : CustomerFulfillmentStatuses.ToDisplay(row.CustomerFulfillmentStatus);

        return Ok(new PublicOrderStatusDto
        {
            OrderCode = code,
            WorkflowStatus = row.Status,
            CustomerFulfillmentStatus = string.IsNullOrWhiteSpace(row.CustomerFulfillmentStatus) ? null : row.CustomerFulfillmentStatus,
            CustomerFulfillmentDisplay = display,
            TableLabel = string.IsNullOrWhiteSpace(table) ? null : table
        });
    }

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
