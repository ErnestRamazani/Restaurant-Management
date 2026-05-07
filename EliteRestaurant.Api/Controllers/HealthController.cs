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
    public IActionResult GetDb()
    {
        try
        {
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
