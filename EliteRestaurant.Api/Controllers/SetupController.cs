using EliteRestaurant.Api.Options;
using EliteRestaurant.Api.Security;
using EliteRestaurant.Contracts.Setup;
using EliteRestaurant.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/setup")]
[AllowAnonymous]
public sealed class SetupController(
    SiteSetupService setupService,
    JwtTokenService jwtTokenService,
    IOptions<SetupOptions> setupOptions) : ControllerBase
{
    [HttpGet("status")]
    [EnableRateLimiting("Setup")]
    [ProducesResponseType(typeof(SetupStatusDto), 200)]
    public async Task<ActionResult<SetupStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var status = await setupService.GetStatusAsync(cancellationToken);
        return Ok(new SetupStatusDto(status.SetupRequired, status.RestaurantCount, status.Message));
    }

    [HttpPost("first-site")]
    [EnableRateLimiting("Setup")]
    [ProducesResponseType(typeof(SiteSetupResponse), 200)]
    [ProducesResponseType(typeof(SiteSetupErrorDto), 400)]
    [ProducesResponseType(typeof(SiteSetupErrorDto), 409)]
    public async Task<IActionResult> PostFirstSite(
        [FromBody] Contracts.Setup.SiteSetupRequest? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new SiteSetupErrorDto(["Request body is required."]));

        var status = await setupService.GetStatusAsync(cancellationToken);
        if (!status.SetupRequired)
            return Conflict(new SiteSetupErrorDto(["First-site setup is not available — a restaurant already exists."]));

        try
        {
            jwtTokenService.EnsureSigningKeyConfigured();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new SiteSetupErrorDto([
                ex.Message,
                "On DigitalOcean: App → Settings → add JWT__SigningKey (at least 32 random characters), then redeploy."
            ]));
        }

        return await CompleteSetupAsync(
            () => setupService.CreateFirstSiteAsync(ToCore(body), cancellationToken));
    }

    [HttpPost("wipe-all-data")]
    [EnableRateLimiting("Setup")]
    [ProducesResponseType(typeof(SetupStatusDto), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<SetupStatusDto>> PostWipeAllData(CancellationToken cancellationToken)
    {
        if (!IsAuthorizedForNewSite())
            return Unauthorized(new { message = "Missing or invalid X-Setup-Secret header." });

        var status = await setupService.WipeAllTenantDataAsync(cancellationToken);
        return Ok(new SetupStatusDto(status.SetupRequired, status.RestaurantCount, status.Message));
    }

    [HttpPost("new-site")]
    [EnableRateLimiting("Setup")]
    [ProducesResponseType(typeof(SiteSetupResponse), 200)]
    [ProducesResponseType(typeof(SiteSetupErrorDto), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(SiteSetupErrorDto), 409)]
    public async Task<IActionResult> PostNewSite(
        [FromBody] Contracts.Setup.SiteSetupRequest? body,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorizedForNewSite())
            return Unauthorized(new { message = "Missing or invalid X-Setup-Secret header." });

        if (body is null)
            return BadRequest(new SiteSetupErrorDto(["Request body is required."]));

        var status = await setupService.GetStatusAsync(cancellationToken);
        if (status.SetupRequired)
            return Conflict(new SiteSetupErrorDto(["Use first-site setup for an empty database."]));

        return await CompleteSetupAsync(
            () => setupService.CreateNewSiteAsync(ToCore(body), cancellationToken));
    }

    private bool IsAuthorizedForNewSite()
    {
        var secret = setupOptions.Value.PlatformSecret?.Trim();
        if (string.IsNullOrEmpty(secret))
            return false;

        if (!Request.Headers.TryGetValue("X-Setup-Secret", out var provided))
            return false;

        return string.Equals(provided.ToString().Trim(), secret, StringComparison.Ordinal);
    }

    private async Task<IActionResult> CompleteSetupAsync(Func<Task<SiteSetupResult>> create)
    {
        SiteSetupResult result;
        try
        {
            result = await create();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new SiteSetupErrorDto([$"Setup failed: {ex.GetBaseException().Message}"]));
        }

        if (!result.Success || result.Site is null)
            return BadRequest(new SiteSetupErrorDto(result.Errors));

        var site = result.Site;
        var session = new AuthenticatedStaffSession(
            Token: string.Empty,
            EmployeeId: site.EmployeeId,
            RestaurantId: site.RestaurantId,
            EmployeeUniqueId: site.EmployeeUniqueId,
            Name: site.Name,
            Role: site.Role,
            SignInId: site.SignInId,
            Portal: "Admin",
            ExpiresAtUtc: DateTime.UtcNow);

        try
        {
            var jwt = jwtTokenService.CreateToken(session, out var expiresAtUtc, preferredLanguage: null);
            return Ok(new SiteSetupResponse(
            site.RestaurantId,
            site.RestaurantUniqueId,
            site.Slug,
            site.CustomDomain,
            jwt,
            expiresAtUtc,
            site.EmployeeId,
            site.Name,
            site.SignInId,
            site.Role));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new SiteSetupErrorDto([
                ex.Message,
                "The site may have been created. Try signing in with your admin ID and PIN, or set JWT__SigningKey on the API and run setup again."
            ]));
        }
    }

    private static SiteSetupCommand ToCore(Contracts.Setup.SiteSetupRequest dto) =>
        new(
            dto.RestaurantName,
            dto.Slug,
            dto.CustomDomain,
            dto.AdminSignInId,
            dto.AdminPin,
            dto.AdminName,
            dto.PreferredLanguage);
}
