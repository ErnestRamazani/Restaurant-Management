// PRICING PRECEDENCE (aligned with PublicMenuController):
// 1. API appsettings.json (CurrencyPricing section) — explicit operator override when values are positive.
// 2. PublicMenuSettings row (Key=default) — cloud profile from POST api/admin/settings/cloud-profile (desktop push).
// 3. App file settings (SettingsManager / app-settings.json) — local fallback when cloud fields are unset.
// See PricingPrecedenceTests for tax/service matrix when cloud row is absent.
//
// RESTAURANT LOGO (same order as /api/public/menu/assets/logo — see RestaurantWebLogoResolver remarks):
// 1. PublicMenuAssets Key=logo (cloud upload / desktop push); 2. on-disk repo assets/images/logo; 3. BusinessProfile.LogoPath.
using EliteRestaurant.Api.Branding;
using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Api.Security;
using EliteRestaurant.Api.Services;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Menu;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Tenancy;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Staff;
using EliteRestaurant.Core.Utils;
using EliteRestaurant.Api.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/server")]
[Authorize(Policy = "StaffAny")]
public sealed class     ServerPortalController(
    TabletAuthService authService,
    ITenantContext tenant,
    IWebHostEnvironment environment,
    IOptions<CurrencyPricingOptions> currencyPricingOptions,
    AppDbContext db,
    IHubContext<OrderHub> orderHub) : ControllerBase
{
    [HttpGet("config")]
    public ActionResult<ServerPortalConfigDto> GetConfig()
    {
        var session = RequireServerOrCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token. Requires Server, Cashier, or Reception portal login." });

        var allSettings = SettingsManager.Load();
        var settings = allSettings.CurrencyPricing;
        var business = allSettings.BusinessProfile;
        var cloudSettings = db.PublicMenuSettings.AsNoTracking().FirstOrDefault(s => s.Key == "default");
        var restaurantName = PublicMenuBrandingMerge.RestaurantDisplayName(cloudSettings, business);
        var logoUrl = "/api/server/assets/restaurant-logo";
        var employeePhotoUrl = "/api/server/assets/me-photo";
        var apiPricing = currencyPricingOptions.Value;
        var effectiveTax = cloudSettings?.TaxPercent ?? settings.TaxPercent;
        var effectiveService = cloudSettings?.ServicePercent ?? settings.ServicePercent;
        var taxPercent = PricingResolver.ResolveTaxRate(apiPricing.TaxPercent, effectiveTax);
        var servicePercent = PricingResolver.ResolveServicePercent(apiPricing.ServicePercent, effectiveService);
        var displayMode = string.IsNullOrWhiteSpace(cloudSettings?.DefaultCurrencyDisplayMode)
            ? (string.IsNullOrWhiteSpace(settings.DefaultCurrencyDisplayMode) ? "Dual" : settings.DefaultCurrencyDisplayMode.Trim())
            : cloudSettings!.DefaultCurrencyDisplayMode.Trim();
        var usdToFc = cloudSettings?.UsdToFcRate > 0m
            ? cloudSettings.UsdToFcRate
            : (settings.UsdToFcRate > 0m ? settings.UsdToFcRate : CurrencyHelper.DefaultFcPerUsd);

        var menuTaxonomyJson = !string.IsNullOrWhiteSpace(cloudSettings?.MenuTaxonomyJson)
            ? cloudSettings!.MenuTaxonomyJson!.Trim()
            : MenuTaxonomyHelper.Serialize(MenuTaxonomyHelper.Resolve(allSettings.MenuTaxonomy));

        return Ok(new ServerPortalConfigDto(
            restaurantName,
            logoUrl,
            employeePhotoUrl,
            displayMode,
            usdToFc,
            taxPercent,
            servicePercent,
            menuTaxonomyJson));
    }

    [HttpGet("assets/restaurant-logo")]
    public IActionResult GetRestaurantLogo()
    {
        var session = RequireServerOrCashierSession();
        if (session is null)
            return Unauthorized();

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

    [HttpGet("assets/me-photo")]
    public IActionResult GetEmployeePhoto()
    {
        var session = RequireServerOrCashierSession();
        if (session is null)
            return Unauthorized();

        var photoPath = db.Employees.AsNoTracking()
            .Where(e => e.Id == session.EmployeeId)
            .Select(e => e.ProfileImagePath)
            .FirstOrDefault();
        return ServeImageFromPath(photoPath?.Trim() ?? string.Empty);
    }

    [HttpGet("products")]
    public ActionResult<IReadOnlyList<ServerProductDto>> GetProducts()
    {
        var session = RequireServerOrCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token. Requires Server or Cashier portal login." });

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
                SubCategory = string.IsNullOrWhiteSpace(p.SubCategory) ? "General" : p.SubCategory,
                p.Price,
                p.Description,
                p.Composition,
                p.PrepMinutes
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

        var rows = products.Select(p =>
        {
            var inStock = true;
            if (ingredientStocks.TryGetValue(p.Id, out var lines) && lines.Count > 0)
                inStock = lines.All(x => x.Stock >= x.Quantity);
            var photoUrl = photoKeyPresent.Contains(ProductPhotoAssetKey(p.Id))
                ? $"/api/public/menu/assets/product/{p.Id}"
                : null;
            return new ServerProductDto(
                p.Id,
                p.UniqueId,
                p.Name,
                p.Category,
                p.SubCategory,
                p.Price,
                inStock,
                photoUrl,
                string.IsNullOrWhiteSpace(p.Description) ? null : p.Description.Trim(),
                string.IsNullOrWhiteSpace(p.Composition) ? null : p.Composition.Trim(),
                Math.Max(0, p.PrepMinutes));
        }).ToList();

        return Ok(rows);
    }

    [HttpGet("open-check")]
    public ActionResult GetOpenCheck([FromQuery] int tableId)
    {
        var err = TryAuthorizeTableForOpenChecks(tableId, out var table);
        if (err is not null)
            return err;

        var open = db.Orders.AsNoTracking()
            .WhereOpenCheckForTable(tableId)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        if (open is null)
            return Ok(new { hasOpenCheck = false });

        var orderLabel = string.IsNullOrWhiteSpace(open.UniqueId) ? $"#{open.Id:000}" : open.UniqueId;
        return Ok(new
        {
            hasOpenCheck = true,
            orderId = open.Id,
            orderCode = orderLabel,
            status = open.Status,
            createdAt = open.CreatedAt
        });
    }

    [HttpGet("tables/{tableId:int}/open-checks")]
    public ActionResult<ServerOpenChecksResponse> GetOpenChecksForTable(int tableId)
    {
        var err = TryAuthorizeTableForOpenChecks(tableId, out _);
        if (err is not null)
            return err;

        var orders = db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .WhereOpenCheckForTable(tableId)
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        var menuTaxonomy = LoadMenuTaxonomy();
        var checks = orders.Select(o => MapOpenCheckDto(o, menuTaxonomy)).ToList();
        return Ok(new ServerOpenChecksResponse(tableId, checks));
    }

    [HttpGet("drafts")]
    public ActionResult<IReadOnlyList<ServerDraftDto>> GetDrafts([FromQuery] int tableId = 0)
    {
        var session = RequireServerOrCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token. Requires Server or Cashier portal login." });

        var restrict = session.Role.Equals("Server", StringComparison.OrdinalIgnoreCase);
        try
        {
            var rows = SharedOrderDraftStore.ListServerDrafts(session.EmployeeId, tableId, restrict)
                .Select(d => new ServerDraftDto(
                    d.Id,
                    d.Label,
                    d.PayloadJson,
                    d.UpdatedAtUtc,
                    d.TableId,
                    d.IsCustomerDraft))
                .ToList();
            return Ok(rows);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Draft storage read failed.", detail = ex.Message });
        }
    }

    [HttpGet("drafts/{draftId}")]
    public ActionResult<ServerDraftDto> GetDraft(string draftId)
    {
        var session = RequireServerOrCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token. Requires Server or Cashier portal login." });

        var restrict = session.Role.Equals("Server", StringComparison.OrdinalIgnoreCase);
        try
        {
            var row = SharedOrderDraftStore.GetServerDraft(session.EmployeeId, draftId, restrict);
            if (row is null)
                return NotFound(new { message = "Draft not found for this server." });

            return Ok(new ServerDraftDto(
                row.Id,
                row.Label,
                row.PayloadJson,
                row.UpdatedAtUtc,
                row.TableId,
                row.IsCustomerDraft));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Draft storage read failed.", detail = ex.Message });
        }
    }

    [HttpPost("drafts")]
    public ActionResult<ServerDraftDto> SaveDraft([FromBody] ServerSaveDraftRequest request)
    {
        var session = RequireServerOrCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token. Requires Server or Cashier portal login." });

        var snapshot = request.SnapshotJson?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(snapshot))
            return BadRequest(new { message = "SnapshotJson is required." });

        try
        {
            using var _ = JsonDocument.Parse(snapshot);
        }
        catch
        {
            return BadRequest(new { message = "SnapshotJson must be valid JSON." });
        }

        try
        {
            var tableId = SharedOrderDraftStore.ParseTableIdFromSnapshotJson(snapshot);
            var saved = SharedOrderDraftStore.SaveServerDraft(
                session.EmployeeId,
                session.Name,
                request.Label ?? string.Empty,
                snapshot,
                tableId);

            return Ok(new ServerDraftDto(
                saved.Id,
                saved.Label,
                saved.PayloadJson,
                saved.UpdatedAtUtc,
                saved.TableId,
                saved.IsCustomerDraft));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Draft save failed.", detail = ex.Message });
        }
    }

    [HttpDelete("drafts/{draftId}")]
    public ActionResult DeleteDraft(string draftId, [FromQuery] int tableId = 0)
    {
        var session = RequireServerOrCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token. Requires Server or Cashier portal login." });

        var restrict = session.Role.Equals("Server", StringComparison.OrdinalIgnoreCase);
        try
        {
            var deleted = SharedOrderDraftStore.DeleteServerDraft(session.EmployeeId, draftId, tableId, restrict);
            if (!deleted)
                return NotFound(new { message = "Draft not found for this server." });

            return Ok(new { ok = true, id = draftId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Draft delete failed.", detail = ex.Message });
        }
    }

    [HttpPost("orders")]
    public async Task<ActionResult<ServerCreateOrderResponse>> CreateOrAppendOrder([FromBody] ServerCreateOrderRequest request)
    {
        var session = RequireServerOrCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token. Requires Server or Cashier portal login." });

        if (request.TableId <= 0)
            return BadRequest(new { message = "TableId is required." });
        if (request.Lines is null || request.Lines.Count == 0)
            return BadRequest(new { message = "At least one line is required." });

        var source = (request.OrderSource ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(source))
            source = "WalkIn";
        var isDelivery = source.Equals("Delivery", StringComparison.OrdinalIgnoreCase);
        if (isDelivery && string.IsNullOrWhiteSpace(request.SourceReference))
            return BadRequest(new { message = "Delivery requires a source reference." });
        if (!isDelivery && !source.Equals("WalkIn", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only WalkIn and Delivery order sources are supported in Server portal." });

        var discountMode = string.IsNullOrWhiteSpace(request.DiscountMode) ? "None" : request.DiscountMode.Trim();
        var discountValue = request.DiscountValue;
        if (discountMode.Equals("Percent", StringComparison.OrdinalIgnoreCase)
            && (discountValue < 0m || discountValue > 100m))
            return BadRequest(new { message = "Percent discount must be between 0 and 100." });
        if (discountMode.Equals("Usd", StringComparison.OrdinalIgnoreCase) && discountValue < 0m)
            return BadRequest(new { message = "USD discount cannot be negative." });
        if (!discountMode.Equals("None", StringComparison.OrdinalIgnoreCase)
            && !discountMode.Equals("Percent", StringComparison.OrdinalIgnoreCase)
            && !discountMode.Equals("Usd", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Discount mode must be None, Percent or Usd." });

        var normalizedLines = request.Lines
            .Where(l => l is not null && l.ProductId > 0 && l.Quantity > 0)
            .GroupBy(l => l.ProductId)
            .Select(g => new ServerOrderLineRequest(g.Key, g.Sum(x => x.Quantity)))
            .ToList();

        if (normalizedLines.Count == 0)
            return BadRequest(new { message = "No valid lines provided." });

        var table = db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == request.TableId);
        if (table is null)
            return NotFound(new { message = "Table not found." });
        if (session.Role.Equals("Server", StringComparison.OrdinalIgnoreCase))
        {
            if (table.AssignedServerId != session.EmployeeId)
                return BadRequest(new { message = "This table is not assigned to the logged-in server." });
        }
        else
        {
            if (table.AssignedServerId is null || table.AssignedServer is null)
                return BadRequest(new { message = "Table must have an assigned server before placing orders." });
        }

        if (string.Equals(table.Status, "Maintenance", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Maintenance table cannot receive orders." });

        var ticketServerId = session.Role.Equals("Server", StringComparison.OrdinalIgnoreCase)
            ? session.EmployeeId
            : table.AssignedServerId!.Value;
        var ticketServerName = session.Role.Equals("Server", StringComparison.OrdinalIgnoreCase)
            ? session.Name
            : table.AssignedServer!.Name;

        var productIds = normalizedLines.Select(l => l.ProductId).Distinct().ToList();
        var products = db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p);
        if (products.Count != productIds.Count)
            return BadRequest(new { message = "One or more products no longer exist." });

        var activeStaff = db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active")
            .ToList();

        var menuTaxonomy = LoadMenuTaxonomy();

        OrderRecord? openOrder = null;
        if (request.AppendToOpenCheck)
        {
            if (request.OpenOrderId is int openId && openId > 0)
            {
                openOrder = db.Orders.Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                    .SingleOrDefault(o => o.Id == openId && o.TableId == table.Id);
                if (openOrder is null || !OrderWorkflow.IsOpenCheckStatus(openOrder.Status))
                    return BadRequest(new { message = "Selected open check was closed or moved. Refresh and try again." });
            }
            else
            {
                openOrder = db.Orders.Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                    .WhereOpenCheckForTable(table.Id)
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefault();
            }
        }

        var newItems = BuildOrderItems(normalizedLines, products, activeStaff);

        if (request.AppendToOpenCheck && openOrder is not null)
        {
            var existingProducts = openOrder.Items
                .Select(i => products.TryGetValue(i.ProductId, out var ep) ? ep : i.Product)
                .Where(p => p is not null)
                .Cast<Product>()
                .ToList();
            var checkKind = OpenCheckKindHelper.TryInferCheckKindFromProducts(existingProducts, menuTaxonomy);
            if (checkKind is null)
                return BadRequest(new { message = "This open check mixes food and drinks. Start a new food or drink ticket instead." });

            var kindErr = OpenCheckKindHelper.TryValidateLinesForCheckKind(
                checkKind,
                products,
                normalizedLines.Select(l => (l.ProductId, l.Quantity)),
                menuTaxonomy);
            if (kindErr is not null)
                return BadRequest(new { message = kindErr });
        }
        else if (!request.AppendToOpenCheck)
        {
            var newKind = OpenCheckKindHelper.NormalizeCheckKind(request.NewCheckKind)
                ?? OpenCheckKindHelper.TryInferCheckKindFromLines(
                    products,
                    normalizedLines.Select(l => (l.ProductId, l.Quantity)),
                    menuTaxonomy);
            if (newKind is null)
                return BadRequest(new { message = "Choose Food or Drink for a new ticket, or send only food or only drink items." });

            var newKindErr = OpenCheckKindHelper.TryValidateLinesForCheckKind(
                newKind,
                products,
                normalizedLines.Select(l => (l.ProductId, l.Quantity)),
                menuTaxonomy);
            if (newKindErr is not null)
                return BadRequest(new { message = newKindErr });
        }

        IReadOnlyList<(int ProductId, int Quantity)> linesToValidate;
        OrderInventoryDeduction.InventoryValidationKind validationKind;
        if (request.AppendToOpenCheck && openOrder is not null)
        {
            if (OrderWorkflow.IsPendingCashier(openOrder.Status) || OrderWorkflow.IsPendingApproval(openOrder.Status))
            {
                linesToValidate = MergeOrderItemsWithNewLines(openOrder.Items, normalizedLines);
                validationKind = OrderInventoryDeduction.InventoryValidationKind.FullOrder;
            }
            else
            {
                linesToValidate = normalizedLines.Select(l => (l.ProductId, l.Quantity)).ToList();
                validationKind = OrderInventoryDeduction.InventoryValidationKind.AdditionalLinesOnly;
            }
        }
        else
        {
            linesToValidate = normalizedLines.Select(l => (l.ProductId, l.Quantity)).ToList();
            validationKind = OrderInventoryDeduction.InventoryValidationKind.FullOrder;
        }

        var inventoryError = OrderInventoryDeduction.TryValidateInventoryForProductQuantities(db, linesToValidate, validationKind);
        if (inventoryError is not null)
            return BadRequest(new { message = inventoryError });

        if (request.AppendToOpenCheck && openOrder is not null)
        {
            foreach (var item in newItems)
                openOrder.Items.Add(item);

            if (!string.IsNullOrWhiteSpace(request.CustomerNotes))
            {
                openOrder.CustomerNotes = string.IsNullOrWhiteSpace(openOrder.CustomerNotes)
                    ? request.CustomerNotes.Trim()
                    : $"{openOrder.CustomerNotes.Trim()}\n{request.CustomerNotes.Trim()}";
            }

            if (!string.IsNullOrWhiteSpace(request.AllergyNotes))
            {
                openOrder.AllergyNotes = string.IsNullOrWhiteSpace(openOrder.AllergyNotes)
                    ? request.AllergyNotes.Trim()
                    : $"{openOrder.AllergyNotes.Trim()}\n{request.AllergyNotes.Trim()}";
            }

            if (string.Equals(openOrder.Status, "Ready", StringComparison.OrdinalIgnoreCase)
                || string.Equals(openOrder.Status, OrderWorkflow.Served, StringComparison.OrdinalIgnoreCase)
                || OrderWorkflow.IsKitchenQueueStatus(openOrder.Status))
            {
                openOrder.Status = OrderWorkflow.PendingCashier;
            }

            // Preserve existing discount when appending unless caller sends a meaningful non-None discount.
            var explicitDiscount = !discountMode.Equals("None", StringComparison.OrdinalIgnoreCase)
                && (discountMode.Equals("Percent", StringComparison.OrdinalIgnoreCase)
                    ? discountValue > 0m && discountValue <= 100m
                    : discountMode.Equals("Usd", StringComparison.OrdinalIgnoreCase) && discountValue > 0m);

            if (explicitDiscount)
            {
                openOrder.DiscountMode = discountMode;
                openOrder.DiscountValue = discountValue;
            }
            openOrder.OrderSource = isDelivery ? "Delivery" : "WalkIn";
            openOrder.OrderOrigin = isDelivery ? OrderOrigin.Online : OrderOrigin.InStore;
            openOrder.ReservationGuestName = isDelivery ? request.SourceReference.Trim() : string.Empty;
            openOrder.PaymentCurrencyCode = CurrencyHelper.NormalizeCurrencyCode(request.PaymentCurrencyCode);
            var appendMerch = openOrder.Items.Sum(i =>
                products.TryGetValue(i.ProductId, out var p) ? p.Price * i.Quantity : 0m);
            openOrder.DeliveryFeeUsd = isDelivery ? Math.Round(appendMerch * 0.20m, 2) : 0m;
            OrderSubmissionHelper.SyncPaymentFields(openOrder, products);
            table.Status = "Occupied";
            DataReconciler.ReconcileTableStatusesWithOrders(db);
            db.SaveChanges();

            if (OrderWorkflow.IsPendingCashier(openOrder.Status) || OrderWorkflow.IsPendingApproval(openOrder.Status))
            {
                await OrderHubBroadcasts.NotifyCashierOrderBoardChangedAsync(
                    orderHub, db, openOrder.Id, "server-order-submitted");
            }

            var code = string.IsNullOrWhiteSpace(openOrder.UniqueId) ? $"#{openOrder.Id:000}" : openOrder.UniqueId;
            return Ok(new ServerCreateOrderResponse(
                Mode: "Append",
                OrderId: code,
                LinesAdded: newItems.Count,
                Message: $"Added {newItems.Count} line(s) to open check {code}. Sent back to cashier for release to kitchen.",
                CreatedAtUtc: DateTime.UtcNow));
        }

        var order = new OrderRecord
        {
            UniqueId = UniqueIdGenerator.NewId("ORD"),
            TableId = table.Id,
            TableCode = $"Table {table.TableNumber}",
            TableName = string.IsNullOrWhiteSpace(table.Name) ? $"Table {table.TableNumber}" : table.Name,
            ServerId = ticketServerId,
            ServerName = ticketServerName,
            Status = OrderWorkflow.PendingCashier,
            CustomerNotes = (request.CustomerNotes ?? string.Empty).Trim(),
            AllergyNotes = (request.AllergyNotes ?? string.Empty).Trim(),
            DiscountMode = discountMode,
            DiscountValue = discountValue,
            PaymentCurrencyCode = CurrencyHelper.NormalizeCurrencyCode(request.PaymentCurrencyCode),
            CreatedAt = DateTime.Now,
            OrderSource = isDelivery ? "Delivery" : "WalkIn",
            OrderOrigin = isDelivery ? OrderOrigin.Online : OrderOrigin.InStore,
            ReservationGuestName = isDelivery ? request.SourceReference.Trim() : string.Empty
        };

        foreach (var item in newItems)
            order.Items.Add(item);

        var merchSubtotal = order.Items.Sum(i =>
            products.TryGetValue(i.ProductId, out var p) ? p.Price * i.Quantity : 0m);
        order.DeliveryFeeUsd = isDelivery ? Math.Round(merchSubtotal * 0.20m, 2) : 0m;

        OrderSubmissionHelper.SyncPaymentFields(order, products);
        db.Orders.Add(order);
        table.Status = "Occupied";
        DataReconciler.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();

        await OrderHubBroadcasts.NotifyCashierOrderBoardChangedAsync(
            orderHub, db, order.Id, "server-order-submitted");

        return Ok(new ServerCreateOrderResponse(
            Mode: "Create",
            OrderId: order.UniqueId,
            LinesAdded: newItems.Count,
            Message: $"Order {order.UniqueId} sent to cashier.",
            CreatedAtUtc: DateTime.UtcNow));
    }

    [HttpGet("table-calls")]
    public ActionResult<ServerTableCallsBoardDto> GetTableCalls()
    {
        var session = RequireServerSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-server role." });

        if (!tenant.IsResolved)
            return BadRequest(new { message = "Restaurant site could not be resolved." });

        var pendingCalls = TableServerCallQueue.ListForServer(tenant.RestaurantId, session.EmployeeId, pendingOnly: true);
        var pendingByTable = pendingCalls
            .GroupBy(c => c.TableId)
            .ToDictionary(
                g => g.Key,
                g => ToTableCallRow(g.OrderByDescending(c => c.CalledAtUtc).First()));

        var assignedTables = db.Tables.AsNoTracking()
            .Where(t => t.AssignedServerId == session.EmployeeId)
            .OrderBy(t => t.TableNumber)
            .Select(t => new { t.Id, t.TableNumber, Name = t.Name ?? string.Empty })
            .ToList();

        var tableIds = assignedTables.Select(t => t.Id).ToList();
        var ordersByTable = new Dictionary<int, List<OrderRecord>>();
        if (tableIds.Count > 0)
        {
            var orders = db.Orders.AsNoTracking()
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .Where(o => o.TableId != null && tableIds.Contains(o.TableId.Value))
                .Where(o => o.ServerId == session.EmployeeId)
                .Where(o =>
                    o.Status == OrderWorkflow.PendingCashier
                    || o.Status == OrderWorkflow.PendingApproval
                    || o.Status == "Waiting"
                    || o.Status == "In Kitchen"
                    || o.Status == "Ready"
                    || o.Status == OrderWorkflow.Served)
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            ordersByTable = orders
                .GroupBy(o => o.TableId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        var mappedByTable = MapTableCallOrdersByTable(ordersByTable, LoadMenuTaxonomy());

        var boardRows = new List<ServerTableBoardRowDto>();
        foreach (var table in assignedTables)
        {
            pendingByTable.TryGetValue(table.Id, out var pendingCall);
            mappedByTable.TryGetValue(table.Id, out var tableOrders);
            var orders = tableOrders ?? [];
            if (pendingCall is null && orders.Count == 0)
                continue;

            boardRows.Add(new ServerTableBoardRowDto(
                table.Id,
                table.TableNumber,
                table.Name.Trim(),
                pendingCall,
                orders));
        }

        return Ok(new ServerTableCallsBoardDto(boardRows, pendingCalls.Count));
    }

    [HttpPost("table-calls/{callId:guid}/accept")]
    public async Task<ActionResult<ServerTableCallAcceptResponse>> AcceptTableCall(Guid callId)
    {
        var session = RequireServerSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-server role." });
        if (callId == Guid.Empty)
            return BadRequest(new { message = "callId is required." });

        if (!TableServerCallQueue.TryGet(callId, out var existing) || existing is null)
            return NotFound(new { message = "Call not found." });
        if (existing.AssignedServerId is > 0 && existing.AssignedServerId != session.EmployeeId)
            return Forbid();

        if (!TableServerCallQueue.TryAccept(callId, session.EmployeeId, out var accepted) || accepted is null)
            return BadRequest(new { message = "This call was already accepted." });

        await OrderHubBroadcasts.NotifyServerTableCallQueueChangedAsync(orderHub, "accepted", callId);

        return Ok(new ServerTableCallAcceptResponse(true, "Call accepted.", callId));
    }

    [HttpGet("ongoing-orders")]
    public ActionResult<IReadOnlyList<ServerOngoingOrderDto>> GetOngoingOrders()
    {
        var session = RequireServerSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-server role." });

        var completedSince = DateTime.UtcNow.AddHours(-24);
        var orders = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.ServerId == session.EmployeeId)
            .Where(o =>
                o.Status == "Waiting"
                || o.Status == "In Kitchen"
                || o.Status == "Ready"
                || o.Status == OrderWorkflow.Served
                || o.Status == OrderWorkflow.PendingCashier
                || o.Status == OrderWorkflow.PendingApproval
                || (o.Status == "Completed" && (o.CompletedAt ?? o.CreatedAt) >= completedSince))
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        return Ok(MapOngoingOrders(orders));
    }

    [HttpGet("ready-orders")]
    public ActionResult<IReadOnlyList<ServerReadyOrderDto>> GetReadyOrders()
    {
        var session = RequireServerSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-server role." });

        var orders = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.Status == "Ready" && o.ServerId == session.EmployeeId)
            .OrderBy(o => o.CreatedAt)
            .ToList();

        var rows = MapOngoingOrders(orders)
            .Select(o => new ServerReadyOrderDto(
                o.Id,
                o.OrderId,
                o.TableId,
                o.TableLabel,
                o.ServerName,
                o.Status,
                o.ItemsSummary,
                o.ItemCount,
                o.TotalUsd,
                o.TotalFc,
                o.CreatedAt,
                o.TimeText,
                o.CustomerNotes,
                o.AllergyNotes,
                o.GuestCustomerName,
                o.OrderOrigin,
                o.OrderSource,
                o.IsOnlineMenuOrder,
                o.Lines))
            .ToList();

        return Ok(rows);
    }

    [HttpPost("orders/{orderId:int}/serve")]
    public ActionResult<ServerMarkServedResponse> MarkServed(int orderId)
    {
        var session = RequireServerSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-server role." });
        if (orderId <= 0)
            return BadRequest(new { message = "orderId is required." });

        var order = db.Orders.SingleOrDefault(o => o.Id == orderId && o.ServerId == session.EmployeeId);
        if (order is null)
            return NotFound(new { message = "Order not found for this server." });
        if (!string.Equals(order.Status, "Ready", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only Ready orders can be marked Served." });

        order.Status = OrderWorkflow.Served;
        DataReconciler.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();

        var orderCode = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId;
        return Ok(new ServerMarkServedResponse(
            true,
            $"Order {orderCode} marked Served.",
            orderCode,
            order.Status,
            DateTime.UtcNow));
    }

    private AuthenticatedStaffSession? RequireServerSession()
    {
        var token = Request.ReadBearerToken();
        var session = authService.Validate(token);
        if (session is null)
            return null;
        return session.Role.Equals("Server", StringComparison.OrdinalIgnoreCase) ? session : null;
    }

    private AuthenticatedStaffSession? RequireServerOrCashierSession()
    {
        var token = Request.ReadBearerToken();
        var session = authService.Validate(token);
        if (session is null)
            return null;
        if (session.Role.Equals("Server", StringComparison.OrdinalIgnoreCase)
            || session.Role.Equals("Cashier", StringComparison.OrdinalIgnoreCase)
            || StaffPortalAuthentication.IsReceptionRole(session.Role))
            return session;
        return null;
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

    private static List<(int ProductId, int Quantity)> MergeOrderItemsWithNewLines(
        ICollection<OrderItem> existing,
        IReadOnlyList<ServerOrderLineRequest> additional)
    {
        var map = existing.GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        foreach (var l in additional)
        {
            if (!map.TryAdd(l.ProductId, l.Quantity))
                map[l.ProductId] += l.Quantity;
        }

        return map.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    private static string? ResolveGuestCustomerName(OrderRecord order)
    {
        var ticket = DeliveryTicketInfoParser.TryParse(order);
        if (ticket is not null && !string.IsNullOrWhiteSpace(ticket.CustomerName))
            return ticket.CustomerName.Trim();

        var reservation = (order.ReservationGuestName ?? string.Empty).Trim();
        return reservation.Length > 0 ? reservation : null;
    }

    private ActionResult? TryAuthorizeTableForOpenChecks(int tableId, out Table? table)
    {
        table = null;
        var session = RequireServerOrCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token. Requires Server or Cashier portal login." });
        if (tableId <= 0)
            return BadRequest(new { message = "tableId is required." });

        table = db.Tables.AsNoTracking().SingleOrDefault(t => t.Id == tableId);
        if (table is null)
            return NotFound(new { message = "Table not found." });
        if (session.Role.Equals("Server", StringComparison.OrdinalIgnoreCase))
        {
            if (table.AssignedServerId != session.EmployeeId)
                return BadRequest(new { message = "This table is not assigned to the logged-in server." });
        }
        else if (table.AssignedServerId is null)
            return BadRequest(new { message = "Table must have an assigned server for open checks." });

        return null;
    }

    private static ServerOpenCheckDto MapOpenCheckDto(OrderRecord order, MenuTaxonomySettings menuTaxonomy)
    {
        var lines = order.Items
            .OrderBy(i => i.Product?.Name ?? string.Empty)
            .Select(i =>
            {
                var price = i.Product?.Price ?? 0m;
                return new ServerOpenCheckLineDto(
                    i.ProductId,
                    string.IsNullOrWhiteSpace(i.Product?.Name) ? "Unknown" : i.Product.Name.Trim(),
                    string.IsNullOrWhiteSpace(i.Product?.Category) ? string.Empty : i.Product.Category.Trim(),
                    i.Quantity,
                    price * i.Quantity);
            })
            .ToList();

        var subtotal = lines.Sum(l => l.LineTotalUsd);
        var totals = OrderTotalsHelper.ComputeTotals(subtotal, order.DiscountMode, order.DiscountValue);
        var productsOnOrder = order.Items
            .Select(i => i.Product)
            .Where(p => p is not null)
            .Cast<Product>();
        var checkKind = OpenCheckKindHelper.TryInferCheckKindFromProducts(productsOnOrder, menuTaxonomy)
            ?? OpenCheckKindHelper.Food;
        var orderLabel = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId;

        return new ServerOpenCheckDto(
            order.Id,
            orderLabel,
            order.Status,
            checkKind,
            order.CreatedAt,
            (order.CustomerNotes ?? string.Empty).Trim(),
            (order.AllergyNotes ?? string.Empty).Trim(),
            subtotal,
            totals.GrandTotal,
            lines);
    }

    private static ServerTableCallRowDto ToTableCallRow(TableServerCallEntry c) =>
        new(
            c.Id,
            c.TableId,
            c.TableNumber,
            c.TableName,
            c.ReasonCode,
            c.ReasonLabel,
            c.IsPending ? "Pending" : "Accepted",
            c.CalledAtUtc,
            c.AcceptedAtUtc,
            c.AssignedServerId,
            c.AssignedServerName);

    private static string ProductPhotoAssetKey(int productId) => $"product:{productId}";

    private List<ServerOngoingOrderDto> MapOngoingOrders(IReadOnlyList<OrderRecord> orders)
    {
        if (orders.Count == 0)
            return [];

        var productIds = orders
            .SelectMany(o => o.Items)
            .Select(i => i.ProductId)
            .Distinct()
            .ToList();
        var photoKeys = productIds.Select(ProductPhotoAssetKey).ToList();
        var photoKeyPresent = photoKeys.Count == 0
            ? new HashSet<string>()
            : db.PublicMenuAssets.AsNoTracking()
                .Where(a => photoKeys.Contains(a.Key) && a.Content.Length > 0)
                .Select(a => a.Key)
                .ToHashSet();

        return orders.Select(o =>
        {
            var subtotal = o.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
            var totals = OrderTotalsHelper.ComputeTotals(subtotal, o.DiscountMode, o.DiscountValue);
            var guestName = ResolveGuestCustomerName(o);
            var lines = o.Items
                .OrderBy(i => i.Product?.Name ?? string.Empty)
                .Select(i =>
                {
                    var photoUrl = photoKeyPresent.Contains(ProductPhotoAssetKey(i.ProductId))
                        ? $"/api/public/menu/assets/product/{i.ProductId}"
                        : null;
                    return new ServerReadyOrderLineDto(
                        i.ProductId,
                        string.IsNullOrWhiteSpace(i.Product?.Name) ? "Unknown" : i.Product.Name.Trim(),
                        i.Quantity,
                        photoUrl);
                })
                .ToList();
            var status = o.Status ?? string.Empty;
            var kitchenKey = OrderWorkflow.KitchenStatusKey(status);
            return new ServerOngoingOrderDto(
                o.Id,
                string.IsNullOrWhiteSpace(o.UniqueId) ? $"#{o.Id:000}" : o.UniqueId,
                o.TableId ?? 0,
                OrderRecordUiLabels.TableCaption(o),
                string.IsNullOrWhiteSpace(o.ServerName) ? "-" : o.ServerName,
                status,
                kitchenKey,
                AdminOrdersViewMapper.GetStatusColor(status),
                ServerOngoingDisplayStatus(status, kitchenKey),
                string.Join(", ", o.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} x{i.Quantity}")),
                o.Items.Sum(i => i.Quantity),
                totals.GrandTotal,
                CurrencyHelper.ConvertUsdToFc(totals.GrandTotal),
                o.CreatedAt,
                o.CreatedAt.ToString("HH:mm"),
                (o.CustomerNotes ?? string.Empty).Trim(),
                (o.AllergyNotes ?? string.Empty).Trim(),
                guestName,
                o.OrderOrigin ?? OrderOrigin.InStore,
                o.OrderSource ?? "WalkIn",
                OrderOrigin.IsOnline(o.OrderOrigin),
                OrderWorkflow.IsReady(status),
                lines);
        }).ToList();
    }

    private Dictionary<int, List<ServerTableCallOrderDto>> MapTableCallOrdersByTable(
        Dictionary<int, List<OrderRecord>> ordersByTable,
        MenuTaxonomySettings menuTaxonomy)
    {
        if (ordersByTable.Count == 0)
            return [];

        var allOrders = ordersByTable.Values.SelectMany(v => v).ToList();
        var ongoing = MapOngoingOrders(allOrders);
        var result = new Dictionary<int, List<ServerTableCallOrderDto>>();
        foreach (var order in ongoing)
        {
            if (order.TableId <= 0)
                continue;
            if (!result.TryGetValue(order.TableId, out var list))
            {
                list = [];
                result[order.TableId] = list;
            }

            var source = allOrders.FirstOrDefault(o => o.Id == order.Id);
            var checkKind = source is null
                ? OpenCheckKindHelper.Food
                : OpenCheckKindHelper.TryInferCheckKindFromProducts(
                    source.Items.Select(i => i.Product).Where(p => p is not null).Cast<Product>(),
                    menuTaxonomy)
                  ?? OpenCheckKindHelper.Food;

            list.Add(new ServerTableCallOrderDto(
                order.Id,
                order.OrderId,
                order.TableId,
                order.TableLabel,
                order.Status,
                order.DisplayStatus,
                order.StatusColor,
                checkKind,
                order.CreatedAt,
                order.TimeText,
                order.CustomerNotes,
                order.AllergyNotes,
                order.TotalUsd,
                order.ItemCount,
                order.ItemsSummary,
                order.CanMarkServed,
                order.Lines));
        }

        return result;
    }

    private static string ServerOngoingDisplayStatus(string status, string kitchenKey) =>
        kitchenKey switch
        {
            "waiting" => "Waiting",
            "inKitchen" => "Cooking",
            "ready" => "Ready",
            "served" => "Served",
            "pendingCashier" => "Pending cashier",
            "pendingApproval" => "Pending approval",
            _ when string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) => "Completed",
            _ => string.IsNullOrWhiteSpace(status) ? "Unknown" : status.Trim()
        };

    private static List<OrderItem> BuildOrderItems(
        IReadOnlyList<ServerOrderLineRequest> lines,
        IReadOnlyDictionary<int, Product> products,
        IReadOnlyList<Employee> activeStaff)
    {
        var items = new List<OrderItem>(lines.Count);
        foreach (var line in lines)
        {
            var assignee = OrderSubmissionHelper.ResolveAssignee(products, activeStaff, line.ProductId);
            items.Add(new OrderItem
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                PreparedByEmployeeId = assignee.EmployeeId,
                PreparedByRole = assignee.Role,
                PreparedByName = assignee.Name
            });
        }

        return items;
    }

    private MenuTaxonomySettings LoadMenuTaxonomy()
    {
        var cloud = db.PublicMenuSettings.AsNoTracking().FirstOrDefault(s => s.Key == "default");
        return MenuTaxonomyHelper.ResolveEffective(cloud?.MenuTaxonomyJson, SettingsManager.Load().MenuTaxonomy);
    }

}
