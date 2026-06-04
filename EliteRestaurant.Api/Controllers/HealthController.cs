using EliteRestaurant.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public sealed class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        service = "EliteRestaurant.Api",
        utc = DateTime.UtcNow
    });

    [HttpGet("db")]
    [Authorize(Policy = "AdminRead")]
    public IActionResult GetDb()
    {
        try
        {
            var employeeCount = db.Employees.Count();
            var tableCount = db.Tables.Count();
            var productCount = db.Products.Count();
            var reservationCount = db.Reservations.Count();
            var inventoryCount = db.InventoryItems.Count();
            var orderCount = db.Orders.Count();
            var orderItemCount = db.OrderItems.Count();
            var customerCount = db.CustomerProfiles.Count();
            return Ok(new
            {
                status = "ok",
                db = "connected",
                employees = employeeCount,
                tables = tableCount,
                products = productCount,
                reservations = reservationCount,
                inventoryItems = inventoryCount,
                orders = orderCount,
                orderItems = orderItemCount,
                customers = customerCount,
                utc = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                status = "error",
                db = "failed",
                message = ex.Message,
                utc = DateTime.UtcNow
            });
        }
    }
}
