using EliteRestaurantPro.Data;
using Microsoft.AspNetCore.Mvc;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        service = "EliteRestaurant.Api",
        utc = DateTime.UtcNow
    });

    [HttpGet("db")]
    public IActionResult GetDb()
    {
        try
        {
            using var db = new AppDbContext();
            var employeeCount = db.Employees.Count();
            var tableCount = db.Tables.Count();
            return Ok(new
            {
                status = "ok",
                db = "connected",
                employees = employeeCount,
                tables = tableCount,
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
