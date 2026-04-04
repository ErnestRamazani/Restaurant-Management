using System.Globalization;
using EliteRestaurantPro.Models;
using EliteRestaurantPro.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurantPro.Data;

public static class FinancialTransactionService
{
    public static void EnsureCompletedOrderRevenues(AppDbContext db)
    {
        var completedOrders = db.Orders
            .AsNoTracking()
            .Where(o => o.Status == "Completed")
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
            .Where(t => t.Type == "Revenue" && t.Category == "Sale")
            .ToList();

        var changed = false;
        foreach (var order in orders)
        {
            var reference = BuildOrderReference(order);
            var tx = sales.FirstOrDefault(t => t.Justification == reference);
            if (tx is null || order.CompletedAt is null)
                continue;
            var target = order.CompletedAt.Value;
            if (tx.Date != target)
            {
                tx.Date = target;
                changed = true;
            }
        }

        if (changed)
            db.SaveChanges();
    }

    public static void RecordCompletedOrderRevenue(AppDbContext db, int orderId)
    {
        var order = db.Orders.AsNoTracking().SingleOrDefault(o => o.Id == orderId);
        if (order is null || order.Status != "Completed")
            return;

        var reference = BuildOrderReference(order);
        var alreadyPosted = db.Transactions.Any(t =>
            t.Type == "Revenue" &&
            t.Category == "Sale" &&
            t.Justification == reference);
        if (alreadyPosted)
            return;

        var items = db.OrderItems
            .AsNoTracking()
            .Where(i => i.OrderRecordId == order.Id)
            .ToList();
        if (items.Count == 0)
            return;

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var pricesByProductId = db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p.Price);

        var usdAmount = items.Sum(item =>
            (pricesByProductId.TryGetValue(item.ProductId, out var price) ? price : 0m) * item.Quantity);

        if (usdAmount <= 0m)
            return;

        var paymentCurrency = string.IsNullOrWhiteSpace(order.PaymentCurrencyCode)
            ? CurrencyHelper.Usd
            : order.PaymentCurrencyCode;
        var exchangeRate = order.ExchangeRateUsed <= 0m
            ? CurrencyHelper.FcPerUsd
            : order.ExchangeRateUsed;
        var paymentAmount = order.PaymentAmount > 0m
            ? order.PaymentAmount
            : paymentCurrency == CurrencyHelper.CongoleseFranc
                ? CurrencyHelper.ConvertUsdToFc(usdAmount)
                : Math.Round(usdAmount, 2);
        var amountUsd = order.PaymentAmountUsd > 0m ? order.PaymentAmountUsd : Math.Round(usdAmount, 2);
        var amountFc = order.PaymentAmountFc > 0m ? order.PaymentAmountFc : CurrencyHelper.ConvertUsdToFc(amountUsd);

        var ledgerDate = order.CompletedAt ?? order.CreatedAt;

