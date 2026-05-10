using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = "AdminRead")]
public sealed class AdminDashboardController(
    AppDbContext db,
    ILogger<AdminDashboardController> logger,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public ActionResult<AdminDashboardDto> GetDashboard()
    {
        try
        {
            return Ok(AdminWebDashboardAggregator.Build(db, User));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Admin dashboard aggregation failed.");
            return Problem(
                detail: environment.IsDevelopment() ? ex.ToString() : null,
                title: "Dashboard aggregation failed",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
