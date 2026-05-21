using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Api.Orders;
using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Reporting;
using EliteRestaurant.Core.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/admin/data")]
[Authorize(Policy = "StaffAny")]
public sealed class AdminDataController(AppDbContext db) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    private const int SnapshotDefaultCap = 1000;
    private const int SnapshotReportRangeCap = 15_000;

    [HttpGet("employees-web/{id:int}/photo")]
    public async Task<IActionResult> GetEmployeeWebPhoto(int id, CancellationToken cancellationToken)
    {
        var photoPath = await db.Employees.AsNoTracking()
            .Where(e => e.Id == id && e.EmploymentStatus == "Active")
            .Select(e => e.ProfileImagePath)
            .FirstOrDefaultAsync(cancellationToken);

        return ServeEmployeeImageFromPath(photoPath?.Trim() ?? string.Empty);
    }

    /// <summary>Money dashboard snapshot (same aggregates as desktop). Under <c>/api/admin/data/...</c> so gateways that only proxy data routes still work.</summary>
    [HttpGet("money-web/snapshot")]
    public ActionResult<MoneyDashboardSnapshotData> GetMoneyWebSnapshot([FromQuery] string period = "Week", [FromQuery] string? origin = null)
    {
        var p = (period ?? "Week").Trim().ToLowerInvariant() switch
        {
            "today" => "Today",
            "month" => "Month",
            "year" => "Year",
            "all" => "All",
            _ => "Week"
        };
        var o = NormalizeMoneyOrigin(origin);
        MoneyDashboardTotals.EnsureSaleRevenueBackfill(db);
        var txs = db.Transactions.AsNoTracking().ToList();
        var snap = MoneyDashboardSnapshotBuilder.BuildFromTransactions(txs, p, maxLedgerRows: 200, originFilter: o);
        return Ok(snap);
    }

    private static string? NormalizeMoneyOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return null;
        var t = origin.Trim();
        if (string.Equals(t, "all", StringComparison.OrdinalIgnoreCase))
            return null;
        if (string.Equals(t, "online", StringComparison.OrdinalIgnoreCase))
            return OrderOrigin.Online;
        if (string.Equals(t, "instore", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "in-store", StringComparison.OrdinalIgnoreCase))
            return OrderOrigin.InStore;
        return null;
    }

    private static readonly HashSet<string> AdminWebBlockedEntityKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "orders", "orderrecord", "orderitems", "orderitem",
        "employees", "employee",
        "settings",
        "reservations", "reservationbooking",
        "customerprofiles", "customerprofile",
        "attendance", "employeeattendances", "employeeattendance",
        "salaryadvances", "salaryadvance",
        "payroll", "payrollpaymentrecords", "payrollpaymentrecord",
        "attendancedayvalidations", "attendancevalidations", "attendancedayvalidation"
    };

    [HttpGet("{entityName}")]
    public async Task<IActionResult> List(
        string entityName,
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken cancellationToken)
    {
        var key = entityName.ToLowerInvariant();
        if (IsAdminWebPortal() && AdminWebBlockedEntityKeys.Contains(key))
            return AdminWebForbidden();

        var snapshotAt = DateTime.UtcNow;

        if (key is "order-summaries" or "ordersummaries" or "order-summary")
        {
            var rows = await LoadOrderSummaryJsonAsync(cancellationToken);
            return Ok(new AdminEntityListResponse(entityName, rows, snapshotAt));
        }

        if (key is "employees-web" or "employeesweb")
        {
            // Serialize DTOs directly. Nested JsonElement + second-pass serialization dropped properties in the browser for this endpoint.
            var rows = await LoadEmployeesWebAsync(cancellationToken);
            return Ok(new { entityName, items = rows, snapshotAtUtc = snapshotAt });
        }

        if (key is "orders" or "orderrecord")
        {
            var orderItems = await ListOrdersJsonAsync(start, end, GetPrepStationPortal(), cancellationToken);
            return Ok(new AdminEntityListResponse(entityName, orderItems, snapshotAt));
        }

        if (key is "reservations" or "reservationbooking")
        {
            var resItems = await ListReservationsJsonAsync(start, end, cancellationToken);
            return Ok(new AdminEntityListResponse(entityName, resItems, snapshotAt));
        }

        var items = key switch
        {
            "products" or "product" => await SnapshotProductsFlat(cancellationToken),
            "productingredients" or "productingredient" => await Snapshot(db.ProductIngredients.AsNoTracking(), cancellationToken),
            "employees" or "employee" => await Snapshot(db.Employees.AsNoTracking().OrderBy(e => e.Name), cancellationToken),
            "tables" or "table" => await SnapshotTablesFlat(cancellationToken),
            "customerprofiles" or "customerprofile" => await Snapshot(db.CustomerProfiles.AsNoTracking().OrderBy(c => c.FullName), cancellationToken),
            "inventory" or "inventoryitems" or "inventoryitem" => await SnapshotInventoryFlat(cancellationToken),
            "attendance" or "employeeattendances" or "employeeattendance" => await SnapshotAttendanceFullAsync(cancellationToken),
            "salaryadvances" or "salaryadvance" => await Snapshot(db.SalaryAdvances.AsNoTracking().OrderByDescending(a => a.GivenAt), cancellationToken),
            "payroll" or "payrollpaymentrecords" or "payrollpaymentrecord" => await Snapshot(db.PayrollPaymentRecords.AsNoTracking().OrderByDescending(p => p.PaidAtUtc), cancellationToken),
            "orderitems" or "orderitem" => await Snapshot(db.OrderItems.AsNoTracking(), cancellationToken),
            "money" or "transactions" or "moneytransaction" => await Snapshot(db.Transactions.AsNoTracking().OrderByDescending(t => t.Date), cancellationToken),
            "attendancedayvalidations" or "attendancevalidations" or "attendancedayvalidation" => await Snapshot(db.AttendanceDayValidations.AsNoTracking().OrderByDescending(v => v.WorkDate), cancellationToken),
            "settings" => SnapshotSettings(),
            _ => null
        };

        if (items is null)
            return NotFound(new { message = $"Unsupported admin entity '{entityName}'." });

        return Ok(new AdminEntityListResponse(entityName, items, snapshotAt));
    }

    [HttpGet("order-summaries/{orderId:int}/invoice")]
    public async Task<ActionResult<CashierOrderDetailDto>> GetOrderSummaryInvoice(int orderId, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Include(o => o.Table)
            .Include(o => o.Server)
            .SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
            return NotFound(new { message = "Order not found." });

        return Ok(CashierOrderDetailBuilder.Build(order));
    }

    [HttpGet("bundles/create-order")]
    public async Task<ActionResult<AdminCreateOrderCatalogBundleResponse>> CreateOrderCatalogBundle(CancellationToken cancellationToken)
    {
        if (IsAdminWebPortal())
            return Forbid();

        var tables = await Snapshot(
            db.Tables.AsNoTracking()
                .Include(t => t.AssignedServer)
                .AsSplitQuery()
                .OrderBy(t => t.TableNumber),
            cancellationToken);
        var productList = await db.Products.AsNoTracking()
            .OrderBy(p => p.Category).ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
        var availability = await OrderInventoryAvailability.GetProductAvailabilityMapAsync(
            db,
            productList.Select(p => p.Id).ToList(),
            cancellationToken);
        foreach (var p in productList)
        {
            if (availability.TryGetValue(p.Id, out var ok))
                p.IsAvailable = ok;
        }

        var products = productList.Select(ToJsonElement).ToList();
        var reservations = await Snapshot(
            db.Reservations.AsNoTracking()
                .Where(r => r.Status == "Arrived")
                .OrderByDescending(r => r.ReservedFor)
                .Take(120),
            cancellationToken);
        var orders = await CreateOrderBundleOrdersAsync(cancellationToken);

        var snapshotAt = DateTime.UtcNow;
        return Ok(new AdminCreateOrderCatalogBundleResponse(
            tables,
            products,
            reservations,
            orders,
            snapshotAt));
    }

    /// <summary>Ingredient-based availability for menu products (same rule as public menu). Used when desktop loads catalog without the create-order bundle.</summary>
    [HttpPost("inventory/menu-product-availability")]
    public async Task<ActionResult<Dictionary<int, bool>>> MenuProductAvailability(
        [FromBody] AdminProductIdsRequest? body,
        CancellationToken cancellationToken)
    {
        if (IsAdminWebPortal())
            return Forbid();

        var ids = body?.ProductIds ?? Array.Empty<int>();
        if (ids.Length > 8000)
            return BadRequest(new { message = "Too many product ids." });

        var map = await OrderInventoryAvailability.GetProductAvailabilityMapAsync(db, ids, cancellationToken);
        return Ok(map);
    }

    private async Task<IReadOnlyList<JsonElement>> CreateOrderBundleOrdersAsync(CancellationToken cancellationToken)
    {
        var deliveryHeaders = await db.Orders.AsNoTracking()
            .Where(o => o.OrderSource == "Delivery")
            .OrderByDescending(o => o.CreatedAt)
            .Take(150)
            .ToListAsync(cancellationToken);

        var openWithLines = await db.Orders.AsNoTracking()
            .WhereOccupiesTable()
            .OrderByDescending(o => o.CreatedAt)
            .Take(400)
            .Include(o => o.Items)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var merged = new Dictionary<int, OrderRecord>();
        foreach (var o in deliveryHeaders)
            merged[o.Id] = o;
        foreach (var o in openWithLines)
            merged[o.Id] = o;

        var ordered = merged.Values
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        return ordered.Select(ToJsonElement).ToList();
    }

    private async Task<IReadOnlyList<JsonElement>> SnapshotInventoryFlat(CancellationToken cancellationToken)
    {
        var rows = await db.InventoryItems.AsNoTracking()
            .OrderBy(i => i.Name)
            .Select(i => new
            {
                i.Id,
                i.UniqueId,
                i.Name,
                i.Unit,
                i.StockQuantity,
                i.ExpirationDate,
                i.Notes
            })
            .Take(1000)
            .ToListAsync(cancellationToken);
        return rows.Select(ToJsonElement).ToList();
    }

    private async Task<IReadOnlyList<JsonElement>> SnapshotProductsFlat(CancellationToken cancellationToken)
    {
        var rows = await db.Products.AsNoTracking()
            .OrderBy(p => p.Category).ThenBy(p => p.Name)
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
            .Take(1000)
            .ToListAsync(cancellationToken);
        return rows.Select(ToJsonElement).ToList();
    }

    private async Task<IReadOnlyList<JsonElement>> SnapshotTablesFlat(CancellationToken cancellationToken)
    {
        var rows = await db.Tables.AsNoTracking()
            .OrderBy(t => t.TableNumber)
            .Select(t => new
            {
                t.Id,
                t.UniqueId,
                t.TableNumber,
                t.Name,
                t.Capacity,
                t.Status,
                t.AssignedServerId,
                AssignedServer = t.AssignedServer == null
                    ? null
                    : new
                    {
                        t.AssignedServer.Id,
                        t.AssignedServer.UniqueId,
                        t.AssignedServer.Name,
                        t.AssignedServer.Role,
                        t.AssignedServer.EmploymentStatus
                    }
            })
            .Take(1000)
            .ToListAsync(cancellationToken);
        return rows.Select(ToJsonElement).ToList();
    }

    /// <summary>
    /// Desktop reports need rows in a calendar range; the generic snapshot cap (1000 newest) hid older online orders and past reservations.
    /// Optional <paramref name="start"/> / <paramref name="end"/> (inclusive calendar end) filter server-side with a higher cap.
    /// Orders use the same anchor as Money: <c>PaymentConfirmedAt ?? CompletedAt ?? CreatedAt</c>.
    /// </summary>
    private async Task<IReadOnlyList<JsonElement>> ListOrdersJsonAsync(
        DateTime? start,
        DateTime? end,
        string? prepStationPortal,
        CancellationToken cancellationToken)
    {
        var kitchenKdsOnly = prepStationPortal is not null;
        var q = db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .AsSplitQuery();
        if (kitchenKdsOnly)
            q = q.WhereKitchenKdsVisible();
        if (start is not null && end is not null)
        {
            var startDay = start.Value.Date;
            var endDay = end.Value.Date;
            if (endDay < startDay)
                return [];

            var endExclusive = endDay.AddDays(1);
            var filtered = q.Where(o =>
                (o.PaymentConfirmedAt ?? o.CompletedAt ?? o.CreatedAt) >= startDay
                && (o.PaymentConfirmedAt ?? o.CompletedAt ?? o.CreatedAt) < endExclusive);
            var list = await filtered
                .OrderByDescending(o => o.CreatedAt)
                .Take(SnapshotReportRangeCap)
                .ToListAsync(cancellationToken);
            return SerializeOrdersForPortal(list, kitchenKdsOnly, prepStationPortal);
        }

        var capped = await q
            .OrderByDescending(o => o.CreatedAt)
            .Take(SnapshotDefaultCap)
            .ToListAsync(cancellationToken);
        return SerializeOrdersForPortal(capped, kitchenKdsOnly, prepStationPortal);
    }

    private static IReadOnlyList<JsonElement> SerializeOrdersForPortal(
        IReadOnlyList<OrderRecord> list,
        bool kitchenKdsOnly,
        string? prepStationPortal)
    {
        if (!kitchenKdsOnly)
            return list.Select(ToJsonElement).ToList();

        var filtered = KitchenQueueKindFilter.FilterForPortal(prepStationPortal, list).ToList();
        return filtered
            .Select(o => ToJsonElement(KitchenOrderQueueMapper.ToQueueRow(o, prepStationPortal)))
            .ToList();
    }

    /// <summary>Reservations: by default last 1000 by <see cref="ReservationBooking.ReservedFor"/>; with range, include updates or reserved slot in range.</summary>
    private async Task<IReadOnlyList<JsonElement>> ListReservationsJsonAsync(
        DateTime? start,
        DateTime? end,
        CancellationToken cancellationToken)
    {
        var q = db.Reservations.AsNoTracking();
        if (start is not null && end is not null)
        {
            var startDay = start.Value.Date;
            var endDay = end.Value.Date;
            if (endDay < startDay)
                return [];

            var endExclusive = endDay.AddDays(1);
            var filtered = q.Where(r =>
                (r.UpdatedAt >= startDay && r.UpdatedAt < endExclusive)
                || (r.ReservedFor >= startDay && r.ReservedFor < endExclusive));
            var list = await filtered
                .OrderByDescending(r => r.ReservedFor)
                .Take(SnapshotReportRangeCap)
                .ToListAsync(cancellationToken);
            return list.Select(ToJsonElement).ToList();
        }

        var capped = await q
            .OrderByDescending(r => r.ReservedFor)
            .Take(SnapshotDefaultCap)
            .ToListAsync(cancellationToken);
        return capped.Select(ToJsonElement).ToList();
    }

    private static async Task<IReadOnlyList<JsonElement>> Snapshot<T>(IQueryable<T> query, CancellationToken cancellationToken)
    {
        var rows = await query.Take(SnapshotDefaultCap).ToListAsync(cancellationToken);
        return rows.Select(ToJsonElement).ToList();
    }

    /// <summary>
    /// Full attendance history for desktop admin (per-employee shift history, payroll, reports). Not subject to the generic 1000-row snapshot cap.
    /// </summary>
    private async Task<IReadOnlyList<JsonElement>> SnapshotAttendanceFullAsync(CancellationToken cancellationToken)
    {
        var rows = await db.EmployeeAttendances.AsNoTracking()
            .OrderByDescending(a => a.WorkDate)
            .ToListAsync(cancellationToken);
        return rows.Select(ToJsonElement).ToList();
    }

    private static IReadOnlyList<JsonElement> SnapshotSettings()
    {
        var settings = SettingsManager.Load();
        return [ToJsonElement(settings)];
    }

    private static JsonElement ToJsonElement<T>(T value) =>
        JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions);

    private bool IsAdminWebPortal() =>
        User.Identity?.IsAuthenticated == true
        && string.Equals(User.FindFirst("portal")?.Value, "AdminWeb", StringComparison.OrdinalIgnoreCase);

    private string? GetPrepStationPortal()
    {
        if (User.Identity?.IsAuthenticated != true)
            return null;
        var portal = User.FindFirst("portal")?.Value;
        return KitchenQueueKindFilter.IsPrepStationPortal(portal) ? portal : null;
    }

    private ActionResult AdminWebForbidden() => StatusCode(StatusCodes.Status403Forbidden,
        new { message = "This dataset is not available to the read-only web admin role." });

    private async Task<IReadOnlyList<JsonElement>> LoadOrderSummaryJsonAsync(CancellationToken cancellationToken)
    {
        var orders = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Include(o => o.Table)
            .OrderByDescending(o => o.CreatedAt)
            .Take(600)
            .ToListAsync(cancellationToken);

        var rows = new List<AdminOrderSummaryRow>(orders.Count);
        foreach (var o in orders)
        {
            var lineSum = o.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
            var totals = OrderTotalsHelper.ComputeTotals(lineSum, o.DiscountMode, o.DiscountValue);
            var serverName = string.IsNullOrWhiteSpace(o.ServerName) ? "Unassigned" : o.ServerName;
            var tableLabel = string.IsNullOrWhiteSpace(o.TableCode)
                ? (o.Table is null ? "—" : $"Table {o.Table.TableNumber}")
                : $"{o.TableCode} · {o.TableName}";

            var preview = string.Join(", ",
                o.Items
                    .OrderByDescending(i => i.Quantity)
                    .Take(8)
                    .Select(i => $"{(string.IsNullOrWhiteSpace(i.Product?.Name) ? "Item" : i.Product!.Name.Trim())} x{i.Quantity}"));

            rows.Add(new AdminOrderSummaryRow(
                o.Id,
                string.IsNullOrWhiteSpace(o.UniqueId) ? $"#{o.Id:000}" : o.UniqueId,
                o.CreatedAt,
                tableLabel,
                serverName,
                totals.GrandTotal,
                preview,
                o.Status,
                o.OrderOrigin));
        }

        return rows.Select(ToJsonElement).ToList();
    }

    private async Task<IReadOnlyList<AdminEmployeeWebRow>> LoadEmployeesWebAsync(CancellationToken cancellationToken)
    {
        var employees = await db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active")
            .OrderBy(e => e.Name)
            .Take(1000)
            .ToListAsync(cancellationToken);

        var today = DateTime.Today;
        var (todayStartUtc, todayEndExclusiveUtc) = AttendanceCalendar.DayRangeUtc(today);

        var attendanceToday = await db.EmployeeAttendances.AsNoTracking()
            .Where(a => a.WorkDate >= todayStartUtc && a.WorkDate < todayEndExclusiveUtc)
            .ToListAsync(cancellationToken);

        var attendanceByEmployeeId = attendanceToday
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        var rows = new List<AdminEmployeeWebRow>(employees.Count);
        foreach (var e in employees)
        {
            attendanceByEmployeeId.TryGetValue(e.Id, out var todayRow);
            var (clockIn, clockOut, attStatus) = MapTodayAttendance(todayRow);

            var photoUrl = string.IsNullOrWhiteSpace(e.ProfileImagePath)
                ? string.Empty
                : $"/api/admin/data/employees-web/{e.Id}/photo";

            rows.Add(new AdminEmployeeWebRow(
                e.Id,
                e.UniqueId ?? string.Empty,
                e.Name ?? string.Empty,
                e.Role ?? string.Empty,
                e.Notes ?? string.Empty,
                e.EmploymentStatus ?? string.Empty,
                e.MondayShift ?? string.Empty,
                e.TuesdayShift ?? string.Empty,
                e.WednesdayShift ?? string.Empty,
                e.ThursdayShift ?? string.Empty,
                e.FridayShift ?? string.Empty,
                e.SaturdayShift ?? string.Empty,
                e.SundayShift ?? string.Empty,
                photoUrl,
                e.PhoneNumber ?? string.Empty,
                e.JoinDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                BuildWorkScheduleSummary(e),
                clockIn,
                clockOut,
                attStatus));
        }

        return rows;
    }

    private static (string ClockIn, string ClockOut, string Status) MapTodayAttendance(EmployeeAttendance? a)
    {
        if (a is null)
            return ("Not clocked in", "Not clocked out", "Not active");

        var baseClockIn = a.ClockInTime?.ToString("HH:mm") ?? "Not clocked in";
        var clockIn = string.IsNullOrWhiteSpace(a.ClockInStatus)
            ? baseClockIn
            : $"{baseClockIn} ({a.ClockInStatus})";
        var clockOut = a.ClockOutTime?.ToString("HH:mm") ?? "Not clocked out";

        string status;
        if (a.ClockInTime is null)
            status = "Not active";
        else if (a.ClockOutTime is null)
            status = "On shift";
        else
            status = "Shift ended";

        return (clockIn, clockOut, status);
    }

    private static string BuildWorkScheduleSummary(Employee e)
    {
        var entries = new List<string>();
        AddWorkShift(entries, "Monday", e.MondayShift);
        AddWorkShift(entries, "Tuesday", e.TuesdayShift);
        AddWorkShift(entries, "Wednesday", e.WednesdayShift);
        AddWorkShift(entries, "Thursday", e.ThursdayShift);
        AddWorkShift(entries, "Friday", e.FridayShift);
        AddWorkShift(entries, "Saturday", e.SaturdayShift);
        AddWorkShift(entries, "Sunday", e.SundayShift);
        return entries.Count == 0 ? "No schedule assigned." : string.Join(", ", entries);
    }

    private static void AddWorkShift(List<string> entries, string day, string shift)
    {
        if (string.IsNullOrWhiteSpace(shift) || shift.Equals("Off", StringComparison.OrdinalIgnoreCase))
            return;

        entries.Add($"{day} ({shift})");
    }

    private IActionResult ServeEmployeeImageFromPath(string absolutePath)
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
