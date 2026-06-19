using System.Globalization;
using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Reporting;

/// <summary>Server-side report aggregation aligned with <c>ReportsViewModel</c> (desktop).</summary>
public sealed class AdminReportAggregationService(AppDbContext db)
{
    public async Task<AdminReportListsResponse> GetListsAsync(CancellationToken cancellationToken = default)
    {
        // Same DbContext cannot run multiple queries concurrently — parallel ToListAsync causes HTTP 500.
        var employeesRows = await db.Employees.AsNoTracking().OrderBy(e => e.Name).ToListAsync(cancellationToken);
        var tablesRows = await db.Tables.AsNoTracking().OrderBy(t => t.TableNumber).ToListAsync(cancellationToken);
        var invRows = await db.InventoryItems.AsNoTracking().OrderBy(i => i.Name).ToListAsync(cancellationToken);
        var productsRows = await db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync(cancellationToken);

        var employees = employeesRows
            .Select(e => new AdminReportEntityRefDto(
                e.Id,
                e.UniqueId ?? string.Empty,
                e.Name ?? string.Empty,
                e.Role ?? string.Empty))
            .ToList();
        var tables = tablesRows
            .Select(t => new AdminReportEntityRefDto(
                t.Id,
                t.UniqueId ?? string.Empty,
                $"Table {t.TableNumber} - {t.Name ?? string.Empty}",
                t.Status ?? string.Empty))
            .ToList();
        var inv = invRows
            .Select(i => new AdminReportEntityRefDto(
                i.Id,
                i.UniqueId ?? string.Empty,
                i.Name ?? string.Empty,
                $"{i.StockQuantity:0.##} {i.Unit ?? string.Empty}"))
            .ToList();
        var products = productsRows
            .Select(p => new AdminReportEntityRefDto(
                p.Id,
                p.UniqueId ?? string.Empty,
                p.Name ?? string.Empty,
                $"{p.Category ?? string.Empty} | $ {p.Price:0.00}"))
            .ToList();

        return new AdminReportListsResponse(employees, tables, inv, products);
    }

    public async Task<AdminReportRangeSummaryResponse> GetDailyAsync(DateTime start, DateTime endInclusive, CancellationToken cancellationToken = default)
    {
        var startDay = start.Date;
        var endDay = endInclusive.Date;
        if (endDay < startDay)
            return new AdminReportRangeSummaryResponse("Set a valid date range (end on or after start).", []);

        var endExclusive = endDay.AddDays(1);
        var entries = await BuildDailyEntriesAsync(startDay, endExclusive, cancellationToken);
        var days = PadDaysDescending(
            ApplyDayGroups(entries, dailyPayrollPinnedDaySort: true),
            startDay,
            endDay,
            "0 events | 0 orders | 0 items | 0 units");
        var summary =
            $"Daily timeline {startDay:yyyy-MM-dd} → {endDay:yyyy-MM-dd}: {entries.Count} events (clock-ins/sign-ins, orders, reservations, menu, inventory, payroll records, salary advances, Money salary ledger).";
        return new AdminReportRangeSummaryResponse(summary, days);
    }

    public async Task<AdminReportRangeSummaryResponse> GetOrdersAsync(DateTime start, DateTime endInclusive, CancellationToken cancellationToken = default)
    {
        var startDay = start.Date;
        var endDay = endInclusive.Date;
        if (endDay < startDay)
            return new AdminReportRangeSummaryResponse("Set a valid date range (end on or after start).", []);

        var endExclusive = endDay.AddDays(1);
        var productsById = await LoadProductsByIdAsync(cancellationToken);
        var orders = await OrderReportAnchor.OrderByAnchor(
                db.Orders.AsNoTracking()
                    .Where(o =>
                        (o.PaymentConfirmedAt ?? o.CompletedAt ?? o.CreatedAt) >= startDay
                        && (o.PaymentConfirmedAt ?? o.CompletedAt ?? o.CreatedAt) < endExclusive)
                    .Include(o => o.Items)
                    .ThenInclude(i => i.Product))
            .ToListAsync(cancellationToken);

        HydrateOrderItems(orders, productsById);

        var entries = new List<AdminReportTimeEntryDto>();
        foreach (var order in orders)
        {
            var totalQty = order.Items.Sum(i => i.Quantity);
            var menu = string.Join(", ",
                order.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} ×{i.Quantity}"));
            var subtotal = OrderReportingTotals.ResolveLineSubtotalUsd(order);
            var computedGrandUsd = OrderReportingTotals.ResolveGrandTotalUsd(order);
            var payUsd = OrderReportingTotals.ResolvePaymentUsd(order, computedGrandUsd);
            var payFc = order.PaymentAmountFc;
            var paymentText = CurrencyHelper.FormatDualCurrency(payUsd, payFc);
            var anchor = OrderReportAnchor.Anchor(order);

            entries.Add(new AdminReportTimeEntryDto(
                anchor,
                string.IsNullOrWhiteSpace(order.Status) ? "Order" : order.Status,
                $"Order {DisplayOrFallback(order.UniqueId, $"#{order.Id}")} · {totalQty} item(s)",
                $"{OrderRecordUiLabels.ServerCaption(order)} | {OrderRecordUiLabels.TableCaption(order)} | {DisplayOrFallback(menu, "No line items")}",
                paymentText,
                1,
                totalQty,
                0m));
        }

        var days = PadDaysDescending(
            ApplyOrderDayGroups(entries),
            startDay,
            endDay,
            "0 order(s) | 0 items");
        var summary =
            $"Range {startDay:yyyy-MM-dd} → {endDay:yyyy-MM-dd}. {orders.Count} order(s), {entries.Sum(e => e.ItemCount)} items total.";
        return new AdminReportRangeSummaryResponse(summary, days);
    }

