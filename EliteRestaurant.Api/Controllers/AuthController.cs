using System.Security.Claims;
using EliteRestaurant.Api.Security;
using EliteRestaurant.Contracts.Auth;
using EliteRestaurant.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public sealed class AuthController(
    TabletAuthService authService,
    JwtTokenService jwtTokenService,
    AppDbContext db,
    IOptions<AuthDevOptions> authDevOptions) : ControllerBase
{
    [HttpGet("session")]
    [Authorize(Policy = "StaffAny")]
    [ProducesResponseType(typeof(AuthSessionDto), 200)]
    public ActionResult<AuthSessionDto> GetSession()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var employeeIdText = User.FindFirstValue("employeeId")
                               ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                               ?? "0";
        _ = int.TryParse(employeeIdText, out var employeeId);
        return Ok(new AuthSessionDto(
            EmployeeId: employeeId,
            Name: User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            Role: role,
            SignInId: User.FindFirstValue("signInId") ?? string.Empty,
            Portal: User.FindFirstValue("portal") ?? string.Empty));
    }

    [HttpPost("login")]
    public ActionResult<CloudLoginResponse> Login([FromBody] CloudLoginRequest request)
    {
        if (AdminDevLoginBypass.TryCreateSession(request, db, authDevOptions) is { } devSession)
        {
            var devJwt = jwtTokenService.CreateToken(devSession, out var devExpiresAtUtc);
            return Ok(new CloudLoginResponse(
                AccessToken: devJwt,
                ExpiresAtUtc: devExpiresAtUtc,
                EmployeeId: devSession.EmployeeId,
                EmployeeUniqueId: devSession.EmployeeUniqueId,
                Name: devSession.Name,
                Role: devSession.Role,
                SignInId: devSession.SignInId,
                Portal: request.Portal));
        }

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
