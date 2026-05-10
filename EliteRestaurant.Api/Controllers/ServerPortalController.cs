// PRICING / BRANDING PRECEDENCE (aligned with PublicMenuController):
// 1. API appsettings.json (CurrencyPricing section) — explicit operator override when values are positive.
// 2. PublicMenuSettings row (Key=default) — cloud profile from POST api/admin/settings/cloud-profile (desktop push).
// 3. App file settings (SettingsManager / app-settings.json) — local fallback when cloud fields are unset.
// See PricingPrecedenceTests for tax/service matrix when cloud row is absent.
using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Api.Security;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/server")]
[Authorize(Policy = "StaffAny")]
public sealed class ServerPortalController(
    TabletAuthService authService,
    IOptions<CurrencyPricingOptions> currencyPricingOptions,
    AppDbContext db) : ControllerBase
{
    [HttpGet("config")]
    public ActionResult<ServerPortalConfigDto> GetConfig()
    {
        var session = RequireServerOrCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token. Requires Server or Cashier portal login." });

        var allSettings = SettingsManager.Load();
        var settings = allSettings.CurrencyPricing;
        var business = allSettings.BusinessProfile;
        var cloudSettings = db.PublicMenuSettings.AsNoTracking().FirstOrDefault(s => s.Key == "default");
        var restaurantName = string.IsNullOrWhiteSpace(cloudSettings?.RestaurantName)
            ? (string.IsNullOrWhiteSpace(business.RestaurantName) ? "Elite Restaurant" : business.RestaurantName.Trim())
            : cloudSettings!.RestaurantName.Trim();
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

        return Ok(new ServerPortalConfigDto(
            restaurantName,
            logoUrl,
            employeePhotoUrl,
            displayMode,
            usdToFc,
            taxPercent,
            servicePercent));
    }

    [HttpGet("assets/restaurant-logo")]
    public IActionResult GetRestaurantLogo()
    {
        var session = RequireServerOrCashierSession();
        if (session is null)
            return Unauthorized();

        var asset = db.PublicMenuAssets.AsNoTracking().FirstOrDefault(a => a.Key == "logo");
        if (asset is { Content.Length: > 0 })
        {
            var contentType = string.IsNullOrWhiteSpace(asset.ContentType)
                ? "image/png"
                : asset.ContentType;
            return File(asset.Content, contentType);
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
                p.Price
            })
            .ToList();

        var productIds = products.Select(p => p.Id).ToList();
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
            return new ServerProductDto(p.Id, p.UniqueId, p.Name, p.Category, p.SubCategory, p.Price, inStock);
        }).ToList();

        return Ok(rows);
    }

    [HttpGet("open-check")]
    public ActionResult GetOpenCheck([FromQuery] int tableId)
    {
        var session = RequireServerOrCashierSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token. Requires Server or Cashier portal login." });
        if (tableId <= 0)
            return BadRequest(new { message = "tableId is required." });

        var table = db.Tables.AsNoTracking().SingleOrDefault(t => t.Id == tableId);
        if (table is null)
            return NotFound(new { message = "Table not found." });
        if (session.Role.Equals("Server", StringComparison.OrdinalIgnoreCase))
        {
            if (table.AssignedServerId != session.EmployeeId)
                return BadRequest(new { message = "This table is not assigned to the logged-in server." });
        }
        else if (table.AssignedServerId is null)
            return BadRequest(new { message = "Table must have an assigned server for open checks." });

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
                .Select(d => new ServerDraftDto(d.Id, d.Label, d.PayloadJson, d.UpdatedAtUtc))
                .ToList();
            return Ok(rows);
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

            return Ok(new ServerDraftDto(saved.Id, saved.Label, saved.PayloadJson, saved.UpdatedAtUtc));
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
    public ActionResult<ServerCreateOrderResponse> CreateOrAppendOrder([FromBody] ServerCreateOrderRequest request)
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

        var openOrder = db.Orders.Include(o => o.Items)
            .WhereOpenCheckForTable(table.Id)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        var newItems = BuildOrderItems(normalizedLines, products, activeStaff);

        IReadOnlyList<(int ProductId, int Quantity)> linesToValidate;
        OrderInventoryDeduction.InventoryValidationKind validationKind;
        if (request.AppendToOpenCheck && openOrder is not null)
        {
            if (OrderWorkflow.IsPendingCashier(openOrder.Status))
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
                || string.Equals(openOrder.Status, OrderWorkflow.Served, StringComparison.OrdinalIgnoreCase))
            {
                openOrder.Status = "In Kitchen";
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
            openOrder.ReservationGuestName = isDelivery ? request.SourceReference.Trim() : string.Empty;
            openOrder.PaymentCurrencyCode = CurrencyHelper.NormalizeCurrencyCode(request.PaymentCurrencyCode);
            OrderSubmissionHelper.SyncPaymentFields(openOrder, products);
            table.Status = "Occupied";
            DataReconciler.ReconcileTableStatusesWithOrders(db);
            db.SaveChanges();

            var code = string.IsNullOrWhiteSpace(openOrder.UniqueId) ? $"#{openOrder.Id:000}" : openOrder.UniqueId;
            return Ok(new ServerCreateOrderResponse(
                Mode: "Append",
                OrderId: code,
                LinesAdded: newItems.Count,
                Message: $"Added {newItems.Count} line(s) to open check {code}.",
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
            ReservationGuestName = isDelivery ? request.SourceReference.Trim() : string.Empty
        };

        foreach (var item in newItems)
            order.Items.Add(item);

        OrderSubmissionHelper.SyncPaymentFields(order, products);
        db.Orders.Add(order);
        table.Status = "Occupied";
        DataReconciler.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();

        return Ok(new ServerCreateOrderResponse(
            Mode: "Create",
            OrderId: order.UniqueId,
            LinesAdded: newItems.Count,
            Message: $"Order {order.UniqueId} sent to cashier.",
            CreatedAtUtc: DateTime.UtcNow));
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

        var rows = orders.Select(o =>
        {
            var subtotal = o.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
            var totals = OrderTotalsHelper.ComputeTotals(subtotal, o.DiscountMode, o.DiscountValue);
            return new ServerReadyOrderDto(
                o.Id,
                string.IsNullOrWhiteSpace(o.UniqueId) ? $"#{o.Id:000}" : o.UniqueId,
                o.TableId ?? 0,
                $"{o.TableCode} · {o.TableName}",
                string.IsNullOrWhiteSpace(o.ServerName) ? "-" : o.ServerName,
                o.Status,
                string.Join(", ", o.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} x{i.Quantity}")),
                o.Items.Sum(i => i.Quantity),
                totals.GrandTotal,
                CurrencyHelper.ConvertUsdToFc(totals.GrandTotal),
                o.CreatedAt,
                o.CreatedAt.ToString("HH:mm"))
            ;
        }).ToList();

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
            || session.Role.Equals("Cashier", StringComparison.OrdinalIgnoreCase))
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

}
