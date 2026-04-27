using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Api.Security;
using EliteRestaurant.Core.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ReservationsController(TabletAuthService authService, AppDbContext db) : ControllerBase
{
    [HttpGet("arrived")]
    public ActionResult<IReadOnlyList<ArrivedReservationDto>> GetArrivedReservations()
    {
        var token = Request.ReadBearerToken();
        var session = authService.Validate(token);
        if (session is null)
            return Unauthorized(new { message = "Missing or expired bearer token." });

        var rows = db.Reservations
            .AsNoTracking()
            .Include(r => r.Table)
            .Where(r => r.Status == "Arrived")
            .OrderBy(r => r.ReservedFor)
            .Take(100)
            .Select(r => new ArrivedReservationDto(
                r.Id,
                r.UniqueId,
                string.IsNullOrWhiteSpace(r.ReservationName) ? r.GuestName : r.ReservationName,
                r.GuestName,
                r.ReservedFor,
                r.TableId,
                r.Table != null && !string.IsNullOrWhiteSpace(r.Table.Name)
                    ? r.Table.Name
                    : (r.TableId.HasValue ? $"Table #{r.TableId.Value}" : "-"),
                r.PartySize))
            .ToList();

        return Ok(rows);
    }
}