        db.Transactions.Add(new MoneyTransaction
        {
            Amount = paymentAmount,
            AmountUsd = amountUsd,
            AmountFc = amountFc,
            Date = ledgerDate,
            Type = "Revenue",
            Category = "Sale",
            CurrencyCode = paymentCurrency,
            ExchangeRateUsed = exchangeRate,
            IsFixed = true,
            Justification = reference
        });
    }

    /// <summary>Legacy no-op: payroll is posted from the Salary module via <see cref="RecordMonthlySalaryPayment"/>.</summary>
    public static void EnsureScheduledSalaryExpenses(AppDbContext db, DateTime startDate, DateTime endDate)
    {
        _ = db;
        _ = startDate;
        _ = endDate;
    }

    public static bool HasMonthlySalaryPayment(AppDbContext db, int employeeId, int year, int month)
    {
        if (db.PayrollPaymentRecords.AsNoTracking().Any(p =>
                p.EmployeeId == employeeId && p.Year == year && p.Month == month))
            return true;

        return HasLegacyMonthlySalaryExpense(db, employeeId, year, month);
    }

    private static bool HasLegacyMonthlySalaryExpense(AppDbContext db, int employeeId, int year, int month)
    {
        var monthLabel = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var prefix = $"Monthly Pay - {monthLabel}";
        var marker = $"| EMP:{employeeId}|";
        return db.Transactions.AsNoTracking().Any(t =>
            t.Type == "Expense" &&
            t.Category == "Salary" &&
            t.Justification.StartsWith(prefix) &&
            t.Justification.Contains(marker));
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

    /// <summary>Recomputes payroll from DB, saves snapshot, applies advances, posts USD expense when net is positive.</summary>
    public static void RecordMonthlySalaryPayment(AppDbContext db, int employeeId, int year, int month)
    {
        if (db.PayrollPaymentRecords.Any(p =>
                p.EmployeeId == employeeId && p.Year == year && p.Month == month))
            return;

        var employee = db.Employees.SingleOrDefault(e => e.Id == employeeId);
        if (employee is null || employee.HourlyRate <= 0m)
            return;

        var (_, scheduledWorkdays, grossPayUsd) =
            PayrollCalculator.GetHourlyGrossForPayrollMonth(employee, year, month);
        if (scheduledWorkdays == 0 || grossPayUsd <= 0m)
            return;

        var start = new DateTime(year, month, 1).Date;
        var endExclusive = start.AddMonths(1);

        var monthRows = db.EmployeeAttendances
            .Where(a => a.EmployeeId == employeeId && a.WorkDate >= start && a.WorkDate < endExclusive)
            .ToList();

        var (absenceDays, lateDays, latePenaltyUnits, totalUnits) =
            PayrollCalculator.CountAttendanceUnitsForPayroll(employee, year, month, monthRows);

        var moneyGenerated = PayrollSupport.SumServerCompletedOrderMerchandiseUsd(db, employeeId, start, endExclusive);
        var advancesDeducted = ApplySalaryAdvancesForPayroll(db, employeeId, year, month);
        var bonus = PayrollCalculator.ComputeBonusUsd(moneyGenerated);
        var netRounded = PayrollCalculator.ComputeFinalNetPayUsd(
            grossPayUsd,
            scheduledWorkdays,
            totalUnits,
            moneyGenerated,
            advancesDeducted);

        db.PayrollPaymentRecords.Add(new PayrollPaymentRecord
        {
            EmployeeId = employeeId,
            Year = year,
            Month = month,
            MonthlySalaryUsd = grossPayUsd,
            AbsenceDays = absenceDays,
            LateDays = lateDays,
            LatePenaltyUnits = latePenaltyUnits,
            TotalDeductionUnits = totalUnits,
            MoneyGeneratedUsd = moneyGenerated,
            BonusFivePercentUsd = bonus,
            AdvancesDeductedUsd = advancesDeducted,
            NetPayUsd = netRounded,
            PaidAtUtc = DateTime.UtcNow
        });

        if (netRounded <= 0m)
            return;

        if (HasLegacyMonthlySalaryExpense(db, employeeId, year, month))
            return;

        var monthLabel = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var payName = string.IsNullOrWhiteSpace(employee.Name) ? $"Employee #{employeeId}" : employee.Name.Trim();
        var justification =
            $"Automatic monthly salary payment to {payName} for {monthLabel} (units:{totalUnits} sales USD:{moneyGenerated:N2} bonus:{bonus:N2} advances deducted:{advancesDeducted:N2}) | EMP:{employeeId}| NET:{netRounded:N2}";

        db.Transactions.Add(new MoneyTransaction
        {
            Amount = netRounded,
            AmountUsd = netRounded,
            AmountFc = CurrencyHelper.ConvertUsdToFc(netRounded),
            Date = DateTime.Now,
            Type = "Expense",
            Category = "Salary",
            CurrencyCode = CurrencyHelper.Usd,
            ExchangeRateUsed = CurrencyHelper.FcPerUsd,
            IsFixed = true,
            Justification = justification
        });
    }

    /// <summary>True if any active employee with hourly pay and scheduled days still lacks payroll for the prior calendar month.</summary>
    public static (bool ShowWarning, string Message, int DaysPastPayDay) GetSalaryOverdueState(AppDbContext db, DateTime now)
    {
        var today = now.Date;
        var firstThisMonth = new DateTime(today.Year, today.Month, 1);
        var lastDayPrevMonth = firstThisMonth.AddDays(-1);
        var dueYear = lastDayPrevMonth.Year;
        var dueMonth = lastDayPrevMonth.Month;

        var employees = db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active" && e.HourlyRate > 0m)
            .ToList();
        if (employees.Count == 0)
            return (false, string.Empty, 0);

        var monthLabel = new DateTime(dueYear, dueMonth, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        foreach (var e in employees)
        {
            var (_, workdays, gross) = PayrollCalculator.GetHourlyGrossForPayrollMonth(e, dueYear, dueMonth);
            if (workdays == 0 || gross <= 0m)
                continue;

            if (HasMonthlySalaryPayment(db, e.Id, dueYear, dueMonth))
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
        if (db.Transactions.AsNoTracking().Any(t => t.Justification.Contains(marker)))
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

    private static string BuildOrderReference(OrderRecord order)
    {
        var orderLabel = string.IsNullOrWhiteSpace(order.UniqueId) ? $"Order #{order.Id:000}" : order.UniqueId;
        return $"Auto revenue from {orderLabel}";
    }
}
