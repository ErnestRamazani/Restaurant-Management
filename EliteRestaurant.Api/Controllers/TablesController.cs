using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Api.Security;
using EliteRestaurant.Api.Tables;
using EliteRestaurant.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "StaffAny")]
public sealed class TablesController(TabletAuthService authService, AppDbContext db) : ControllerBase
{
    [HttpGet("my")]
    public ActionResult<IReadOnlyList<TableSummaryDto>> GetMyTables()
    {
        var token = Request.ReadBearerToken();
        var session = authService.Validate(token);
        if (session is null)
            return Unauthorized(new { message = "Missing or expired bearer token." });

        var rows = StaffTableListQuery.ListForSession(db, session);
        return Ok(rows);
    }
}
