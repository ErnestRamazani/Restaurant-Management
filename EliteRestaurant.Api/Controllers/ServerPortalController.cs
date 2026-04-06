using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Api.Security;
using EliteRestaurantPro.Data;
using EliteRestaurantPro.Models;
using EliteRestaurantPro.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/server")]
public sealed class ServerPortalController(TabletAuthService authService) : ControllerBase
{
    [HttpGet("config")]
    public ActionResult<ServerPortalConfigDto> GetConfig()
    {
        var session = RequireServerSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-server role." });

        var allSettings = SettingsManager.Load();
        var settings = allSettings.CurrencyPricing;
        var business = allSettings.BusinessProfile;
        var token = Uri.EscapeDataString(session.Token);
        var logoUrl = $"/api/server/assets/restaurant-logo?token={token}";
        var employeePhotoUrl = $"/api/server/assets/me-photo?token={token}";

        return Ok(new ServerPortalConfigDto(
            string.IsNullOrWhiteSpace(business.RestaurantName) ? "Elite Restaurant" : business.RestaurantName.Trim(),
            logoUrl,
            employeePhotoUrl,
            settings.DefaultCurrencyDisplayMode,
            settings.UsdToFcRate > 0m ? settings.UsdToFcRate : CurrencyHelper.DefaultFcPerUsd,
            settings.TaxPercent > 0m ? settings.TaxPercent : 7m,
            settings.ServicePercent > 0m ? settings.ServicePercent : 10m));
    }

    [HttpGet("assets/restaurant-logo")]
    public IActionResult GetRestaurantLogo([FromQuery] string token)
    {
        var session = authService.Validate(token);
        if (session is null || !session.Role.Equals("Server", StringComparison.OrdinalIgnoreCase))
            return Unauthorized();

        var logoPath = SettingsManager.Load().BusinessProfile.LogoPath?.Trim() ?? string.Empty;
        return ServeImageFromPath(logoPath);
    }

    [HttpGet("assets/me-photo")]
    public IActionResult GetEmployeePhoto([FromQuery] string token)
    {
        var session = authService.Validate(token);
        if (session is null || !session.Role.Equals("Server", StringComparison.OrdinalIgnoreCase))
            return Unauthorized();

        using var db = new AppDbContext();
        var photoPath = db.Employees.AsNoTracking()
            .Where(e => e.Id == session.EmployeeId)
            .Select(e => e.ProfileImagePath)
            .FirstOrDefault();
        return ServeImageFromPath(photoPath?.Trim() ?? string.Empty);
    }

    [HttpGet("products")]
    public ActionResult<IReadOnlyList<ServerProductDto>> GetProducts()
    {
        var session = RequireServerSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-server role." });

        using var db = new AppDbContext();
        var rows = db.Products.AsNoTracking()
            .OrderBy(p => p.Category)
            .ThenBy(p => p.SubCategory)
            .ThenBy(p => p.Name)
            .Select(p => new ServerProductDto(
                p.Id,
                p.UniqueId,
                p.Name,
                p.Category,
                string.IsNullOrWhiteSpace(p.SubCategory) ? "General" : p.SubCategory,
                p.Price))
            .ToList();

        return Ok(rows);
    }

