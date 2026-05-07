using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EliteRestaurant.Api.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public string CreateToken(AuthenticatedStaffSession session, out DateTime expiresAtUtc)
    {
        expiresAtUtc = DateTime.UtcNow.AddHours(Math.Max(1, _options.ExpirationHours));
        var credentials = new SigningCredentials(GetSigningKey(), SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, session.EmployeeId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, session.EmployeeId.ToString()),
            new(ClaimTypes.Name, session.Name),
            new(ClaimTypes.Role, session.Role),
            new("employeeId", session.EmployeeId.ToString()),
            new("employeeUniqueId", session.EmployeeUniqueId),
            new("portal", session.Portal),
            new("signInId", session.SignInId)
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddSeconds(-30),
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public AuthenticatedStaffSession? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token.Trim(), BuildValidationParameters(), out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwt
                || !string.Equals(jwt.Header.Alg, SecurityAlgorithms.HmacSha256, StringComparison.Ordinal))
                return null;

            var employeeIdText = principal.FindFirstValue("employeeId")
                                 ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(employeeIdText, out var employeeId))
                return null;

            var expiresAtUtc = jwt.ValidTo.Kind == DateTimeKind.Utc
                ? jwt.ValidTo
                : DateTime.SpecifyKind(jwt.ValidTo, DateTimeKind.Utc);

            return new AuthenticatedStaffSession(
                token.Trim(),
                employeeId,
                principal.FindFirstValue("employeeUniqueId") ?? string.Empty,
                principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
                principal.FindFirstValue("signInId") ?? string.Empty,
                principal.FindFirstValue("portal") ?? string.Empty,
                expiresAtUtc);
        }
        catch
        {
            return null;
        }
    }

    public TokenValidationParameters BuildValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = _options.Issuer,
        ValidateAudience = true,
        ValidAudience = _options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = GetSigningKey(),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    private SymmetricSecurityKey GetSigningKey()
    {
        var key = _options.SigningKey?.Trim() ?? string.Empty;
        if (key.Length < 32)
            throw new InvalidOperationException("JWT signing key must be at least 32 characters. Set Jwt:SigningKey or JWT__SigningKey.");

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }
}
