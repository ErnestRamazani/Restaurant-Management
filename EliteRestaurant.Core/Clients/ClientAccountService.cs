using System.Data;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Clients;

public sealed class ClientAccountService(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public decimal GetDebtCapUsd() => ClientDebtSettingsHelper.ResolveDebtCapUsd(_db);

    public RestaurantClient? GetById(int id) =>
        _db.RestaurantClients.FirstOrDefault(c => c.Id == id);

    public (string? Error, RestaurantClient? Created) TryCreateClient(
        string fullName,
        string? phone,
        string? email,
        string? notes)
    {
        var name = (fullName ?? string.Empty).Trim();
        if (name.Length < 2)
            return ("Full name is required (at least 2 characters).", null);

        var phoneNorm = NormalizePhone(phone);
        if (!string.IsNullOrEmpty(phoneNorm)
            && _db.RestaurantClients.Any(c => !c.IsStaffClient && c.PrimaryPhone == phoneNorm))
            return ("Another client already uses this phone number.", null);

        var client = new RestaurantClient
        {
            UniqueId = UniqueIdGenerator.NewId("CLT"),
            FullName = name,
            PrimaryPhone = phoneNorm,
            Email = (email ?? string.Empty).Trim(),
            InternalNotes = (notes ?? string.Empty).Trim(),
            DebtBalanceUsd = 0m,
            IsStaffClient = false,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.RestaurantClients.Add(client);
        try
        {
            _db.SaveChanges();
        }
        catch (DbUpdateException ex) when (IsDuplicateClientName(ex))
        {
            return ("A client with this name already exists for this restaurant.", null);
        }

        return (null, client);
    }

    public string? TryUpdateClient(int id, string fullName, string? phone, string? email, string? notes, bool isActive)
    {
        var client = _db.RestaurantClients.FirstOrDefault(c => c.Id == id);
        if (client is null)
            return "Client not found.";
        if (client.IsStaffClient)
            return "Staff client records are updated from the Employees screen.";

        var name = (fullName ?? string.Empty).Trim();
        if (name.Length < 2)
            return "Full name is required.";

        var phoneNorm = NormalizePhone(phone);
        if (!string.IsNullOrEmpty(phoneNorm)
            && _db.RestaurantClients.Any(c => !c.IsStaffClient && c.Id != id && c.PrimaryPhone == phoneNorm))
            return "Another client already uses this phone number.";

        client.FullName = name;
        client.PrimaryPhone = phoneNorm;
        client.Email = (email ?? string.Empty).Trim();
        client.InternalNotes = (notes ?? string.Empty).Trim();
        client.IsActive = isActive;
        client.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();
        return null;
    }

    public IReadOnlyList<RestaurantClient> ListAll(bool includeInactive = false)
    {
        var q = _db.RestaurantClients.AsNoTracking().AsQueryable();
        if (!includeInactive)
            q = q.Where(c => c.IsActive);
        return q.OrderBy(c => c.IsStaffClient).ThenBy(c => c.FullName).ToList();
    }

    public IReadOnlyList<RestaurantClient> Search(string? query, int max = 25)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length == 0)
            return ListAll().Take(max).ToList();

        var needle = q.ToLowerInvariant();
        var phoneNeedle = NormalizePhone(q);
        return _db.RestaurantClients.AsNoTracking()
            .Where(c => c.IsActive)
            .Where(c =>
                c.FullName.ToLower().Contains(needle)
                || c.UniqueId.ToLower().Contains(needle)
                || (!string.IsNullOrEmpty(c.PrimaryPhone) && c.PrimaryPhone.Contains(phoneNeedle)))
            .OrderBy(c => c.IsStaffClient)
            .ThenBy(c => c.FullName)
            .Take(max)
            .ToList();
    }

    public void SyncStaffClientFromEmployee(Employee employee)
    {
        ApplyStaffClientMirror(employee);
        _db.SaveChanges();
    }

    /// <summary>Creates or updates staff client rows for all employees (e.g. after deploy or before listing clients).</summary>
    public void EnsureStaffClientsFromEmployees()
    {
        var employees = _db.Employees
            .Where(e => e.EmploymentStatus == "Active")
            .ToList();
        foreach (var employee in employees)
        {
            try
            {
                ApplyStaffClientMirror(employee);
                _db.SaveChanges();
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
            }
        }
    }

    private void ApplyStaffClientMirror(Employee employee)
    {
        if (employee.Id <= 0)
            return;

        var name = string.IsNullOrWhiteSpace(employee.Name) ? $"Staff #{employee.Id}" : employee.Name.Trim();
        var active = string.Equals(employee.EmploymentStatus, "Active", StringComparison.OrdinalIgnoreCase);

        var existing = _db.RestaurantClients.FirstOrDefault(c => c.EmployeeId == employee.Id)
            ?? _db.RestaurantClients.FirstOrDefault(c =>
                c.IsStaffClient && c.FullName == name && c.EmployeeId == null);

        var phone = ResolveStaffClientPhone(employee.PhoneNumber, employee.Id, existing?.Id);

        if (existing is null)
        {
            _db.RestaurantClients.Add(new RestaurantClient
            {
                UniqueId = UniqueIdGenerator.NewId("CLT"),
                FullName = name,
                PrimaryPhone = phone,
                Email = string.Empty,
                InternalNotes = $"Staff · {employee.Role}",
                DebtBalanceUsd = 0m,
                IsStaffClient = true,
                EmployeeId = employee.Id,
                IsActive = active,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            return;
        }

        existing.FullName = name;
        existing.IsStaffClient = true;
        existing.EmployeeId = employee.Id;
        existing.PrimaryPhone = phone;
        existing.InternalNotes = $"Staff · {employee.Role}";
        existing.IsActive = active;
        existing.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static bool IsDuplicateClientName(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("IX_RestaurantClients_RestaurantId_Name", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                   && msg.Contains("RestaurantClients", StringComparison.OrdinalIgnoreCase);
    }

    public void TryLinkNewOrder(int orderId, int? restaurantClientId)
    {
        if (restaurantClientId is int id && id > 0)
            TryLinkOrderToClient(orderId, id);
    }

    public string? TryLinkOrderToClient(int orderId, int restaurantClientId)
    {
        var order = _db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefault(o => o.Id == orderId);
        if (order is null)
            return "Order not found.";

        var client = _db.RestaurantClients
            .Include(c => c.Employee)
            .FirstOrDefault(c => c.Id == restaurantClientId && c.IsActive);
        if (client is null)
            return "Client not found.";
        if (client.Employee is null && client.EmployeeId is int eid)
            client.Employee = _db.Employees.AsNoTracking().FirstOrDefault(e => e.Id == eid);

        order.RestaurantClientId = client.Id;
        ApplyStaffDiscountIfNeeded(order, client);
        _db.SaveChanges();
        return null;
    }

    public static void ApplyStaffDiscountIfNeeded(OrderRecord order, RestaurantClient client)
    {
        if (!client.IsStaffClient || client.EmployeeId is null)
            return;

        var percent = client.Employee?.StaffMealDiscountPercent ?? 0m;
        if (percent <= 0m)
            return;

        order.DiscountMode = "Percent";
        order.DiscountValue = Math.Min(100m, Math.Max(0m, percent));
    }

    public decimal ComputeOrderGrandTotalUsd(OrderRecord order)
    {
        var items = order.Items?.ToList() ?? _db.OrderItems.AsNoTracking()
            .Where(i => i.OrderRecordId == order.Id)
            .ToList();
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var prices = _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p.Price);
        var lineSub = items.Sum(i =>
            (prices.TryGetValue(i.ProductId, out var p) ? p : 0m) * i.Quantity);
        return OrderTotalsHelper.ComputeTotalsWithDeliveryFee(
            lineSub,
            order.DiscountMode,
            order.DiscountValue,
            order.DeliveryFeeUsd).GrandTotal;
    }

    public string? TryCompleteOrderOnAccount(int orderId, int? employeeId)
    {
        var order = _db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefault(o => o.Id == orderId);
        if (order is null)
            return "Order not found.";
        if (order.RestaurantClientId is null)
            return "Link this order to a client before adding to their account.";
        if (Models.ClientSettlement.IsOnAccount(order.ClientSettlement) && order.AmountOnAccountUsd > 0m)
        {
            if (order.ClientDebtSettledUsd >= order.AmountOnAccountUsd - 0.01m)
                return "This order debt is already settled.";
            return "This order is already on the client's account.";
        }
        if (string.Equals(order.Status, "Completed", StringComparison.OrdinalIgnoreCase)
            && !Models.ClientSettlement.IsOnAccount(order.ClientSettlement))
            return "Order is already completed.";

        var client = _db.RestaurantClients.FirstOrDefault(c => c.Id == order.RestaurantClientId);
        if (client is null)
            return "Client not found.";

        var amount = order.MerchandiseGrandTotalUsd > 0m
            ? Math.Round(order.MerchandiseGrandTotalUsd, 2)
            : order.PaymentAmountUsd > 0m
                ? Math.Round(order.PaymentAmountUsd, 2)
                : Math.Round(ComputeOrderGrandTotalUsd(order), 2);
        if (amount <= 0m)
            return "Order total must be greater than zero.";

        var cap = GetDebtCapUsd();
        if (client.DebtBalanceUsd >= cap)
            return $"Client debt is at the ${cap:N0} limit. Collect payment before adding more debt.";

        if (client.DebtBalanceUsd + amount > cap)
            return $"This charge would exceed the ${cap:N0} debt limit (current ${client.DebtBalanceUsd:N2}, ticket ${amount:N2}).";

        order.Status = "Completed";
        order.ClientSettlement = Models.ClientSettlement.OnAccount;
        order.AmountOnAccountUsd = amount;
        order.ClientDebtSettledUsd = 0m;
        order.CompletedAt = DateTime.Now;
        order.PaymentConfirmedAt = null;
        order.PaymentAmountUsd = 0m;
        order.PaymentAmountFc = 0m;
        order.PaymentAmount = 0m;
        order.CustomerPaidUsd = 0m;
        order.CustomerPaidFc = 0m;
        order.ChangeGivenUsd = 0m;
        order.ChangeGivenFc = 0m;

        client.DebtBalanceUsd = Math.Round(client.DebtBalanceUsd + amount, 2);
        client.UpdatedAtUtc = DateTime.UtcNow;

        if (!_db.ClientDebtLedgerEntries.Any(e =>
                e.OrderId == order.Id && e.EntryType == ClientDebtLedgerEntryType.Charge))
        {
            _db.ClientDebtLedgerEntries.Add(new ClientDebtLedgerEntry
            {
                RestaurantClientId = client.Id,
                OrderId = order.Id,
                EntryType = ClientDebtLedgerEntryType.Charge,
                AmountUsd = amount,
                BalanceAfterUsd = client.DebtBalanceUsd,
                Note = $"Order {order.UniqueId} on account",
                CreatedByEmployeeId = employeeId,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        DataReconciler.ReconcileTableStatusesWithOrders(_db);
        _db.SaveChanges();
        return null;
    }

    public string? TryCompleteOrderPaid(
        int orderId,
        string? paymentCurrencyOverride,
        decimal paidUsd,
        decimal paidFc,
        decimal changeUsd,
        decimal changeFc,
        AdminOrderOperationsService orderOps)
    {
        var order = _db.Orders.AsNoTracking().FirstOrDefault(o => o.Id == orderId);
        if (order?.RestaurantClientId is int cid)
        {
            var tracked = _db.Orders.First(o => o.Id == orderId);
            tracked.ClientSettlement = Models.ClientSettlement.PaidAtCompletion;
            _db.SaveChanges();
        }

        try
        {
            orderOps.UpdateOrderStatus(orderId, "Completed", paymentCurrencyOverride, paidUsd, paidFc, changeUsd, changeFc);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }

        var after = _db.Orders.AsNoTracking().First(o => o.Id == orderId);
        if (after.RestaurantClientId is not null
            && !Models.ClientSettlement.IsOnAccount(after.ClientSettlement)
            && string.IsNullOrWhiteSpace(after.ClientSettlement))
        {
            var t = _db.Orders.First(o => o.Id == orderId);
            t.ClientSettlement = Models.ClientSettlement.PaidAtCompletion;
            _db.SaveChanges();
        }

        return null;
    }

    public (bool Ok, string? Message, decimal Applied, decimal Remaining) TrySettleDebt(
        int clientId,
        decimal paymentAmountUsd,
        string? passcode,
        int? employeeId,
        string? note)
    {
        var passErr = OrderCancelPasscodeHelper.Validate(_db, passcode);
        if (passErr is not null)
            return (false, passErr, 0m, 0m);

        var amount = Math.Round(Math.Max(0m, paymentAmountUsd), 2);
        if (amount <= 0m)
            return (false, "Enter a payment amount greater than zero.", 0m, 0m);

        return DatabaseResilientTransaction.Execute(_db, () =>
        {
            if (IsInMemoryDatabase(_db))
                return SettleDebtCore(clientId, amount, employeeId, note);

            using var tx = _db.Database.BeginTransaction(IsolationLevel.Serializable);
            try
            {
                var result = SettleDebtCore(clientId, amount, employeeId, note);
                if (result.Ok)
                    tx.Commit();
                else
                    tx.Rollback();
                return result;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        });
    }

    private (bool Ok, string? Message, decimal Applied, decimal Remaining) SettleDebtCore(
        int clientId,
        decimal amount,
        int? employeeId,
        string? note)
    {
        var client = _db.RestaurantClients.FirstOrDefault(c => c.Id == clientId);
        if (client is null)
            return (false, "Client not found.", 0m, 0m);
        if (client.DebtBalanceUsd <= 0m)
            return (false, "This client has no open debt.", 0m, 0m);

        var maxApply = Math.Min(amount, client.DebtBalanceUsd);
        if (maxApply <= 0m)
            return (false, "This client has no open debt.", 0m, 0m);

        var remainingPayment = maxApply;
        var allocatedUsd = 0m;
        var paymentNote = string.IsNullOrWhiteSpace(note) ? "Debt payment" : note!.Trim();

        var openOrders = _db.Orders
            .Where(o => o.RestaurantClientId == clientId
                        && o.ClientSettlement == Models.ClientSettlement.OnAccount
                        && o.ClientDebtSettledUsd < o.AmountOnAccountUsd - 0.001m)
            .OrderBy(o => o.CompletedAt ?? o.CreatedAt)
            .ThenBy(o => o.Id)
            .ToList();

        foreach (var order in openOrders)
        {
            if (remainingPayment <= 0m)
                break;

            var owedOnOrder = Math.Round(order.AmountOnAccountUsd - order.ClientDebtSettledUsd, 2);
            if (owedOnOrder <= 0m)
                continue;

            var alloc = Math.Min(remainingPayment, owedOnOrder);
            order.ClientDebtSettledUsd = Math.Round(order.ClientDebtSettledUsd + alloc, 2);
            remainingPayment = Math.Round(remainingPayment - alloc, 2);
            allocatedUsd = Math.Round(allocatedUsd + alloc, 2);

            var runningBalance = Math.Round(Math.Max(0m, client.DebtBalanceUsd - allocatedUsd), 2);
            if (!HasLedgerEntry(order.Id, ClientDebtLedgerEntryType.Payment, alloc))
            {
                _db.ClientDebtLedgerEntries.Add(new ClientDebtLedgerEntry
                {
                    RestaurantClientId = client.Id,
                    OrderId = order.Id,
                    EntryType = ClientDebtLedgerEntryType.Payment,
                    AmountUsd = alloc,
                    BalanceAfterUsd = runningBalance,
                    Note = paymentNote,
                    CreatedByEmployeeId = employeeId,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            if (order.ClientDebtSettledUsd >= order.AmountOnAccountUsd - 0.01m)
            {
                order.PaymentConfirmedAt = DateTime.Now;
                order.PaymentCurrencyCode = CurrencyHelper.Usd;
                order.PaymentAmountUsd = order.AmountOnAccountUsd;
                order.PaymentAmount = order.AmountOnAccountUsd;
                order.CustomerPaidUsd = alloc;

                if (!HasLedgerEntry(order.Id, ClientDebtLedgerEntryType.RevenueRecognized))
                {
                    FinancialTransactionService.RecordCompletedOrderRevenue(_db, order.Id);
                    _db.ClientDebtLedgerEntries.Add(new ClientDebtLedgerEntry
                    {
                        RestaurantClientId = client.Id,
                        OrderId = order.Id,
                        EntryType = ClientDebtLedgerEntryType.RevenueRecognized,
                        AmountUsd = order.AmountOnAccountUsd,
                        BalanceAfterUsd = runningBalance,
                        Note = $"Revenue recognized · {order.UniqueId}",
                        CreatedByEmployeeId = employeeId,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }
            }
        }

        if (allocatedUsd <= 0m)
            return (false, "This client has no open debt.", 0m, client.DebtBalanceUsd);

        client.DebtBalanceUsd = Math.Round(Math.Max(0m, client.DebtBalanceUsd - allocatedUsd), 2);
        client.UpdatedAtUtc = DateTime.UtcNow;

        _db.SaveChanges();
        return (true, null, allocatedUsd, client.DebtBalanceUsd);
    }

    private bool HasLedgerEntry(int orderId, string entryType, decimal? amountUsd = null)
    {
        var q = _db.ClientDebtLedgerEntries.Where(e =>
            e.OrderId == orderId && e.EntryType == entryType);
        if (amountUsd is decimal amt)
            q = q.Where(e => e.AmountUsd == amt);
        return q.Any();
    }

    /// <summary>
    /// Collapses duplicate ledger rows (e.g. double settlement) for display.
    /// Keeps the newest row per order/type, or per order/type/amount for payments.
    /// </summary>
    public static IReadOnlyList<ClientDebtLedgerEntry> DedupeLedgerEntriesForDisplay(
        IEnumerable<ClientDebtLedgerEntry> entries)
    {
        return entries
            .GroupBy(e => e.EntryType switch
            {
                ClientDebtLedgerEntryType.Payment when e.OrderId is int oid =>
                    (OrderId: (int?)oid, EntryType: e.EntryType, AmountUsd: e.AmountUsd),
                _ when e.OrderId is int orderId =>
                    (OrderId: (int?)orderId, EntryType: e.EntryType, AmountUsd: (decimal?)null),
                _ => (OrderId: (int?)null, EntryType: e.EntryType, AmountUsd: e.AmountUsd)
            })
            .Select(g => g.OrderByDescending(e => e.Id).First())
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToList();
    }

    private static bool IsInMemoryDatabase(AppDbContext db) =>
        db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;

    public decimal ComputeSettledRevenueUsd(int clientId)
    {
        var orders = _db.Orders.AsNoTracking()
            .Where(o => o.RestaurantClientId == clientId && o.Status == "Completed")
            .Select(o => new { o.ClientSettlement, o.AmountOnAccountUsd, o.ClientDebtSettledUsd, o.PaymentAmountUsd })
            .ToList();
        decimal sum = 0m;
        foreach (var o in orders)
        {
            if (Models.ClientSettlement.IsOnAccount(o.ClientSettlement))
            {
                if (o.ClientDebtSettledUsd >= o.AmountOnAccountUsd - 0.01m)
                    sum += o.AmountOnAccountUsd;
            }
            else if (Models.ClientSettlement.IsPaidAtCompletion(o.ClientSettlement) || o.ClientSettlement == Models.ClientSettlement.None)
            {
                sum += o.PaymentAmountUsd > 0 ? o.PaymentAmountUsd : 0m;
            }
        }

        return Math.Round(sum, 2);
    }

    /// <summary>
    /// Lifetime order revenue that is paid or debt-settled. Excludes open on-account balances
    /// (same eligibility as <see cref="ComputeSettledRevenueUsd"/>).
    /// </summary>
    public decimal ComputeTotalGeneratedRevenueUsd(int clientId) =>
        ComputeSettledRevenueUsd(clientId);

    /// <summary>Staff rows skip phone when it would collide with another regular client's unique phone.</summary>
    private string ResolveStaffClientPhone(string? employeePhone, int employeeId, int? staffClientId)
    {
        var phone = NormalizePhone(employeePhone);
        if (string.IsNullOrEmpty(phone))
            return string.Empty;

        var takenByOther = _db.RestaurantClients.Any(c =>
            c.PrimaryPhone == phone
            && !c.IsStaffClient
            && c.Id != (staffClientId ?? 0));

        if (takenByOther)
            return string.Empty;

        var takenByOtherStaff = _db.RestaurantClients.Any(c =>
            c.PrimaryPhone == phone
            && c.IsStaffClient
            && c.EmployeeId != employeeId
            && c.Id != (staffClientId ?? 0));

        return takenByOtherStaff ? string.Empty : phone;
    }

    private static string NormalizePhone(string? phone)
    {
        var raw = (phone ?? string.Empty).Trim();
        if (raw.Length == 0)
            return string.Empty;
        return new string(raw.Where(char.IsDigit).ToArray());
    }
}