    [HttpGet("open-check")]
    public ActionResult GetOpenCheck([FromQuery] int tableId)
    {
        var session = RequireServerSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-server role." });
        if (tableId <= 0)
            return BadRequest(new { message = "tableId is required." });

        using var db = new AppDbContext();
        var table = db.Tables.AsNoTracking().SingleOrDefault(t => t.Id == tableId);
        if (table is null)
            return NotFound(new { message = "Table not found." });
        if (table.AssignedServerId != session.EmployeeId)
            return BadRequest(new { message = "This table is not assigned to the logged-in server." });

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
    public ActionResult<IReadOnlyList<ServerDraftDto>> GetDrafts()
    {
        var session = RequireServerSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-server role." });

        try
        {
            var rows = SharedOrderDraftStore.ListServerDrafts(session.EmployeeId)
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
        var session = RequireServerSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-server role." });

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
            var saved = SharedOrderDraftStore.SaveServerDraft(
                session.EmployeeId,
                session.Name,
                request.Label ?? string.Empty,
                snapshot);

            return Ok(new ServerDraftDto(saved.Id, saved.Label, saved.PayloadJson, saved.UpdatedAtUtc));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Draft save failed.", detail = ex.Message });
        }
    }

    [HttpDelete("drafts/{draftId}")]
    public ActionResult DeleteDraft(string draftId)
    {
        var session = RequireServerSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-server role." });

        try
        {
            var deleted = SharedOrderDraftStore.DeleteServerDraft(session.EmployeeId, draftId);
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
        var session = RequireServerSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-server role." });

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

        using var db = new AppDbContext();
        var table = db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == request.TableId);
        if (table is null)
            return NotFound(new { message = "Table not found." });
        if (table.AssignedServerId != session.EmployeeId)
            return BadRequest(new { message = "This table is not assigned to the logged-in server." });
        if (string.Equals(table.Status, "Maintenance", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Maintenance table cannot receive orders." });

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

            if (discountMode.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                // preserve existing discount setup when appending unless caller explicitly sets a mode
            }
            else
            {
                openOrder.DiscountMode = discountMode;
                openOrder.DiscountValue = discountValue;
            }
            openOrder.OrderSource = isDelivery ? "Delivery" : "WalkIn";
            openOrder.ReservationGuestName = isDelivery ? request.SourceReference.Trim() : string.Empty;
            openOrder.PaymentCurrencyCode = CurrencyHelper.NormalizeCurrencyCode(request.PaymentCurrencyCode);
            SyncPaymentFields(openOrder, products);
            table.Status = "Occupied";
            db.SaveChanges();
            AppDbContext.ReconcileTableStatusesWithOrders(db);
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
            ServerId = session.EmployeeId,
            ServerName = session.Name,
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

        SyncPaymentFields(order, products);
        db.Orders.Add(order);
        table.Status = "Occupied";
        db.SaveChanges();
        AppDbContext.ReconcileTableStatusesWithOrders(db);
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

        using var db = new AppDbContext();
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

        using var db = new AppDbContext();
        var order = db.Orders.SingleOrDefault(o => o.Id == orderId && o.ServerId == session.EmployeeId);
        if (order is null)
            return NotFound(new { message = "Order not found for this server." });
        if (!string.Equals(order.Status, "Ready", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only Ready orders can be marked Served." });

        order.Status = OrderWorkflow.Served;
        db.SaveChanges();
        AppDbContext.ReconcileTableStatusesWithOrders(db);
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

    private static List<OrderItem> BuildOrderItems(
        IReadOnlyList<ServerOrderLineRequest> lines,
        IReadOnlyDictionary<int, Product> products,
        IReadOnlyList<Employee> activeStaff)
    {
        var items = new List<OrderItem>(lines.Count);
        foreach (var line in lines)
        {
            var assignee = ResolveAssignee(products, activeStaff, line.ProductId);
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

    private static (int? EmployeeId, string Role, string Name) ResolveAssignee(
        IReadOnlyDictionary<int, Product> products,
        IReadOnlyList<Employee> activeStaff,
        int productId)
    {
        if (!products.TryGetValue(productId, out var product))
            return (null, "Unknown", "Unassigned");

        if (string.Equals(product.Category, "Drink", StringComparison.OrdinalIgnoreCase))
        {
            var barman = activeStaff.FirstOrDefault(e =>
                e.Role.Equals("Barman", StringComparison.OrdinalIgnoreCase) ||
                e.Role.Equals("Bartender", StringComparison.OrdinalIgnoreCase));
            return barman is null ? (null, "Barman", "Unassigned Barman") : (barman.Id, "Barman", barman.Name);
        }

        var chef = activeStaff.FirstOrDefault(e => e.Role.Equals("Chef", StringComparison.OrdinalIgnoreCase));
        return chef is null ? (null, "Chef", "Unassigned Chef") : (chef.Id, "Chef", chef.Name);
    }

    private static void SyncPaymentFields(OrderRecord order, IReadOnlyDictionary<int, Product> products)
    {
        var subtotal = order.Items.Sum(i => (products.TryGetValue(i.ProductId, out var p) ? p.Price : 0m) * i.Quantity);
        var totals = OrderTotalsHelper.ComputeTotals(subtotal, order.DiscountMode, order.DiscountValue);
        var grand = totals.GrandTotal;
        order.DiscountAmountUsd = totals.DiscountApplied;
        order.PaymentAmountUsd = Math.Round(grand, 2);
        order.PaymentAmountFc = CurrencyHelper.ConvertUsdToFc(grand);
        order.PaymentAmount = string.Equals(order.PaymentCurrencyCode, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase)
            ? order.PaymentAmountFc
            : order.PaymentAmountUsd;
        order.CustomerPaidUsd = 0m;
        order.CustomerPaidFc = 0m;
        order.ChangeGivenUsd = 0m;
        order.ChangeGivenFc = 0m;
        order.ExchangeRateUsed = CurrencyHelper.FcPerUsd;
    }
}
