using EliteRestaurant.Api.Security;
using EliteRestaurant.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public sealed class AuthController(TabletAuthService authService, JwtTokenService jwtTokenService) : ControllerBase
{
    [HttpPost("login")]
    public ActionResult<CloudLoginResponse> Login([FromBody] CloudLoginRequest request)
    {
        var session = authService.Login(request.StaffId, request.Pin, request.Portal);
        if (session is null)
            return Unauthorized(new { message = "Invalid staff ID / PIN for selected portal." });

        var jwt = jwtTokenService.CreateToken(session, out var expiresAtUtc);
        var responsePortal = string.Equals(session.Portal, "Admin", StringComparison.OrdinalIgnoreCase)
            ? request.Portal
            : session.Portal;
        return Ok(new CloudLoginResponse(
            AccessToken: jwt,
            ExpiresAtUtc: expiresAtUtc,
            EmployeeId: session.EmployeeId,
            EmployeeUniqueId: session.EmployeeUniqueId,
            Name: session.Name,
            Role: session.Role,
            SignInId: session.SignInId,
            Portal: responsePortal));
    }
}
