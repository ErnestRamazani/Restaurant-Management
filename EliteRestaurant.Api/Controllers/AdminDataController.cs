using System.Text.Json;
using System.Text.Json.Serialization;
using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/admin/data")]
[AllowAnonymous]
public sealed class AdminDataController(AppDbContext db) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    [HttpGet("{entityName}")]
    public async Task<ActionResult<AdminEntityListResponse>> List(string entityName, CancellationToken cancellationToken)
    {
        var snapshotAt = DateTime.UtcNow;
        var items = entityName.ToLowerInvariant() switch
        {
            "products" or "product" => await Snapshot(db.Products.AsNoTracking().OrderBy(p => p.Category).ThenBy(p => p.Name), cancellationToken),
            "productingredients" or "productingredient" => await Snapshot(db.ProductIngredients.AsNoTracking(), cancellationToken),
            "employees" or "employee" => await Snapshot(db.Employees.AsNoTracking().OrderBy(e => e.Name), cancellationToken),
            "tables" or "table" => await Snapshot(db.Tables.AsNoTracking().Include(t => t.AssignedServer).OrderBy(t => t.TableNumber), cancellationToken),
            "reservations" or "reservationbooking" => await Snapshot(db.Reservations.AsNoTracking().OrderByDescending(r => r.ReservedFor), cancellationToken),
            "inventory" or "inventoryitems" or "inventoryitem" => await Snapshot(db.InventoryItems.AsNoTracking().OrderBy(i => i.Name), cancellationToken),
            "attendance" or "employeeattendances" or "employeeattendance" => await Snapshot(db.EmployeeAttendances.AsNoTracking().OrderByDescending(a => a.WorkDate), cancellationToken),
            "salaryadvances" or "salaryadvance" => await Snapshot(db.SalaryAdvances.AsNoTracking().OrderByDescending(a => a.GivenAt), cancellationToken),
            "payroll" or "payrollpaymentrecords" or "payrollpaymentrecord" => await Snapshot(db.PayrollPaymentRecords.AsNoTracking().OrderByDescending(p => p.PaidAtUtc), cancellationToken),
            "orders" or "orderrecord" => await Snapshot(db.Orders.AsNoTracking().Include(o => o.Items).OrderByDescending(o => o.CreatedAt), cancellationToken),
            "orderitems" or "orderitem" => await Snapshot(db.OrderItems.AsNoTracking(), cancellationToken),
            "money" or "transactions" or "moneytransaction" => await Snapshot(db.Transactions.AsNoTracking().OrderByDescending(t => t.Date), cancellationToken),
            "settings" => SnapshotSettings(),
            _ => null
        };

        if (items is null)
            return NotFound(new { message = $"Unsupported admin entity '{entityName}'." });

        return Ok(new AdminEntityListResponse(entityName, items, snapshotAt));
    }

    private static async Task<IReadOnlyList<JsonElement>> Snapshot<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken)
    {
        var rows = await query.Take(1000).ToListAsync(cancellationToken);
        return rows.Select(ToJsonElement).ToList();
    }

    private static IReadOnlyList<JsonElement> SnapshotSettings()
    {
        var settings = EliteRestaurant.Core.Utils.SettingsManager.Load();
        return [ToJsonElement(settings)];
    }

    private static JsonElement ToJsonElement<T>(T value) =>
        JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions);
}
