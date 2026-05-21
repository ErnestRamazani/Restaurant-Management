using EliteRestaurant.Api.Services;
using EliteRestaurant.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/language")]
public sealed class LanguageController(
    LocalizationService localization,
    AppDbContext db) : ControllerBase
{
    [HttpGet("strings")]
    [AllowAnonymous]
    public ActionResult<LanguageStringsResponse> GetStrings([FromQuery] string? lang)
    {
        var resolved = ResolveLanguage(lang);
        var strings = localization.GetAllStrings(resolved);
        return Ok(new LanguageStringsResponse(resolved, strings));
    }

    [HttpGet("supported")]
    [AllowAnonymous]
    public ActionResult<SupportedLanguagesResponse> GetSupported()
    {
        return Ok(new SupportedLanguagesResponse(
            localization.DefaultLanguage,
            localization.SupportedLanguages));
    }

    [HttpPost("preference")]
    [Authorize(Policy = "StaffAny")]
    public async Task<ActionResult> SetPreferredLanguage(
        [FromQuery] string? lang,
        CancellationToken cancellationToken)
    {
        var resolved = ResolveLanguage(lang);
        if (!localization.SupportedLanguages.Contains(resolved, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "Unsupported language." });

        if (!int.TryParse(User.FindFirstValue("employeeId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out var employeeId))
            return Unauthorized();

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);
        if (employee is null)
            return NotFound();

        employee.PreferredLanguage = resolved;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { language = resolved });
    }

    private string ResolveLanguage(string? lang)
    {
        if (!string.IsNullOrWhiteSpace(lang))
            return LocalizationService.NormalizeLanguage(lang);

        var header = Request.Headers.AcceptLanguage.ToString();
        if (!string.IsNullOrWhiteSpace(header))
        {
            var first = header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => p.Split(';')[0].Trim())
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
                return LocalizationService.NormalizeLanguage(first);
        }

        return localization.DefaultLanguage;
    }

    public sealed record LanguageStringsResponse(string Language, IReadOnlyDictionary<string, object?> Strings);

    public sealed record SupportedLanguagesResponse(string DefaultLanguage, IReadOnlyList<string> SupportedLanguages);
}
