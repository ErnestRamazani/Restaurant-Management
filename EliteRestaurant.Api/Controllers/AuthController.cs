using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(TabletAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        var session = authService.Login(request.StaffId, request.Pin, request.Portal);
        if (session is null)
            return Unauthorized(new { message = "Invalid staff ID / PIN for selected portal." });

        return Ok(new LoginResponse(
            AccessToken: session.Token,
            ExpiresAtUtc: session.ExpiresAtUtc,
            EmployeeId: session.EmployeeId,
            EmployeeUniqueId: session.EmployeeUniqueId,
            Name: session.Name,
            Role: session.Role,
            SignInId: session.SignInId));
    }
}
