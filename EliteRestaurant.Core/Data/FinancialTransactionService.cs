using System.Globalization;
using EliteRestaurant.Core.Models;
using ClientSettlement = EliteRestaurant.Core.Models.ClientSettlement;
using EliteRestaurant.Core.Reporting;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

public static class FinancialTransactionService
{
    private static SalaryPayrollRules SalaryRulesFromDefaultMenuRow(AppDbContext db) =>
        SalaryPayrollRules.FromPublicMenuRow(
            db.PublicMenuSettings.AsNoTracking().FirstOrDefault(s => s.Key == "default"));

    public static void EnsureCompletedOrderRevenues(AppDbContext db)
    {
        var completedOrders = db.Orders
            .AsNoTracking()
            .Where(o => o.Status == "Completed" && o.PaymentConfirmedAt != null)
            .OrderBy(o => o.CreatedAt)
            .ToList();

        foreach (var order in completedOrders)
            RecordCompletedOrderRevenue(db, order.Id);

        AlignPostedRevenueDatesToOrderCompletedAt(db);
    }

    /// <summary>Sets sale revenue row dates to each order's CompletedAt when they differ from what is stored.</summary>
    public static void AlignPostedRevenueDatesToOrderCompletedAt(AppDbContext db)
    {
        var orders = db.Orders.AsNoTracking()
            .Where(o => o.Status == "Completed" && o.CompletedAt != null)
            .ToList();
        if (orders.Count == 0)
            return;

        var sales = db.Transactions
            .Where(t => t.Type == "Revenue" && (t.Category == "Sale" || t.Category == "Delivery Fee"))
            .ToList();

        var changed = false;
        foreach (var order in orders)
        {
            var reference = BuildOrderReference(order);
            var target = order.CompletedAt!.Value;
            foreach (var tx in sales)
            {
                var match = tx.RelatedOrderId == order.Id
                    || (tx.Category == "Sale" && tx.Justification == reference)
                    || (tx.Justification ?? string.Empty).Contains(DeliveryMarker(order.Id), StringComparison.Ordinal);
                if (!match)
                    continue;
                if (tx.Date != target)
                {
                    tx.Date = target;
                    changed = true;
                }
            }
        }

        if (changed)
            db.SaveChanges();
    }

    public static void RecordCompletedOrderRevenue(AppDbContext db, int orderId)
    {
        var order = db.Orders.AsNoTracking().SingleOrDefault(o => o.Id == orderId);
        if (order is null || order.Status != "Completed" || order.PaymentConfirmedAt is null)
            return;

        if (ClientSettlement.IsOnAccount(order.ClientSettlement)
            && order.ClientDebtSettledUsd < order.AmountOnAccountUsd - 0.01m)
            return;

        var deliveryFee = Math.Round(Math.Max(0m, order.DeliveryFeeUsd), 2);
        var merchGrandUsd = ResolveMerchandiseGrandUsd(db, order);
        if (merchGrandUsd <= 0m && deliveryFee <= 0m)
            return;

        var reference = BuildOrderReference(order);
        var ledgerDate = order.PaymentConfirmedAt ?? order.CompletedAt ?? order.CreatedAt;

        var paymentCurrency = string.IsNullOrWhiteSpace(order.PaymentCurrencyCode)
            ? CurrencyHelper.Usd
            : order.PaymentCurrencyCode;
        var exchangeRate = order.ExchangeRateUsed <= 0m
            ? CurrencyHelper.FcPerUsd
            : order.ExchangeRateUsed;
        var originType = string.IsNullOrWhiteSpace(order.OrderOrigin) ? OrderOrigin.InStore : order.OrderOrigin.Trim();

        var totalPartsUsd = merchGrandUsd + deliveryFee;

        if (merchGrandUsd > 0m && !HasPostedMerchandiseSale(db, order.Id, reference))
        {
            var (amt, usd, fc) = MerchandiseRevenueAmounts(order, merchGrandUsd, paymentCurrency, exchangeRate);
            db.Transactions.Add(new MoneyTransaction
            {
                RelatedOrderId = order.Id,
                OrderOriginType = originType,
                Amount = amt,
                AmountUsd = usd,
                AmountFc = fc,
                Date = ledgerDate,
                Type = "Revenue",
                Category = "Sale",
                CurrencyCode = paymentCurrency,
                ExchangeRateUsed = exchangeRate,
                IsFixed = true,
                Justification = reference
            });
        }

        if (deliveryFee > 0m && !HasPostedDeliveryFeeSale(db, order.Id))
        {
            var (amt, usd, fc) = ResolvePostedAmounts(order, deliveryFee, totalPartsUsd, paymentCurrency, exchangeRate);
            db.Transactions.Add(new MoneyTransaction
            {
                RelatedOrderId = order.Id,
                OrderOriginType = originType,
                Amount = amt,
                AmountUsd = usd,
                AmountFc = fc,
                Date = ledgerDate,
                Type = "Revenue",
                Category = "Delivery Fee",
                CurrencyCode = paymentCurrency,
                ExchangeRateUsed = exchangeRate,
                IsFixed = true,
                Justification = $"Delivery fee (20%) — {reference} {DeliveryMarker(order.Id)}"
            });
        }
    }

