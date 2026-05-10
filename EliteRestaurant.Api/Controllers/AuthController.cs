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
        var outcome = authService.Login(request.StaffId, request.Pin, request.Portal);
        if (outcome.Session is null)
            return Unauthorized(new { message = outcome.ErrorMessage ?? "Sign-in failed." });

        var jwt = jwtTokenService.CreateToken(outcome.Session, out var expiresAtUtc);
        var responsePortal =
            string.Equals(outcome.Session.Portal, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(outcome.Session.Portal, "AdminWeb", StringComparison.OrdinalIgnoreCase)
                ? request.Portal
                : outcome.Session.Portal;
        return Ok(new CloudLoginResponse(
            AccessToken: jwt,
            ExpiresAtUtc: expiresAtUtc,
            EmployeeId: outcome.Session.EmployeeId,
            EmployeeUniqueId: outcome.Session.EmployeeUniqueId,
            Name: outcome.Session.Name,
            Role: outcome.Session.Role,
            SignInId: outcome.Session.SignInId,
            Portal: responsePortal));
    }
}
