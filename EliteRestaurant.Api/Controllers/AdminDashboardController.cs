using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminDashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public ActionResult<AdminDashboardDto> GetDashboard()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var activeOrders = db.Orders.AsNoTracking()
            .Count(o => o.Status != "Completed" && o.Status != "Cancelled");
        var pendingCashier = db.Orders.AsNoTracking()
            .Count(o => o.Status == OrderWorkflow.PendingCashier);
        var readyOrders = db.Orders.AsNoTracking()
            .Count(o => o.Status == "Ready");
        var occupiedTables = db.Tables.AsNoTracking()
            .Count(t => t.Status == "Occupied");
        var availableTables = db.Tables.AsNoTracking()
            .Count(t => t.Status == "Available");
        var todayRevenueUsd = db.Orders.AsNoTracking()
            .Where(o => o.Status == "Completed" && o.CompletedAt >= today && o.CompletedAt < tomorrow)
            .Sum(o => (decimal?)o.PaymentAmountUsd) ?? 0m;

        var recentOrders = db.Orders.AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .Select(o => new AdminActivityDto(
                string.IsNullOrWhiteSpace(o.UniqueId) ? $"Order #{o.Id:000}" : o.UniqueId,
                $"{o.Status} • {o.TableName}",
                "Order",
                o.CreatedAt))
            .ToList();

        return Ok(new AdminDashboardDto(
            new AdminDashboardSummaryDto(
                activeOrders,
                pendingCashier,
                readyOrders,
                occupiedTables,
                availableTables,
                todayRevenueUsd,
                DateTime.UtcNow),
            recentOrders));
    }
}
