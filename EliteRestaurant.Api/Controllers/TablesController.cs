using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Api.Security;
using EliteRestaurantPro.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TablesController(TabletAuthService authService) : ControllerBase
{
    [HttpGet("my")]
    public ActionResult<IReadOnlyList<TableSummaryDto>> GetMyTables()
    {
        var token = Request.ReadBearerToken();
        var session = authService.Validate(token);
        if (session is null)
            return Unauthorized(new { message = "Missing or expired bearer token." });

        using var db = new AppDbContext();
        var query = db.Tables.AsNoTracking()
            .Include(t => t.AssignedServer)
            .Where(t => t.Status != "Maintenance");

        if (session.Role.Equals("Server", StringComparison.OrdinalIgnoreCase))
            query = query.Where(t => t.AssignedServerId == session.EmployeeId);

        var rows = query
            .OrderBy(t => t.TableNumber)
            .Select(t => new TableSummaryDto(
                t.Id,
                t.UniqueId,
                t.TableNumber,
                t.Name,
                t.Capacity,
                t.Status,
                t.AssignedServerId,
                t.AssignedServer == null ? null : t.AssignedServer.Name))
            .ToList();

        return Ok(rows);
    }
}
