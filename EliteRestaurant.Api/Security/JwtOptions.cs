namespace EliteRestaurant.Api.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "EliteRestaurant";
    public string Audience { get; set; } = "EliteRestaurant";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpirationHours { get; set; } = 12;
}