    public async Task<AdminReportEmployeeDetailResponse> GetEmployeeDetailAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.Id == employeeId, cancellationToken);
        if (employee is null)
            return new AdminReportEmployeeDetailResponse("Employee not found.", "-", "-", []);

        var notes = string.IsNullOrWhiteSpace(employee.Notes) ? "-" : employee.Notes;

        var payrollLines = await db.PayrollPaymentRecords.AsNoTracking()
            .Where(p => p.EmployeeId == employee.Id)
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .Take(18)
            .ToListAsync(cancellationToken);

        var payrollHistory =
            payrollLines.Count == 0
                ? "No saved payroll yet. Confirm monthly payment on the Salary screen to record pay here."
                : string.Join(
                    "\n",
                    payrollLines.Select(p =>
                    {
                        var paidLine = p.PaidToDateUsd >= p.NetPayUsd - 0.005m
                            ? $"Paid in full ${p.NetPayUsd:N2} USD"
                            : $"Partial: ${p.PaidToDateUsd:N2} of ${p.NetPayUsd:N2} USD net";
                        return
                            $"{PayrollCalculator.FormatPayrollMonthLabel(p.Year, p.Month)}: {paidLine} (base gross ${p.MonthlySalaryUsd:N2}, sales ${p.MoneyGeneratedUsd:N2}, sales bonus ${p.BonusFivePercentUsd:N2}, advances -${p.AdvancesDeductedUsd:N2}) — last posting {p.PaidAtUtc.ToLocalTime():yyyy-MM-dd}";
                    }));

        var entries = new List<AdminReportTimeEntryDto>();

        var attendanceRows = await db.EmployeeAttendances.AsNoTracking()
            .Where(a => a.EmployeeId == employee.Id)
            .OrderByDescending(a => a.WorkDate)
            .Take(120)
            .ToListAsync(cancellationToken);

        foreach (var row in attendanceRows)
        {
            foreach (var ev in AttendanceReportEntries.BuildForEmployeeDetail(row, employee.Name))
            {
                entries.Add(new AdminReportTimeEntryDto(
                    ev.EventTime,
                    ev.EventType,
                    ev.Summary,
                    ev.RelatedInfo,
                    ev.EntityContext,
                    0,
                    0,
                    0m));
            }
        }

        var productsById = await LoadProductsByIdAsync(cancellationToken);
        var servedOrders = await OrderReportAnchor.OrderByAnchorDescending(
                db.Orders.AsNoTracking()
                    .Where(o => o.ServerId == employee.Id)
                    .Include(o => o.Items)
                    .ThenInclude(i => i.Product))
            .Take(180)
            .ToListAsync(cancellationToken);
        HydrateOrderItems(servedOrders, productsById);

        foreach (var order in servedOrders)
        {
            var totalItems = order.Items.Sum(i => i.Quantity);
            var items = string.Join(", ", order.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} x{i.Quantity}"));
            var anchor = OrderReportAnchor.Anchor(order);
            entries.Add(new AdminReportTimeEntryDto(
                anchor,
                "Served Order",
                $"Order {order.UniqueId} ({order.Status})",
                $"{OrderRecordUiLabels.TableCaption(order)} | {DisplayOrFallback(items, "No order items.")}",
                employee.Name,
                1,
                totalItems,
                0m));
        }

        var empMarker = $"| EMP:{employee.Id}|";
        // Load salary ledger rows with EF-translatable predicates; filter marker/sort in memory (see daily report).
        var salaryMoneyRows = (await db.Transactions.AsNoTracking()
                .Where(t => t.Type == "Expense" && t.Category == "Salary" && t.Justification != null)
                .ToListAsync(cancellationToken))
            .Where(t =>
                MoneyTransactionReportHelper.IsSalaryExpense(t) &&
                t.Justification!.Contains(empMarker, StringComparison.Ordinal))
            .OrderByDescending(t => MoneyTransactionReportHelper.ToLocalInstant(t.Date))
            .Take(48)
            .ToList();

        foreach (var t in salaryMoneyRows)
        {
            entries.Add(new AdminReportTimeEntryDto(
                MoneyTransactionReportHelper.ToLocalInstant(t.Date),
                MoneyTransactionReportHelper.LedgerEventType(t),
                t.Justification ?? string.Empty,
                $"USD $ {t.AmountUsd:0.00}",
                employee.Name,
                0,
                0,
                0m));
        }

        var advanceRows = await db.SalaryAdvances.AsNoTracking()
            .Where(a => a.EmployeeId == employee.Id)
            .OrderByDescending(a => a.GivenAt)
            .Take(36)
            .ToListAsync(cancellationToken);

        foreach (var adv in advanceRows)
        {
            var period = adv.ForPayrollYear.HasValue && adv.ForPayrollMonth.HasValue
                ? PayrollCalculator.FormatPayrollMonthLabel(adv.ForPayrollYear.Value, adv.ForPayrollMonth.Value)
                : "(by date)";
            var applied = adv.AppliedPayrollYear.HasValue && adv.AppliedPayrollMonth.HasValue
                ? $"Applied to {PayrollCalculator.FormatPayrollMonthLabel(adv.AppliedPayrollYear.Value, adv.AppliedPayrollMonth.Value)}"
                : "Pending deduction on payroll confirm";
            entries.Add(new AdminReportTimeEntryDto(
                MoneyTransactionReportHelper.ToLocalInstant(adv.GivenAt),
                "Salary advance (record)",
                $"${adv.AmountUsd:0.00} for payroll {period} — {applied}",
                DisplayOrFallback(adv.Note, "—"),
                employee.Name,
                0,
                0,
                0m));
        }

        var days = ApplyDayGroups(entries);
        var summary =
            $"Name: {employee.Name}\nID: {employee.UniqueId}\nRole: {employee.Role}\nStatus: {employee.EmploymentStatus}\nPhone: {employee.PhoneNumber}\nClock-in / sign-in events: {attendanceRows.Count}\nOrders Served: {servedOrders.Count}\nItems Served: {servedOrders.Sum(o => o.Items.Sum(i => i.Quantity))}";

        return new AdminReportEmployeeDetailResponse(summary, notes, payrollHistory, days);
    }

    public async Task<AdminReportTableDetailResponse> GetTableDetailAsync(int tableId, CancellationToken cancellationToken = default)
    {
        var table = await db.Tables.AsNoTracking()
            .Include(t => t.AssignedServer)
            .SingleOrDefaultAsync(t => t.Id == tableId, cancellationToken);
        if (table is null)
            return new AdminReportTableDetailResponse("Table not found.", "-", []);

        var employees = await db.Employees.AsNoTracking().ToListAsync(cancellationToken);
        var employeesById = employees.GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());
        Employee? assignedServer = table.AssignedServer;
        if (assignedServer is null && table.AssignedServerId is int sid && employeesById.TryGetValue(sid, out var linked))
            assignedServer = linked;

        var entries = new List<AdminReportTimeEntryDto>();
        var productsById = await LoadProductsByIdAsync(cancellationToken);
        var relatedOrders = await OrderReportAnchor.OrderByAnchorDescending(
                db.Orders.AsNoTracking()
                    .Where(o => o.TableId == table.Id)
                    .Include(o => o.Items)
                    .ThenInclude(i => i.Product))
            .Take(220)
            .ToListAsync(cancellationToken);
        HydrateOrderItems(relatedOrders, productsById);

        foreach (var order in relatedOrders)
        {
            var totalItems = order.Items.Sum(i => i.Quantity);
            var items = string.Join(", ", order.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} x{i.Quantity}"));
            var anchor = OrderReportAnchor.Anchor(order);
            entries.Add(new AdminReportTimeEntryDto(
                anchor,
                "Table Order",
                $"Order {order.UniqueId} ({order.Status})",
                $"Server: {OrderRecordUiLabels.ServerCaption(order)} | {DisplayOrFallback(items, "No order items.")}",
                $"Table {table.TableNumber}",
                1,
                totalItems,
                0m));
        }

        var relatedReservations = await db.Reservations.AsNoTracking()
            .Where(r => r.TableId == table.Id)
            .Include(r => r.Table)
            .OrderByDescending(r => r.UpdatedAt)
            .Take(180)
            .ToListAsync(cancellationToken);

        foreach (var reservation in relatedReservations)
        {
            var name = string.IsNullOrWhiteSpace(reservation.ReservationName)
                ? DisplayOrFallback(reservation.GuestName, reservation.UniqueId)
                : reservation.ReservationName.Trim();
            entries.Add(new AdminReportTimeEntryDto(
                reservation.UpdatedAt,
                "Reservation",
                $"{name} ({reservation.Status})",
                $"Reservation {reservation.UniqueId} | Guest: {DisplayOrFallback(reservation.GuestName, "-")} | Party: {reservation.PartySize}",
                $"Table {table.TableNumber}",
                0,
                0,
                0m));
        }

        var days = ApplyDayGroups(entries);
        var summary =
            $"Table: {table.TableNumber} ({table.Name})\nID: {table.UniqueId}\nCapacity: {table.Capacity}\nStatus: {table.Status}\nOrders Logged: {relatedOrders.Count}\nItems Served: {relatedOrders.Sum(o => o.Items.Sum(i => i.Quantity))}";
        var serverLine = assignedServer is null
            ? "Unassigned"
            : $"{assignedServer.Name} ({assignedServer.UniqueId})";

        return new AdminReportTableDetailResponse(summary, serverLine, days);
    }

    public async Task<AdminReportInventoryDetailResponse> GetInventoryDetailAsync(int inventoryId, CancellationToken cancellationToken = default)
    {
        var item = await db.InventoryItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == inventoryId, cancellationToken);
        if (item is null)
            return new AdminReportInventoryDetailResponse("Inventory item not found.", "-", []);

        var notes = string.IsNullOrWhiteSpace(item.Notes) ? "-" : item.Notes;
        var productsById = await LoadProductsByIdAsync(cancellationToken);
        var ingredients = await db.ProductIngredients.AsNoTracking()
            .Where(pi => pi.InventoryItemId == item.Id)
            .ToListAsync(cancellationToken);
        foreach (var pi in ingredients)
        {
            if (productsById.TryGetValue(pi.ProductId, out var p))
                pi.Product = p;
        }

        var entries = new List<AdminReportTimeEntryDto>();
        AppendInventoryNotesTimeline(item.Notes, entries, item.Name);
        var days = ApplyDayGroups(entries);
        var summary =
            $"Item: {item.Name}\nID: {item.UniqueId}\nQuantity: {item.StockQuantity:0.##} {item.Unit}\nExpiration: {item.ExpirationDate?.ToString("yyyy-MM-dd") ?? "Not set"}\nLinked Menu Items: {ingredients.Select(i => i.ProductId).Distinct().Count()}";

        return new AdminReportInventoryDetailResponse(summary, notes, days);
    }

    public async Task<AdminReportMenuDetailResponse> GetMenuDetailAsync(int productId, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
            return new AdminReportMenuDetailResponse("Menu item not found.", "-", []);

        var invRows = await db.InventoryItems.AsNoTracking().ToListAsync(cancellationToken);
        var invById = invRows.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        var ingredients = await db.ProductIngredients.AsNoTracking()
            .Where(pi => pi.ProductId == product.Id)
            .ToListAsync(cancellationToken);
        foreach (var pi in ingredients)
        {
            if (invById.TryGetValue(pi.InventoryItemId, out var inv))
                pi.InventoryItem = inv;
        }

        var ingSummary = ingredients.Count == 0
            ? "No ingredients linked."
            : string.Join(", ", ingredients.Select(i => $"{i.InventoryItem?.Name ?? "Unknown"} ({i.Quantity:0.##} {i.InventoryItem?.Unit ?? "unit"})"));

        var productsById = await LoadProductsByIdAsync(cancellationToken);
        var allOrders = await OrderReportAnchor.OrderByAnchorDescending(
                db.Orders.AsNoTracking()
                    .Include(o => o.Items)
                    .ThenInclude(i => i.Product))
            .Take(800)
            .ToListAsync(cancellationToken);
        HydrateOrderItems(allOrders, productsById);

        var servedLines = allOrders
            .SelectMany(o => o.Items.Where(i => i.ProductId == product.Id).Select(i => (Order: o, Item: i)))
            .OrderByDescending(x => OrderReportAnchor.Anchor(x.Order))
            .Take(300)
            .ToList();

        var entries = new List<AdminReportTimeEntryDto>();
        foreach (var line in servedLines)
        {
            var order = line.Order;
            var anchor = OrderReportAnchor.Anchor(order);
            entries.Add(new AdminReportTimeEntryDto(
                anchor,
                "Menu Ordered",
                $"{product.Name} x{line.Item.Quantity} in order {order.UniqueId}",
                $"{OrderRecordUiLabels.TableCaption(order)} | Server: {OrderRecordUiLabels.ServerCaption(order)} | Status: {order.Status}",
                product.Name,
                1,
                line.Item.Quantity,
                0m));
        }

        var days = ApplyDayGroups(entries);
        var totalQty = servedLines.Sum(l => l.Item.Quantity);
        var summary =
            $"Menu Item: {product.Name}\nID: {product.UniqueId}\nCategory: {product.Category}\nSub Category: {product.SubCategory}\nPrice: $ {product.Price:0.00}\nTimes Ordered: {servedLines.Count}\nUnits Ordered: {totalQty}";

        return new AdminReportMenuDetailResponse(summary, ingSummary, days);
    }

    public async Task<(byte[] Content, string FileName, string ContentType)> ExportAsync(
        string reportType,
        DateTime start,
        DateTime endInclusive,
        CancellationToken cancellationToken = default)
    {
        var startDay = start.Date;
        var endDay = endInclusive.Date;
        if (endDay < startDay)
            throw new ArgumentException("Invalid range.");

        var endExclusive = endDay.AddDays(1);

        if (string.Equals(reportType, "All Reports", StringComparison.OrdinalIgnoreCase))
        {
            var daily = await BuildExportRowsAsync("Daily", startDay, endExclusive, cancellationToken);
            var orders = await BuildExportRowsAsync("Orders", startDay, endExclusive, cancellationToken);
            var employees = await BuildExportRowsAsync("Employees", startDay, endExclusive, cancellationToken);
            var tables = await BuildExportRowsAsync("Tables", startDay, endExclusive, cancellationToken);
            var inventory = await BuildExportRowsAsync("Inventory", startDay, endExclusive, cancellationToken);
            var menu = await BuildExportRowsAsync("Menu", startDay, endExclusive, cancellationToken);
            var bytes = ExcelExportService.ExportWorkbookToByteArray(
            [
                ("Daily", daily.Headers, daily.Rows),
                ("Orders", orders.Headers, orders.Rows),
                ("Employees", employees.Headers, employees.Rows),
                ("Tables", tables.Headers, tables.Rows),
                ("Inventory", inventory.Headers, inventory.Rows),
                ("Menu", menu.Headers, menu.Rows)
            ]);
            return (bytes, $"reports-bulk-{startDay:yyyyMMdd}-{endDay:yyyyMMdd}.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }

        var single = await BuildExportRowsAsync(reportType, startDay, endExclusive, cancellationToken);
        var oneBytes = ExcelExportService.ExportWorkbookToByteArray(
            [(reportType, single.Headers, single.Rows)]);
        return (oneBytes, $"{reportType.ToLowerInvariant()}-report-{startDay:yyyyMMdd}-{endDay:yyyyMMdd}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private async Task<List<AdminReportTimeEntryDto>> BuildDailyEntriesAsync(
        DateTime start,
        DateTime endExclusive,
        CancellationToken cancellationToken)
    {
        var entries = new List<AdminReportTimeEntryDto>();
        var employeeRows = await db.Employees.AsNoTracking().ToListAsync(cancellationToken);
        var employeesById = employeeRows.GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());

        var rangeStartUtc = AttendanceCalendar.DayAnchorUtc(start);
        var rangeEndExclusiveUtc = AttendanceCalendar.DayAnchorUtc(endExclusive);

        var attendanceRows = await db.EmployeeAttendances.AsNoTracking()
            .Where(a => a.WorkDate >= rangeStartUtc && a.WorkDate < rangeEndExclusiveUtc)
            .OrderByDescending(a => a.WorkDate)
            .ToListAsync(cancellationToken);

        foreach (var row in attendanceRows)
        {
            employeesById.TryGetValue(row.EmployeeId, out var emp);
            var name = emp?.Name ?? "Unknown";
            foreach (var ev in AttendanceReportEntries.Build(row, name))
            {
                entries.Add(new AdminReportTimeEntryDto(
                    ev.EventTime,
                    ev.EventType,
                    ev.Summary,
                    ev.RelatedInfo,
                    ev.EntityContext,
                    0,
                    0,
                    0m));
            }
        }

        var productsById = await LoadProductsByIdAsync(cancellationToken);
        var orders = await db.Orders.AsNoTracking()
            .Where(o =>
                (o.PaymentConfirmedAt ?? o.CompletedAt ?? o.CreatedAt) >= start
                && (o.PaymentConfirmedAt ?? o.CompletedAt ?? o.CreatedAt) < endExclusive)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .OrderByDescending(o => o.PaymentConfirmedAt ?? o.CompletedAt ?? o.CreatedAt)
            .ToListAsync(cancellationToken);
        HydrateOrderItems(orders, productsById);

        foreach (var order in orders)
        {
            var itemQty = order.Items.Sum(i => i.Quantity);
            var anchor = OrderReportAnchor.Anchor(order);
            entries.Add(new AdminReportTimeEntryDto(
                anchor,
                OrderOrigin.IsOnline(order.OrderOrigin) ? "Online order" : "Table Service",
                $"Order {order.UniqueId} ({order.Status})",
                $"{OrderRecordUiLabels.TableCaption(order)} | Server: {OrderRecordUiLabels.ServerCaption(order)} | Items: {itemQty}",
                $"Table: {OrderRecordUiLabels.TableCaption(order)}",
                1,
                itemQty,
                0m));
        }

        var tableRows = await db.Tables.AsNoTracking().ToListAsync(cancellationToken);
        var tablesById = tableRows.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());

        var reservations = await db.Reservations.AsNoTracking()
            .Where(r =>
                (r.UpdatedAt >= start && r.UpdatedAt < endExclusive)
                || (r.ReservedFor >= start && r.ReservedFor < endExclusive))
            .OrderByDescending(r => r.ReservedFor)
            .ToListAsync(cancellationToken);

        foreach (var reservation in reservations)
        {
            Table? tbl = null;
            if (reservation.TableId is int tid && tablesById.TryGetValue(tid, out var t))
                tbl = t;

            var displayName = string.IsNullOrWhiteSpace(reservation.ReservationName)
                ? DisplayOrFallback(reservation.GuestName, reservation.UniqueId)
                : reservation.ReservationName.Trim();
            var tableLabel = tbl is not null
                ? $"{tbl.TableNumber} ({DisplayOrFallback(tbl.Name, "-")})"
                : "-";
            var resEvent = ReservationReportTime.DisplayEventTime(reservation, start, endExclusive);
            entries.Add(new AdminReportTimeEntryDto(
                resEvent,
                "Reservation",
                $"Reservation {reservation.UniqueId} · {displayName} · {reservation.Status}",
                $"Reserved for {reservation.ReservedFor:yyyy-MM-dd HH:mm} | Party: {reservation.PartySize} | Table: {tableLabel}",
                "Reservations",
                0,
                0,
                0m));
        }

        foreach (var order in orders)
        {
            var anchor = OrderReportAnchor.Anchor(order);
            foreach (var line in order.Items)
            {
                entries.Add(new AdminReportTimeEntryDto(
                    anchor,
                    "Menu Activity",
                    $"{line.Product?.Name ?? "Unknown"} x{line.Quantity}",
                    $"Order {order.UniqueId} | {OrderRecordUiLabels.TableCaption(order)}",
                    $"Menu: {line.Product?.Name ?? "Unknown"}",
                    1,
                    line.Quantity,
                    0m));
            }
        }

        var inventoryNoteRows = await db.InventoryItems.AsNoTracking()
            .Where(i => !string.IsNullOrWhiteSpace(i.Notes))
            .Select(i => new InventoryNotesSnapshot(i.UniqueId, i.Name, i.Notes!))
            .ToListAsync(cancellationToken);
        AppendInventoryActivityFromNotes(entries, start, endExclusive, inventoryNoteRows);

        var salaryTx = await db.Transactions.AsNoTracking()
            .Where(t => t.Type == "Expense" && t.Category == "Salary")
            .ToListAsync(cancellationToken);

        var payrollRows = await db.PayrollPaymentRecords.AsNoTracking()
            .Include(p => p.Employee)
            .ToListAsync(cancellationToken);

        foreach (var p in payrollRows)
        {
            var paidLocal = p.PaidAtUtc.ToLocalTime();
            if (paidLocal < start || paidLocal >= endExclusive)
                continue;

            employeesById.TryGetValue(p.EmployeeId, out var empFromDict);
            var name = p.Employee?.Name ?? empFromDict?.Name ?? $"Employee #{p.EmployeeId}";
            var monthLabel = PayrollCalculator.FormatPayrollMonthLabel(p.Year, p.Month);
            var paidLine = p.PaidToDateUsd >= p.NetPayUsd - 0.005m
                ? $"Paid in full ${p.NetPayUsd:N2} USD net"
                : $"Paid to date ${p.PaidToDateUsd:N2} of ${p.NetPayUsd:N2} USD net";
            entries.Add(new AdminReportTimeEntryDto(
                paidLocal,
                "Payroll month (record)",
                $"{name} · {monthLabel} · {paidLine}",
                $"Base gross ${p.MonthlySalaryUsd:N2} · Sales ${p.MoneyGeneratedUsd:N2} · Bonus ${p.BonusFivePercentUsd:N2} · Advances deducted ${p.AdvancesDeductedUsd:N2}",
                "Payroll",
                0,
                0,
                0m));
        }

        var advanceRows = await db.SalaryAdvances.AsNoTracking()
            .Include(a => a.Employee)
            .ToListAsync(cancellationToken);

        foreach (var adv in advanceRows)
        {
            var givenLocal = MoneyTransactionReportHelper.ToLocalInstant(adv.GivenAt);
            if (givenLocal < start || givenLocal >= endExclusive)
                continue;

            employeesById.TryGetValue(adv.EmployeeId, out var empFromDict);
            var advName = adv.Employee?.Name ?? empFromDict?.Name ?? $"Employee #{adv.EmployeeId}";
            var period = adv.ForPayrollYear.HasValue && adv.ForPayrollMonth.HasValue
                ? PayrollCalculator.FormatPayrollMonthLabel(adv.ForPayrollYear.Value, adv.ForPayrollMonth.Value)
                : "(by date)";
            var applied = adv.AppliedPayrollYear.HasValue && adv.AppliedPayrollMonth.HasValue
                ? $"Applied to {PayrollCalculator.FormatPayrollMonthLabel(adv.AppliedPayrollYear.Value, adv.AppliedPayrollMonth.Value)}"
                : "Pending deduction on payroll confirm";
            entries.Add(new AdminReportTimeEntryDto(
                givenLocal,
                "Salary advance (record)",
                $"{advName} · ${adv.AmountUsd:0.00} USD for payroll {period} — {applied}",
                DisplayOrFallback(adv.Note, "—"),
                "Salary advances",
                0,
                0,
                0m));
        }

        foreach (var t in salaryTx)
        {
            if (!MoneyTransactionReportHelper.IsSalaryExpense(t))
                continue;
            var local = MoneyTransactionReportHelper.ToLocalInstant(t.Date);
            if (local < start || local >= endExclusive)
                continue;

            _ = MoneyTransactionReportHelper.TryParseEmployeeIdFromSalaryJustification(t.Justification ?? string.Empty, out var eid);
            employeesById.TryGetValue(eid, out var empMoney);
            var who = empMoney?.Name ?? string.Empty;
            var label = MoneyTransactionReportHelper.LedgerEventType(t);
            var summaryText = string.IsNullOrWhiteSpace(who)
                ? (t.Justification ?? string.Empty)
                : $"{who} · {t.Justification}";
            entries.Add(new AdminReportTimeEntryDto(
                local,
                label,
                summaryText,
                $"USD $ {t.AmountUsd:0.00} (Money ledger)",
                "Money",
                0,
                0,
                0m));
        }

        return entries;
    }

    private async Task<Dictionary<int, Product>> LoadProductsByIdAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Products.AsNoTracking().ToListAsync(cancellationToken);
        return rows.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
    }

    private static void HydrateOrderItems(IReadOnlyList<OrderRecord> orders, IReadOnlyDictionary<int, Product> productsById)
    {
        foreach (var o in orders)
        {
            foreach (var i in o.Items)
            {
                i.OrderRecord = o;
                if (productsById.TryGetValue(i.ProductId, out var p))
                    i.Product = p;
            }
        }
    }

    private static List<AdminReportDayGroupDto> PadDaysDescending(
        IReadOnlyList<AdminReportDayGroupDto> filled,
        DateTime rangeStartInclusive,
        DateTime rangeEndInclusive,
        string emptyTotalsText)
    {
        var start = rangeStartInclusive.Date;
        var end = rangeEndInclusive.Date;
        var map = filled.GroupBy(g => g.Day.Date).ToDictionary(g => g.Key, g => g.First());
        var list = new List<AdminReportDayGroupDto>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (map.TryGetValue(d, out var existing))
                list.Add(existing);
            else
            {
                list.Add(new AdminReportDayGroupDto(
                    d,
                    d.ToString("dddd, MMM dd yyyy", CultureInfo.InvariantCulture),
                    emptyTotalsText,
                    Array.Empty<AdminReportTimeEntryDto>()));
            }
        }

        list.Sort((a, b) => b.Day.CompareTo(a.Day));
        return list;
    }

    private static List<AdminReportDayGroupDto> ApplyDayGroups(
        IReadOnlyList<AdminReportTimeEntryDto> entries,
        bool dailyPayrollPinnedDaySort = false)
    {
        if (dailyPayrollPinnedDaySort)
        {
            var listPinned = new List<AdminReportDayGroupDto>();
            foreach (var dayGroup in entries
                .GroupBy(e => OrderReportAnchor.LocalCalendarDay(e.EventTime))
                .OrderByDescending(g => g.Key))
            {
                var rows = dayGroup.ToList();
                rows.Sort((a, b) => ReportDailyTimelineSort.CompareWithinDayPayrollFirstNewestFirst(
                    a.EventTime,
                    a.EventType,
                    b.EventTime,
                    b.EventType));
                var key = dayGroup.Key;
                listPinned.Add(new AdminReportDayGroupDto(
                    key,
                    key.ToString("dddd, MMM dd yyyy", CultureInfo.InvariantCulture),
                    $"{rows.Count} events | {rows.Sum(r => r.OrdersCount)} orders | {rows.Sum(r => r.ItemCount)} items | {rows.Sum(r => r.UnitUsage):0.##} units",
                    rows));
            }

            return listPinned;
        }

        var normalized = entries
            .OrderByDescending(e => e.EventTime)
            .ThenBy(e => e.EventType)
            .ToList();

        var list = new List<AdminReportDayGroupDto>();
        foreach (var dayGroup in normalized
            .GroupBy(e => OrderReportAnchor.LocalCalendarDay(e.EventTime))
            .OrderByDescending(g => g.Key))
        {
            var rows = dayGroup.ToList();
            var key = dayGroup.Key;
            list.Add(new AdminReportDayGroupDto(
                key,
                key.ToString("dddd, MMM dd yyyy", CultureInfo.InvariantCulture),
                $"{rows.Count} events | {rows.Sum(r => r.OrdersCount)} orders | {rows.Sum(r => r.ItemCount)} items | {rows.Sum(r => r.UnitUsage):0.##} units",
                rows));
        }

        return list;
    }

    private static List<AdminReportDayGroupDto> ApplyOrderDayGroups(List<AdminReportTimeEntryDto> entries)
    {
        var list = new List<AdminReportDayGroupDto>();
        foreach (var dayGroup in entries.GroupBy(e => OrderReportAnchor.LocalCalendarDay(e.EventTime)).OrderByDescending(g => g.Key))
        {
            var rows = dayGroup.OrderBy(e => e.EventTime).ThenBy(e => e.Summary).ToList();
            var orderCount = rows.Sum(r => r.OrdersCount);
            list.Add(new AdminReportDayGroupDto(
                dayGroup.Key,
                dayGroup.Key.ToString("dddd, MMM dd yyyy", CultureInfo.InvariantCulture),
                $"{orderCount} order(s) | {rows.Sum(r => r.ItemCount)} items",
                rows));
        }

        return list;
    }

    private sealed record InventoryNotesSnapshot(string UniqueId, string Name, string Notes);

    private static void AppendInventoryActivityFromNotes(
        List<AdminReportTimeEntryDto> entries,
        DateTime start,
        DateTime endExclusive,
        IReadOnlyList<InventoryNotesSnapshot> inventoryNoteRows)
    {
        foreach (var row in inventoryNoteRows)
        {
            foreach (var rawLine in row.Notes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var ts = TryParseLeadingTimestamp(line);
                if (ts is null || ts.Value < start || ts.Value >= endExclusive)
                    continue;

                entries.Add(new AdminReportTimeEntryDto(
                    ts.Value,
                    "Inventory Activity",
                    line,
                    $"Item: {row.Name} ({row.UniqueId})",
                    $"Inventory: {row.Name}",
                    0,
                    0,
                    0m));
            }
        }
    }

    private static void AppendInventoryNotesTimeline(string? notes, ICollection<AdminReportTimeEntryDto> entries, string itemName)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return;

        var lines = notes
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        foreach (var line in lines)
        {
            var timestamp = TryParseLeadingTimestamp(line);
            entries.Add(new AdminReportTimeEntryDto(
                timestamp ?? DateTime.Today.AddHours(12),
                "Inventory Note",
                line,
                "Text-based inventory history entry",
                itemName,
                0,
                0,
                0m));
        }
    }

    private static DateTime? TryParseLeadingTimestamp(string line)
    {
        if (line.Length < 16)
            return null;

        var maybeDate = line[..16];
        if (DateTime.TryParseExact(
                maybeDate,
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
            return parsed;

        return null;
    }

    private static string DisplayOrFallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private async Task<(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows)> BuildExportRowsAsync(
        string reportType,
        DateTime start,
        DateTime endExclusive,
        CancellationToken cancellationToken)
    {
        if (reportType == "Orders")
        {
            var productsById = await LoadProductsByIdAsync(cancellationToken);
            var orders = await OrderReportAnchor.OrderByAnchor(
                    db.Orders.AsNoTracking()
                        .Where(o =>
                            (o.PaymentConfirmedAt ?? o.CompletedAt ?? o.CreatedAt) >= start
                            && (o.PaymentConfirmedAt ?? o.CompletedAt ?? o.CreatedAt) < endExclusive)
                        .Include(o => o.Items)
                        .ThenInclude(i => i.Product))
                .ToListAsync(cancellationToken);
            HydrateOrderItems(orders, productsById);
            return BuildOrderExportRows(orders);
        }

        var employeeRows = await db.Employees.AsNoTracking().ToListAsync(cancellationToken);
        var employeesById = employeeRows.GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());
        var tableRows = await db.Tables.AsNoTracking().ToListAsync(cancellationToken);
        var tablesById = tableRows.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());

        var rows = new List<IReadOnlyList<string>>();

        var includeAttendance = reportType is "Daily" or "Employees";
        var includeOrders = reportType is "Daily" or "Employees" or "Tables" or "Menu";
        var includeReservations = reportType is "Daily" or "Tables";

        if (includeAttendance)
        {
            var attendanceStartUtc = AttendanceCalendar.DayAnchorUtc(start.Date);
            var attendanceEndExclusiveUtc = AttendanceCalendar.DayAnchorUtc(endExclusive.Date);
            var attendanceRows = await db.EmployeeAttendances.AsNoTracking()
                .Where(a => a.WorkDate >= attendanceStartUtc && a.WorkDate < attendanceEndExclusiveUtc)
                .OrderBy(a => a.WorkDate)
                .ThenBy(a => a.EmployeeId)
                .ToListAsync(cancellationToken);

            foreach (var row in attendanceRows)
            {
                employeesById.TryGetValue(row.EmployeeId, out var empAttach);
                var empName = empAttach?.Name ?? string.Empty;
                foreach (var ev in AttendanceReportEntries.Build(row, empName))
                {
                    rows.Add(BuildAnalyticalRow(
                        eventTime: ev.EventTime,
                        eventType: ev.EventType,
                        employeeId: empAttach?.UniqueId ?? string.Empty,
                        employeeName: empName,
                        orderId: ev.Summary.Length > 120 ? ev.Summary[..120] + "…" : ev.Summary,
                        costOrPrice: string.Empty));
                }
            }
        }

        var includeSalaryLedger = reportType is "Daily" or "Employees";
        if (includeSalaryLedger)
        {
            var salaryTx = await db.Transactions.AsNoTracking()
                .Where(t => t.Type == "Expense" && t.Category == "Salary")
                .ToListAsync(cancellationToken);

            foreach (var t in salaryTx)
            {
                if (!MoneyTransactionReportHelper.IsSalaryExpense(t))
                    continue;
                var local = MoneyTransactionReportHelper.ToLocalInstant(t.Date);
                if (local < start || local >= endExclusive)
                    continue;

                _ = MoneyTransactionReportHelper.TryParseEmployeeIdFromSalaryJustification(t.Justification ?? string.Empty, out var eid);
                employeesById.TryGetValue(eid, out var empRef);
                var j = t.Justification ?? string.Empty;
                if (j.Length > 120)
                    j = j[..120] + "…";

                rows.Add(BuildAnalyticalRow(
                    eventTime: local,
                    eventType: MoneyTransactionReportHelper.LedgerEventType(t),
                    employeeId: empRef?.UniqueId ?? string.Empty,
                    employeeName: empRef?.Name ?? string.Empty,
                    orderId: j,
                    costOrPrice: t.AmountUsd.ToString("0.00", CultureInfo.InvariantCulture)));
            }

            var payrollRows = await db.PayrollPaymentRecords.AsNoTracking()
                .Include(p => p.Employee)
                .ToListAsync(cancellationToken);

            foreach (var p in payrollRows)
            {
                var paidLocal = p.PaidAtUtc.ToLocalTime();
                if (paidLocal < start || paidLocal >= endExclusive)
                    continue;

                employeesById.TryGetValue(p.EmployeeId, out var empRef);
                var monthLabel = PayrollCalculator.FormatPayrollMonthLabel(p.Year, p.Month);
                var detail =
                    $"{monthLabel} · paid ${p.PaidToDateUsd:0.00} / net ${p.NetPayUsd:0.00} · base ${p.MonthlySalaryUsd:0.00} · sales ${p.MoneyGeneratedUsd:0.00} · bonus ${p.BonusFivePercentUsd:0.00} · adv -${p.AdvancesDeductedUsd:0.00}";
                rows.Add(BuildAnalyticalRow(
                    eventTime: paidLocal,
                    eventType: "Payroll month (record)",
                    employeeId: empRef?.UniqueId ?? string.Empty,
                    employeeName: empRef?.Name ?? p.Employee?.Name ?? string.Empty,
                    orderId: detail));
            }

            var advanceRows = await db.SalaryAdvances.AsNoTracking()
                .Include(a => a.Employee)
                .ToListAsync(cancellationToken);

            foreach (var adv in advanceRows)
            {
                var givenLocal = MoneyTransactionReportHelper.ToLocalInstant(adv.GivenAt);
                if (givenLocal < start || givenLocal >= endExclusive)
                    continue;

                employeesById.TryGetValue(adv.EmployeeId, out var empRef);
                var period = adv.ForPayrollYear.HasValue && adv.ForPayrollMonth.HasValue
                    ? PayrollCalculator.FormatPayrollMonthLabel(adv.ForPayrollYear.Value, adv.ForPayrollMonth.Value)
                    : "(by date)";
                var applied = adv.AppliedPayrollYear.HasValue && adv.AppliedPayrollMonth.HasValue
                    ? $"Applied to {PayrollCalculator.FormatPayrollMonthLabel(adv.AppliedPayrollYear.Value, adv.AppliedPayrollMonth.Value)}"
                    : "Pending deduction on payroll confirm";
                var note = string.IsNullOrWhiteSpace(adv.Note) ? "—" : adv.Note.Trim();
                rows.Add(BuildAnalyticalRow(
                    eventTime: givenLocal,
                    eventType: "Salary advance (record)",
                    employeeId: empRef?.UniqueId ?? string.Empty,
                    employeeName: empRef?.Name ?? adv.Employee?.Name ?? string.Empty,
                    orderId: $"${adv.AmountUsd:0.00} for {period} — {applied} — {note}"));
            }
        }

        if (includeOrders)
        {
            var productsById = await LoadProductsByIdAsync(cancellationToken);
            var ordersForLines = await OrderReportAnchor.OrderByAnchor(
                    db.Orders.AsNoTracking()
                        .Where(o =>
                            (o.PaymentConfirmedAt ?? o.CompletedAt ?? o.CreatedAt) >= start
                            && (o.PaymentConfirmedAt ?? o.CompletedAt ?? o.CreatedAt) < endExclusive)
                        .Include(o => o.Items)
                        .ThenInclude(i => i.Product))
                .ToListAsync(cancellationToken);
            HydrateOrderItems(ordersForLines, productsById);
            var orderItems = ordersForLines
                .SelectMany(o => o.Items)
                .OrderBy(i => i.OrderRecord is null ? DateTime.MinValue : OrderReportAnchor.Anchor(i.OrderRecord))
                .ThenBy(i => i.Id)
                .ToList();

            foreach (var line in orderItems)
            {
                var order = line.OrderRecord;
                if (order is null)
                    continue;

                employeesById.TryGetValue(line.PreparedByEmployeeId ?? 0, out var preparedByEmployee);
                employeesById.TryGetValue(order.ServerId ?? 0, out var serverEmployee);
                tablesById.TryGetValue(order.TableId ?? 0, out var table);
                var anchor = OrderReportAnchor.Anchor(order);

                rows.Add(BuildAnalyticalRow(
                    eventTime: anchor,
                    eventType: "Order",
                    employeeId: preparedByEmployee?.UniqueId ?? string.Empty,
                    employeeName: line.PreparedByName,
                    serverId: serverEmployee?.UniqueId ?? string.Empty,
                    serverName: OrderRecordUiLabels.ServerCaption(order),
                    orderId: order.UniqueId,
                    tableId: table?.UniqueId ?? string.Empty,
                    tableName: OrderRecordUiLabels.TableCaption(order),
                    productId: line.Product?.UniqueId ?? string.Empty,
                    productName: line.Product?.Name ?? string.Empty,
                    quantity: line.Quantity.ToString(CultureInfo.InvariantCulture),
                    costOrPrice: line.Product?.Price.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty,
                    chefId: line.PreparedByRole == "Chef" ? preparedByEmployee?.UniqueId ?? string.Empty : string.Empty,
                    chefName: line.PreparedByRole == "Chef" ? line.PreparedByName : string.Empty,
                    barmanId: line.PreparedByRole == "Barman" ? preparedByEmployee?.UniqueId ?? string.Empty : string.Empty,
                    barmanName: line.PreparedByRole == "Barman" ? line.PreparedByName : string.Empty));
            }
        }

        if (reportType is "Daily" or "Inventory")
        {
            var inventoryActivityItems = await db.InventoryItems.AsNoTracking()
                .Where(i => !string.IsNullOrWhiteSpace(i.Notes))
                .ToListAsync(cancellationToken);

            foreach (var item in inventoryActivityItems)
            {
                foreach (var rawLine in item.Notes!.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var ts = TryParseLeadingTimestamp(line);
                    if (ts is null || ts.Value < start || ts.Value >= endExclusive)
                        continue;

                    var orderCol = line.Length > 200 ? line[..200] + "…" : line;
                    rows.Add(BuildAnalyticalRow(
                        eventTime: ts.Value,
                        eventType: "Inventory activity",
                        orderId: orderCol,
                        ingredientId: item.UniqueId,
                        ingredientName: item.Name));
                }
            }
        }

        if (includeReservations)
        {
            var reservationRows = await db.Reservations.AsNoTracking()
                .Where(r =>
                    (r.UpdatedAt >= start && r.UpdatedAt < endExclusive)
                    || (r.ReservedFor >= start && r.ReservedFor < endExclusive))
                .OrderBy(r => r.ReservedFor)
                .ThenBy(r => r.Id)
                .ToListAsync(cancellationToken);

            foreach (var reservation in reservationRows)
            {
                tablesById.TryGetValue(reservation.TableId ?? 0, out var table);
                var reservationName = string.IsNullOrWhiteSpace(reservation.ReservationName)
                    ? DisplayOrFallback(reservation.GuestName, reservation.UniqueId)
                    : reservation.ReservationName.Trim();

                rows.Add(BuildAnalyticalRow(
                    eventTime: ReservationReportTime.DisplayEventTime(reservation, start, endExclusive),
                    eventType: "Reservation",
                    orderId: reservation.UniqueId,
                    tableId: table?.UniqueId ?? string.Empty,
                    tableName: table is null ? string.Empty : $"Table {table.TableNumber} - {table.Name}",
                    productId: string.Empty,
                    productName: reservationName,
                    quantity: reservation.PartySize.ToString(CultureInfo.InvariantCulture),
                    costOrPrice: reservation.DepositAmountUsd.ToString("0.00", CultureInfo.InvariantCulture)));
            }
        }

        rows = rows
            .OrderByDescending(ParseAnalyticalRowInstant)
            .ThenByDescending(r => r.Count > 3 && ReportDailyTimelineSort.IsPinnedPayrollSalaryEventType(r[3]))
            .ThenBy(r => r[3], StringComparer.Ordinal)
            .ToList();

        return (AnalyticalHeaders, rows);
    }

    private static DateTime ParseAnalyticalRowInstant(IReadOnlyList<string> r)
    {
        if (r.Count < 3)
            return DateTime.MinValue;

        if (DateTime.TryParseExact(
                $"{r[0]} {r[2]}",
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dt))
            return dt;

        return DateTime.MinValue;
    }

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildOrderExportRows(
        IReadOnlyList<OrderRecord> orders)
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach (var order in orders)
        {
            var totalQty = order.Items.Sum(i => i.Quantity);
            var menu = string.Join("; ",
                order.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} ×{i.Quantity}"));
            var subtotal = OrderReportingTotals.ResolveLineSubtotalUsd(order);
            var computedGrandUsd = OrderReportingTotals.ResolveGrandTotalUsd(order);
            var payUsd = OrderReportingTotals.ResolvePaymentUsd(order, computedGrandUsd);
            var payFc = order.PaymentAmountFc;
            var discountUsd = order.DiscountAmountUsd > 0m
                ? order.DiscountAmountUsd
                : OrderTotalsHelper.ComputeTotals(
                    subtotal,
                    order.DiscountMode,
                    order.DiscountValue,
                    order.TaxPercentApplied,
                    order.ServicePercentApplied).DiscountApplied;
            var deliveryFeeUsd = Math.Round(Math.Max(0m, order.DeliveryFeeUsd), 2);
            var anchor = OrderReportAnchor.Anchor(order);

            rows.Add(
            [
                anchor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                anchor.ToString("dddd", CultureInfo.InvariantCulture),
                anchor.ToString("HH:mm", CultureInfo.InvariantCulture),
                DisplayOrFallback(order.UniqueId, order.Id.ToString(CultureInfo.InvariantCulture)),
                order.Status,
                DisplayOrFallback(order.ServerName, "Unassigned"),
                DisplayOrFallback(order.TableCode, string.Empty),
                DisplayOrFallback(order.TableName, string.Empty),
                menu,
                totalQty.ToString(CultureInfo.InvariantCulture),
                order.Items.Count.ToString(CultureInfo.InvariantCulture),
                payUsd.ToString("0.00", CultureInfo.InvariantCulture),
                payFc.ToString("0.##", CultureInfo.InvariantCulture),
                order.PaymentCurrencyCode,
                discountUsd.ToString("0.00", CultureInfo.InvariantCulture),
                deliveryFeeUsd.ToString("0.00", CultureInfo.InvariantCulture),
                computedGrandUsd.ToString("0.00", CultureInfo.InvariantCulture)
            ]);
        }

        return (OrderReportHeaders, rows);
    }

    private static readonly IReadOnlyList<string> OrderReportHeaders =
    [
        "Date",
        "Day",
        "Time",
        "Order ID",
        "Status",
        "Server",
        "Table Code",
        "Table Name",
        "Menu (lines)",
        "Item Qty",
        "Line Count",
        "Total USD",
        "Total FC",
        "Pay Currency",
        "Discount USD",
        "Delivery Fee USD",
        "Computed Grand USD"
    ];

    private static readonly IReadOnlyList<string> AnalyticalHeaders =
    [
        "Date",
        "Day",
        "Time",
        "Event Type",
        "Employee ID",
        "Employee Name",
        "Server ID",
        "Server Name",
        "Order ID",
        "Table ID",
        "Table Name",
        "Product ID",
        "Product Name",
        "Qty",
        "Ingredient ID",
        "Ingredient Name",
        "Unit",
        "Cost/Price",
        "Cashier ID",
        "Cashier Name",
        "Chef ID",
        "Chef Name",
        "Barman ID",
        "Barman Name"
    ];

    private static IReadOnlyList<string> BuildAnalyticalRow(
        DateTime eventTime,
        string eventType,
        string employeeId = "",
        string employeeName = "",
        string serverId = "",
        string serverName = "",
        string orderId = "",
        string tableId = "",
        string tableName = "",
        string productId = "",
        string productName = "",
        string quantity = "",
        string ingredientId = "",
        string ingredientName = "",
        string unit = "",
        string costOrPrice = "",
        string cashierId = "",
        string cashierName = "",
        string chefId = "",
        string chefName = "",
        string barmanId = "",
        string barmanName = "")
    {
        return
        [
            eventTime.ToString("yyyy-MM-dd"),
            eventTime.ToString("dddd", CultureInfo.InvariantCulture),
            eventTime.ToString("HH:mm"),
            eventType,
            employeeId,
            employeeName,
            serverId,
            serverName,
            orderId,
            tableId,
            tableName,
            productId,
            productName,
            quantity,
            ingredientId,
            ingredientName,
            unit,
            costOrPrice,
            cashierId,
            cashierName,
            chefId,
            chefName,
            barmanId,
            barmanName
        ];
    }
}