    private static string DeliveryMarker(int orderId) => $"|ORDER:{orderId}:DELIVERY|";

    private static decimal ResolveMerchandiseGrandUsd(AppDbContext db, OrderRecord order)
    {
        if (order.MerchandiseGrandTotalUsd > 0m)
            return Math.Round(order.MerchandiseGrandTotalUsd, 2);

        if (order.PaymentAmountUsd > 0m)
            return Math.Round(order.PaymentAmountUsd, 2);

        var items = db.OrderItems.AsNoTracking().Where(i => i.OrderRecordId == order.Id).ToList();
        if (items.Count == 0)
            return 0m;

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var prices = db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p.Price);
        var lineSub = items.Sum(i => (prices.TryGetValue(i.ProductId, out var price) ? price : 0m) * i.Quantity);
        return OrderTotalsHelper.ComputeTotalsWithDeliveryFee(
            lineSub,
            order.DiscountMode,
            order.DiscountValue,
            order.DeliveryFeeUsd).GrandTotal;
    }

    private static (decimal Amount, decimal AmountUsd, decimal AmountFc) MerchandiseRevenueAmounts(
        OrderRecord order,
        decimal merchGrandUsd,
        string paymentCurrency,
        decimal exchangeRate)
    {
        merchGrandUsd = Math.Round(merchGrandUsd, 2);
        if (merchGrandUsd <= 0m)
            return (0m, 0m, 0m);

        if (string.Equals(paymentCurrency, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase)
            && order.PaymentAmountFc > 0m
            && order.PaymentAmountUsd <= 0m)
        {
            var fc = CurrencyHelper.ConvertUsdToFc(merchGrandUsd);
            return (fc, 0m, fc);
        }

        if (MoneyReportingHelpers.IsMixedCurrency(paymentCurrency)
            && (order.PaymentAmountUsd > 0m || order.PaymentAmountFc > 0m))
        {
            var usd = merchGrandUsd;
            var fc = CurrencyHelper.ConvertUsdToFc(merchGrandUsd);
            return (usd, usd, fc);
        }

        var amount = string.Equals(paymentCurrency, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase)
            ? CurrencyHelper.ConvertUsdToFc(merchGrandUsd)
            : merchGrandUsd;
        return (amount, merchGrandUsd, CurrencyHelper.ConvertUsdToFc(merchGrandUsd));
    }

    private static bool HasPostedMerchandiseSale(AppDbContext db, int orderId, string legacyJustification)
    {
        if (db.Transactions.Local.Any(t =>
                t.RelatedOrderId == orderId && t.Type == "Revenue" && t.Category == "Sale"))
            return true;
        if (db.Transactions.AsNoTracking().Any(t =>
                t.RelatedOrderId == orderId && t.Type == "Revenue" && t.Category == "Sale"))
            return true;
        return db.Transactions.AsNoTracking().Any(t =>
            t.Type == "Revenue" &&
            t.Category == "Sale" &&
            t.Justification == legacyJustification);
    }

    private static bool HasPostedDeliveryFeeSale(AppDbContext db, int orderId)
    {
        if (db.Transactions.Local.Any(t =>
                t.RelatedOrderId == orderId && t.Type == "Revenue" && t.Category == "Delivery Fee"))
            return true;

        return db.Transactions.AsNoTracking().Any(t =>
            t.RelatedOrderId == orderId && t.Type == "Revenue" && t.Category == "Delivery Fee");
    }

    /// <summary>Split stored payment across merchandise vs delivery fee by USD grand sub-parts.</summary>
    private static (decimal Amount, decimal AmountUsd, decimal AmountFc) ResolvePostedAmounts(
        OrderRecord order,
        decimal componentUsd,
        decimal totalPartsUsd,
        string paymentCurrency,
        decimal exchangeRate)
    {
        _ = exchangeRate;
        componentUsd = Math.Round(componentUsd, 2);
        if (componentUsd <= 0m)
            return (0m, 0m, 0m);

        if (totalPartsUsd <= 0m)
            totalPartsUsd = componentUsd;
        var share = Math.Min(1m, Math.Max(0m, componentUsd / totalPartsUsd));

        if (order.PaymentAmountUsd > 0m || order.PaymentAmountFc > 0m)
        {
            var usd = Math.Round(order.PaymentAmountUsd * share, 2);
            var fc = Math.Round(order.PaymentAmountFc * share, 2);
            var amt = MoneyReportingHelpers.IsMixedCurrency(paymentCurrency)
                ? usd
                : paymentCurrency == CurrencyHelper.CongoleseFranc
                    ? fc
                    : usd;
            return (amt, usd, fc);
        }

        // No per-currency tender on the order: book USD revenue only (do not infer FC from the menu total).
        var fallbackUsd = componentUsd;
        var amountLegacy = Math.Round(fallbackUsd, 2);
        return (amountLegacy, fallbackUsd, 0m);
    }

    /// <summary>Legacy no-op: payroll is posted from the Salary module via <see cref="TryRecordMonthlySalaryPayment"/>.</summary>
    public static void EnsureScheduledSalaryExpenses(AppDbContext db, DateTime startDate, DateTime endDate)
    {
        _ = db;
        _ = startDate;
        _ = endDate;
    }

    /// <summary>True if any payroll snapshot or legacy salary expense exists for the month (blocks new advances).</summary>
    public static bool HasMonthlySalaryPayment(AppDbContext db, int employeeId, int year, int month)
    {
        if (db.PayrollPaymentRecords.AsNoTracking().Any(p =>
                p.EmployeeId == employeeId && p.Year == year && p.Month == month))
            return true;

        return LegacyMonthlySalaryExpenseExistsInDb(db, employeeId, year, month);
    }

    public static bool HasMonthlySalaryPayment(
        IReadOnlyList<PayrollPaymentRecord> payrollRecords,
        IReadOnlyList<MoneyTransaction> transactions,
        int employeeId,
        int year,
        int month)
    {
        if (payrollRecords.Any(p => p.EmployeeId == employeeId && p.Year == year && p.Month == month))
            return true;

        return HasLegacyMonthlySalaryExpense(transactions, employeeId, year, month);
    }

    /// <summary>True when cumulative posted pay meets or exceeds net for the month (or legacy closed expense).</summary>
    public static bool IsPayrollFullyPaid(AppDbContext db, int employeeId, int year, int month)
    {
        var p = db.PayrollPaymentRecords.AsNoTracking()
            .FirstOrDefault(x => x.EmployeeId == employeeId && x.Year == year && x.Month == month);
        if (p is not null)
        {
            if (p.NetPayUsd <= 0m)
                return true;
            return p.PaidToDateUsd >= p.NetPayUsd - 0.005m;
        }

        return LegacyMonthlySalaryExpenseExistsInDb(db, employeeId, year, month);
    }

    private static bool LegacyMonthlySalaryExpenseExistsInDb(AppDbContext db, int employeeId, int year, int month)
    {
        var monthLabel = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var prefix = $"Monthly Pay - {monthLabel}";
        var marker = $"| EMP:{employeeId}|";
        return db.Transactions.AsNoTracking().Any(t =>
            t.Type == "Expense" &&
            t.Category == "Salary" &&
            (t.Justification ?? string.Empty).StartsWith(prefix) &&
            (t.Justification ?? string.Empty).Contains(marker));
    }

    /// <summary>HTTP/sync clients: payroll completeness without <see cref="AppDbContext"/>.</summary>
    public static bool IsPayrollFullyPaid(
        IReadOnlyList<PayrollPaymentRecord> payrollRecords,
        IReadOnlyList<MoneyTransaction> transactions,
        int employeeId,
        int year,
        int month)
    {
        var p = payrollRecords.FirstOrDefault(x => x.EmployeeId == employeeId && x.Year == year && x.Month == month);
        if (p is not null)
        {
            if (p.NetPayUsd <= 0m)
                return true;
            return p.PaidToDateUsd >= p.NetPayUsd - 0.005m;
        }

        return HasLegacyMonthlySalaryExpense(transactions, employeeId, year, month);
    }

    private static bool HasLegacyMonthlySalaryExpense(IReadOnlyList<MoneyTransaction> transactions, int employeeId, int year, int month)
    {
        var monthLabel = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var prefix = $"Monthly Pay - {monthLabel}";
        var marker = $"| EMP:{employeeId}|";
        return transactions.Any(t =>
            t.Type == "Expense" &&
            t.Category == "Salary" &&
            (t.Justification ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal) &&
            (t.Justification ?? string.Empty).Contains(marker, StringComparison.Ordinal));
    }

    /// <summary>Marks advances that match this payroll month as applied. Returns total applied.</summary>
    public static decimal ApplySalaryAdvancesForPayroll(AppDbContext db, int employeeId, int year, int month)
    {
        var advances = db.SalaryAdvances
            .Where(a => a.EmployeeId == employeeId && a.AppliedPayrollYear == null)
            .ToList();

        var sum = 0m;
        foreach (var a in advances)
        {
            if (!PayrollSupport.AdvanceAppliesToPayrollMonth(a, year, month))
                continue;
            sum += a.AmountUsd;
            a.AppliedPayrollYear = year;
            a.AppliedPayrollMonth = month;
        }

        return Math.Round(sum, 2);
    }

    /// <summary>Records a payroll payment (full or partial). Returns null on success.</summary>
    public static string? TryRecordMonthlySalaryPayment(
        AppDbContext db,
        int employeeId,
        int year,
        int month,
        decimal paymentUsd)
    {
        paymentUsd = Math.Round(paymentUsd, 2);
        if (paymentUsd <= 0m)
            return "Enter a payment amount greater than zero.";

        var existing = db.PayrollPaymentRecords
            .FirstOrDefault(p => p.EmployeeId == employeeId && p.Year == year && p.Month == month);

        if (existing is not null)
        {
            var remaining = Math.Round(existing.NetPayUsd - existing.PaidToDateUsd, 2);
            if (remaining <= 0m)
                return "Payroll for this month is already paid in full.";
            if (paymentUsd > remaining + 0.01m)
                return $"This payment ({paymentUsd:N2} USD) exceeds the remaining balance ({remaining:N2} USD).";

            existing.PaidToDateUsd = Math.Round(existing.PaidToDateUsd + paymentUsd, 2);
            existing.PaidAtUtc = DateTime.UtcNow;

            if (existing.NetPayUsd > 0m)
            {
                var emp = db.Employees.AsNoTracking().SingleOrDefault(e => e.Id == employeeId);
                var payName = string.IsNullOrWhiteSpace(emp?.Name) ? $"Employee #{employeeId}" : emp!.Name.Trim();
                AddSalaryPaymentExpense(
                    db,
                    employeeId,
                    payName,
                    year,
                    month,
                    paymentUsd,
                    existing.NetPayUsd,
                    existing.PaidToDateUsd,
                    existing.TotalDeductionUnits,
                    existing.MoneyGeneratedUsd,
                    existing.BonusFivePercentUsd,
                    existing.AdvancesDeductedUsd);
            }

            return null;
        }

        var employee = db.Employees.SingleOrDefault(e => e.Id == employeeId);
        if (employee is null)
            return "Employee not found.";

        if (employee.MonthlySalaryUSD <= 0m)
            return "Employee not found or monthly salary is not set.";

        var monthBase = PayrollCalculator.ResolvePayrollMonthBase(employee, year, month);
        if (monthBase.GrossPayUsd <= 0m)
            return "No payroll gross for this month — check join date, monthly salary, and weekly shifts in Employees.";

        var start = new DateTime(year, month, 1).Date;
        var endExclusive = start.AddMonths(1);
        var monthStartUtc = AttendanceCalendar.DayAnchorUtc(start);
        var monthEndExclusiveUtc = AttendanceCalendar.DayAnchorUtc(endExclusive);

        var monthRows = db.EmployeeAttendances
            .Where(a => a.EmployeeId == employeeId && a.WorkDate >= monthStartUtc && a.WorkDate < monthEndExclusiveUtc)
            .ToList();

        var rules = SalaryRulesFromDefaultMenuRow(db);
        var (absenceDays, lateDays, latePenaltyUnits, totalUnits) =
            PayrollCalculator.CountAttendanceUnitsForPayroll(employee, year, month, monthRows, rules);

        var moneyGenerated = PayrollSupport.SumServerCompletedOrderMerchandiseUsd(db, employeeId, start, endExclusive);
        var advancesDeducted = ApplySalaryAdvancesForPayroll(db, employeeId, year, month);
        var bonus = PayrollCalculator.ComputeBonusUsd(moneyGenerated, rules);
        var netRounded = PayrollCalculator.ComputeFinalNetPayUsd(
            monthBase.GrossPayUsd,
            monthBase.AttendanceDenominatorWorkdays,
            totalUnits,
            moneyGenerated,
            advancesDeducted,
            rules);

        if (paymentUsd > netRounded + 0.01m)
            return $"Payment ({paymentUsd:N2} USD) is greater than net pay ({netRounded:N2} USD).";

        if (LegacyMonthlySalaryExpenseExistsInDb(db, employeeId, year, month))
            return "Payroll already has a legacy Money expense for this period.";

        db.PayrollPaymentRecords.Add(new PayrollPaymentRecord
        {
            EmployeeId = employeeId,
            Year = year,
            Month = month,
            MonthlySalaryUsd = monthBase.GrossPayUsd,
            AbsenceDays = absenceDays,
            LateDays = lateDays,
            LatePenaltyUnits = latePenaltyUnits,
            TotalDeductionUnits = totalUnits,
            MoneyGeneratedUsd = moneyGenerated,
            BonusFivePercentUsd = bonus,
            AdvancesDeductedUsd = advancesDeducted,
            NetPayUsd = netRounded,
            PaidToDateUsd = paymentUsd,
            PaidAtUtc = DateTime.UtcNow
        });

        if (netRounded > 0m && paymentUsd > 0m)
        {
            var payName = string.IsNullOrWhiteSpace(employee.Name) ? $"Employee #{employeeId}" : employee.Name.Trim();
            AddSalaryPaymentExpense(
                db,
                employeeId,
                payName,
                year,
                month,
                paymentUsd,
                netRounded,
                paymentUsd,
                totalUnits,
                moneyGenerated,
                bonus,
                advancesDeducted);
        }

        return null;
    }

    /// <summary>Applies pending advances in-memory (mutates <paramref name="advances"/>). Returns total applied USD.</summary>
    public static decimal ApplySalaryAdvancesForPayrollMemory(List<SalaryAdvance> advances, int employeeId, int year, int month)
    {
        var sum = 0m;
        foreach (var a in advances)
        {
            if (a.EmployeeId != employeeId || a.AppliedPayrollYear != null)
                continue;
            if (!PayrollSupport.AdvanceAppliesToPayrollMonth(a, year, month))
                continue;
            sum += a.AmountUsd;
            a.AppliedPayrollYear = year;
            a.AppliedPayrollMonth = month;
        }

        return Math.Round(sum, 2);
    }

    /// <summary>
    /// Payroll payment without <see cref="AppDbContext"/>; returns entities to upsert via HTTP sync (payroll rows, advances, expense).
    /// </summary>
    public static string? TryRecordMonthlySalaryPaymentMemory(
        Employee employee,
        IReadOnlyList<EmployeeAttendance> monthAttendancesForEmployee,
        decimal moneyGeneratedUsd,
        List<SalaryAdvance> advances,
        PayrollPaymentRecord? existing,
        decimal paymentUsd,
        int year,
        int month,
        IReadOnlyList<MoneyTransaction> transactions,
        out List<object> upserts)
    {
        upserts = [];
        paymentUsd = Math.Round(paymentUsd, 2);
        if (paymentUsd <= 0m)
            return "Enter a payment amount greater than zero.";

        if (existing is not null)
        {
            var remaining = Math.Round(existing.NetPayUsd - existing.PaidToDateUsd, 2);
            if (remaining <= 0m)
                return "Payroll for this month is already paid in full.";
            if (paymentUsd > remaining + 0.01m)
                return $"This payment ({paymentUsd:N2} USD) exceeds the remaining balance ({remaining:N2} USD).";

            var paidTo = Math.Round(existing.PaidToDateUsd + paymentUsd, 2);
            upserts.Add(new PayrollPaymentRecord
            {
                Id = existing.Id,
                EmployeeId = existing.EmployeeId,
                Year = existing.Year,
                Month = existing.Month,
                MonthlySalaryUsd = existing.MonthlySalaryUsd,
                AbsenceDays = existing.AbsenceDays,
                LateDays = existing.LateDays,
                LatePenaltyUnits = existing.LatePenaltyUnits,
                TotalDeductionUnits = existing.TotalDeductionUnits,
                MoneyGeneratedUsd = existing.MoneyGeneratedUsd,
                BonusFivePercentUsd = existing.BonusFivePercentUsd,
                AdvancesDeductedUsd = existing.AdvancesDeductedUsd,
                NetPayUsd = existing.NetPayUsd,
                PaidToDateUsd = paidTo,
                PaidAtUtc = DateTime.UtcNow
            });

            if (existing.NetPayUsd > 0m)
            {
                var payName = string.IsNullOrWhiteSpace(employee.Name) ? $"Employee #{employee.Id}" : employee.Name.Trim();
                upserts.Add(BuildSalaryPaymentExpense(
                    employee.Id,
                    payName,
                    year,
                    month,
                    paymentUsd,
                    existing.NetPayUsd,
                    paidTo,
                    existing.TotalDeductionUnits,
                    existing.MoneyGeneratedUsd,
                    existing.BonusFivePercentUsd,
                    existing.AdvancesDeductedUsd));
            }

            return null;
        }

        var monthBase = PayrollCalculator.ResolvePayrollMonthBase(employee, year, month);
        if (employee.MonthlySalaryUSD <= 0m && employee.HourlyRate <= 0m)
            return "Set monthly salary or hourly rate (USD) in Employees — required for payroll.";
        if (monthBase.GrossPayUsd <= 0m)
            return "No payroll gross for this month — check join date, monthly amount, or scheduled shifts in Employees.";

        var rules = PayrollCalculator.ResolveSalaryPayrollRulesForLocalFile();
        var (absenceDays, lateDays, latePenaltyUnits, totalUnits) =
            PayrollCalculator.CountAttendanceUnitsForPayroll(employee, year, month, monthAttendancesForEmployee, rules);

        var advancesDeducted = ApplySalaryAdvancesForPayrollMemory(advances, employee.Id, year, month);
        foreach (var a in advances.Where(x =>
                     x.EmployeeId == employee.Id &&
                     x.AppliedPayrollYear == year &&
                     x.AppliedPayrollMonth == month))
            upserts.Add(a);

        var bonus = PayrollCalculator.ComputeBonusUsd(moneyGeneratedUsd, rules);
        var netRounded = PayrollCalculator.ComputeFinalNetPayUsd(
            monthBase.GrossPayUsd,
            monthBase.AttendanceDenominatorWorkdays,
            totalUnits,
            moneyGeneratedUsd,
            advancesDeducted,
            rules);

        if (paymentUsd > netRounded + 0.01m)
            return $"Payment ({paymentUsd:N2} USD) is greater than net pay ({netRounded:N2} USD).";

        if (HasLegacyMonthlySalaryExpense(transactions, employee.Id, year, month))
            return "Payroll already has a legacy Money expense for this period.";

        upserts.Add(new PayrollPaymentRecord
        {
            EmployeeId = employee.Id,
            Year = year,
            Month = month,
            MonthlySalaryUsd = monthBase.GrossPayUsd,
            AbsenceDays = absenceDays,
            LateDays = lateDays,
            LatePenaltyUnits = latePenaltyUnits,
            TotalDeductionUnits = totalUnits,
            MoneyGeneratedUsd = moneyGeneratedUsd,
            BonusFivePercentUsd = bonus,
            AdvancesDeductedUsd = advancesDeducted,
            NetPayUsd = netRounded,
            PaidToDateUsd = paymentUsd,
            PaidAtUtc = DateTime.UtcNow
        });

        if (netRounded > 0m && paymentUsd > 0m)
        {
            var payName = string.IsNullOrWhiteSpace(employee.Name) ? $"Employee #{employee.Id}" : employee.Name.Trim();
            upserts.Add(BuildSalaryPaymentExpense(
                employee.Id,
                payName,
                year,
                month,
                paymentUsd,
                netRounded,
                paymentUsd,
                totalUnits,
                moneyGeneratedUsd,
                bonus,
                advancesDeducted));
        }

        return null;
    }

    private static MoneyTransaction BuildSalaryPaymentExpense(
        int employeeId,
        string payName,
        int year,
        int month,
        decimal paymentUsd,
        decimal netPayUsd,
        decimal cumulativePaidUsd,
        int totalUnits,
        decimal moneyGenerated,
        decimal bonus,
        decimal advancesDeducted)
    {
        var monthLabel = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var cumulative = Math.Round(cumulativePaidUsd, 2);
        var remaining = Math.Round(netPayUsd - cumulative, 2);
        var partLabel = remaining <= 0.01m ? "Final" : "Partial";
        var justification =
            $"{partLabel} monthly salary payment to {payName} for {monthLabel} " +
            $"(units:{totalUnits} sales USD:{moneyGenerated:N2} bonus:{bonus:N2} advances deducted:{advancesDeducted:N2}) " +
            $"| EMP:{employeeId}| NET:{netPayUsd:N2} THIS:{paymentUsd:N2} CUMULATIVE:{cumulative:N2}|";

        return new MoneyTransaction
        {
            Amount = paymentUsd,
            AmountUsd = paymentUsd,
            AmountFc = CurrencyHelper.ConvertUsdToFc(paymentUsd),
            Date = DateTime.Now,
            Type = "Expense",
            Category = "Salary",
            CurrencyCode = CurrencyHelper.Usd,
            ExchangeRateUsed = CurrencyHelper.FcPerUsd,
            IsFixed = true,
            Justification = justification
        };
    }

    /// <summary>Salary advance cash expense if not already present (for HTTP sync upsert).</summary>
    public static MoneyTransaction? BuildSalaryAdvanceExpenseIfMissing(
        int salaryAdvanceId,
        int employeeId,
        string employeeName,
        decimal amountUsd,
        IReadOnlyList<MoneyTransaction> existingTransactions)
    {
        var rounded = Math.Round(amountUsd, 2);
        if (rounded <= 0m)
            return null;

        var marker = $"| ADVANCE:{salaryAdvanceId}|";
        if (existingTransactions.Any(t => (t.Justification ?? string.Empty).Contains(marker, StringComparison.Ordinal)))
            return null;

        var name = string.IsNullOrWhiteSpace(employeeName) ? $"Employee #{employeeId}" : employeeName.Trim();
        var justification = $"Salary advance to {name} | EMP:{employeeId}| ADVANCE:{salaryAdvanceId}| USD:{rounded:N2}";

        return new MoneyTransaction
        {
            Amount = rounded,
            AmountUsd = rounded,
            AmountFc = CurrencyHelper.ConvertUsdToFc(rounded),
            Date = DateTime.Now,
            Type = "Expense",
            Category = "Salary",
            CurrencyCode = CurrencyHelper.Usd,
            ExchangeRateUsed = CurrencyHelper.FcPerUsd,
            IsFixed = true,
            Justification = justification
        };
    }

    private static void AddSalaryPaymentExpense(
        AppDbContext db,
        int employeeId,
        string payName,
        int year,
        int month,
        decimal paymentUsd,
        decimal netPayUsd,
        decimal cumulativePaidUsd,
        int totalUnits,
        decimal moneyGenerated,
        decimal bonus,
        decimal advancesDeducted)
    {
        var monthLabel = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var cumulative = Math.Round(cumulativePaidUsd, 2);
        var remaining = Math.Round(netPayUsd - cumulative, 2);
        var partLabel = remaining <= 0.01m ? "Final" : "Partial";
        var justification =
            $"{partLabel} monthly salary payment to {payName} for {monthLabel} " +
            $"(units:{totalUnits} sales USD:{moneyGenerated:N2} bonus:{bonus:N2} advances deducted:{advancesDeducted:N2}) " +
            $"| EMP:{employeeId}| NET:{netPayUsd:N2} THIS:{paymentUsd:N2} CUMULATIVE:{cumulative:N2}|";

        db.Transactions.Add(new MoneyTransaction
        {
            Amount = paymentUsd,
            AmountUsd = paymentUsd,
            AmountFc = CurrencyHelper.ConvertUsdToFc(paymentUsd),
            Date = DateTime.Now,
            Type = "Expense",
            Category = "Salary",
            CurrencyCode = CurrencyHelper.Usd,
            ExchangeRateUsed = CurrencyHelper.FcPerUsd,
            IsFixed = true,
            Justification = justification
        });
    }

    /// <summary>True if any active employee with payroll gross for the prior calendar month still lacks a full Salary posting.</summary>
    public static (bool ShowWarning, string Message, int DaysPastPayDay) GetSalaryOverdueState(AppDbContext db, DateTime now)
    {
        var employees = db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active" && e.MonthlySalaryUSD > 0m)
            .ToList();
        var payroll = db.PayrollPaymentRecords.AsNoTracking().ToList();
        var transactions = db.Transactions.AsNoTracking().ToList();
        return GetSalaryOverdueState(now, employees, payroll, transactions);
    }

    /// <summary>HTTP/sync clients (no EF).</summary>
    public static (bool ShowWarning, string Message, int DaysPastPayDay) GetSalaryOverdueState(
        DateTime now,
        IReadOnlyList<Employee> employees,
        IReadOnlyList<PayrollPaymentRecord> payrollRecords,
        IReadOnlyList<MoneyTransaction> transactions)
    {
        var today = now.Date;
        var firstThisMonth = new DateTime(today.Year, today.Month, 1);
        var lastDayPrevMonth = firstThisMonth.AddDays(-1);
        var dueYear = lastDayPrevMonth.Year;
        var dueMonth = lastDayPrevMonth.Month;

        var payrollCandidates = employees
            .Where(e => string.Equals(e.EmploymentStatus, "Active", StringComparison.OrdinalIgnoreCase) &&
                        e.MonthlySalaryUSD > 0m)
            .ToList();
        if (payrollCandidates.Count == 0)
            return (false, string.Empty, 0);

        var monthLabel = new DateTime(dueYear, dueMonth, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        foreach (var e in payrollCandidates)
        {
            var monthBase = PayrollCalculator.ResolvePayrollMonthBase(e, dueYear, dueMonth);
            if (monthBase.GrossPayUsd <= 0m)
                continue;

            if (IsPayrollFullyPaid(payrollRecords, transactions, e.Id, dueYear, dueMonth))
                continue;

            var daysPast = Math.Max(0, (today - lastDayPrevMonth).Days);
            var message =
                $"Payroll for {monthLabel} is not fully posted. Use Salary to record payments ({daysPast} day(s) past month end).";
            return (true, message, daysPast);
        }

        return (false, string.Empty, 0);
    }

    /// <summary>Posts a Salary expense when cash is given as an advance (deducted later on payroll confirm).</summary>
    public static void RecordSalaryAdvanceExpense(
        AppDbContext db,
        int salaryAdvanceId,
        int employeeId,
        string employeeName,
        decimal amountUsd)
    {
        var rounded = Math.Round(amountUsd, 2);
        if (rounded <= 0m)
            return;

        var marker = $"| ADVANCE:{salaryAdvanceId}|";
        if (db.Transactions.AsNoTracking()
            .Where(t => t.Type == "Expense" && t.Category == "Salary")
            .Select(t => t.Justification)
            .AsEnumerable()
            .Any(j => (j ?? string.Empty).Contains(marker, StringComparison.Ordinal)))
            return;

        var name = string.IsNullOrWhiteSpace(employeeName) ? $"Employee #{employeeId}" : employeeName.Trim();
        var justification = $"Salary advance to {name} | EMP:{employeeId}| ADVANCE:{salaryAdvanceId}| USD:{rounded:N2}";

        db.Transactions.Add(new MoneyTransaction
        {
            Amount = rounded,
            AmountUsd = rounded,
            AmountFc = CurrencyHelper.ConvertUsdToFc(rounded),
            Date = DateTime.Now,
            Type = "Expense",
            Category = "Salary",
            CurrencyCode = CurrencyHelper.Usd,
            ExchangeRateUsed = CurrencyHelper.FcPerUsd,
            IsFixed = true,
            Justification = justification
        });
    }

    /// <summary>Creates a Sale revenue row for a completed order if missing (HTTP/sync clients).</summary>
    public static MoneyTransaction? BuildAutoSaleRevenueIfMissing(
        OrderRecord order,
        IReadOnlyList<OrderItem> items,
        IReadOnlyDictionary<int, decimal> productPrices,
        IReadOnlyList<MoneyTransaction> existingTransactions)
    {
        if (!string.Equals(order.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            return null;
        if (order.PaymentConfirmedAt is null)
            return null;

        var reference = BuildOrderReference(order);
        if (existingTransactions.Any(t =>
                string.Equals(t.Type, "Revenue", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(t.Category, "Sale", StringComparison.OrdinalIgnoreCase) &&
                (t.RelatedOrderId == order.Id || string.Equals(t.Justification, reference, StringComparison.Ordinal))))
            return null;

        if (items.Count == 0)
            return null;

        var merchSubtotal = items.Sum(item =>
            (productPrices.TryGetValue(item.ProductId, out var price) ? price : 0m) * item.Quantity);
        var totals = OrderTotalsHelper.ComputeTotals(merchSubtotal, order.DiscountMode, order.DiscountValue);
        var merchGrandUsd = totals.GrandTotal;
        if (merchGrandUsd <= 0m)
            return null;

        var paymentCurrency = string.IsNullOrWhiteSpace(order.PaymentCurrencyCode)
            ? CurrencyHelper.Usd
            : order.PaymentCurrencyCode;
        var exchangeRate = order.ExchangeRateUsed <= 0m
            ? CurrencyHelper.FcPerUsd
            : order.ExchangeRateUsed;
        var deliveryFee = Math.Round(Math.Max(0m, order.DeliveryFeeUsd), 2);
        var totalParts = merchGrandUsd + deliveryFee;
        var (amount, amountUsd, amountFc) = ResolvePostedAmounts(order, merchGrandUsd, totalParts, paymentCurrency, exchangeRate);

        var ledgerDate = order.PaymentConfirmedAt ?? order.CompletedAt ?? order.CreatedAt;
        var originType = string.IsNullOrWhiteSpace(order.OrderOrigin) ? OrderOrigin.InStore : order.OrderOrigin.Trim();

        return new MoneyTransaction
        {
            RelatedOrderId = order.Id,
            OrderOriginType = originType,
            Amount = amount,
            AmountUsd = amountUsd,
            AmountFc = amountFc,
            Date = ledgerDate,
            Type = "Revenue",
            Category = "Sale",
            CurrencyCode = paymentCurrency,
            ExchangeRateUsed = exchangeRate,
            IsFixed = true,
            Justification = reference
        };
    }

    /// <summary>Creates Sale Change expense rows when absent (HTTP/sync clients).</summary>
    public static IReadOnlyList<MoneyTransaction> BuildSaleChangeExpensesIfMissing(
        OrderRecord order,
        IReadOnlyList<MoneyTransaction> existingTransactions)
    {
        var list = new List<MoneyTransaction>();
        var usd = Math.Round(Math.Max(0m, order.ChangeGivenUsd), 2);
        var fc = Math.Round(Math.Max(0m, order.ChangeGivenFc), 2);
        if (usd <= 0m && fc <= 0m)
            return list;

        var orderCode = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId;
        var usdMarker = $"| CHANGE_ORDER:{order.Id}:USD|";
        var fcMarker = $"| CHANGE_ORDER:{order.Id}:FC|";
        var ledgerDate = order.CompletedAt ?? DateTime.Now;

        if (usd > 0m && !existingTransactions.Any(t =>
                (t.Justification ?? string.Empty).Contains(usdMarker, StringComparison.Ordinal)))
        {
            list.Add(new MoneyTransaction
            {
                Amount = usd,
                AmountUsd = usd,
                AmountFc = CurrencyHelper.ConvertUsdToFc(usd),
                Date = ledgerDate,
                Type = "Expense",
                Category = "Sale Change",
                CurrencyCode = CurrencyHelper.Usd,
                ExchangeRateUsed = CurrencyHelper.FcPerUsd,
                IsFixed = false,
                Justification = $"Cash change returned for order {orderCode} (USD). {usdMarker}"
            });
        }

        if (fc > 0m && !existingTransactions.Any(t =>
                (t.Justification ?? string.Empty).Contains(fcMarker, StringComparison.Ordinal)))
        {
            list.Add(new MoneyTransaction
            {
                Amount = fc,
                AmountUsd = CurrencyHelper.ConvertFcToUsd(fc),
                AmountFc = fc,
                Date = ledgerDate,
                Type = "Expense",
                Category = "Sale Change",
                CurrencyCode = CurrencyHelper.CongoleseFranc,
                ExchangeRateUsed = CurrencyHelper.FcPerUsd,
                IsFixed = false,
                Justification = $"Cash change returned for order {orderCode} (FC). {fcMarker}"
            });
        }

        return list;
    }

    private static string BuildOrderReference(OrderRecord order)
    {
        var orderLabel = string.IsNullOrWhiteSpace(order.UniqueId) ? $"Order #{order.Id:000}" : order.UniqueId;
        if (string.Equals(order.OrderSource, "Reservation", StringComparison.OrdinalIgnoreCase))
        {
            var reservationLabel = string.IsNullOrWhiteSpace(order.ReservationCode)
                ? "Reservation"
                : order.ReservationCode;
            return $"Auto revenue from {orderLabel} (Reservation: {reservationLabel})";
        }

        return $"Auto revenue from {orderLabel}";
    }
}
