namespace EliteRestaurant.Api.Security;

/// <summary>Non-production conveniences. Never enable <see cref="DesktopAdminAcceptAnyCredentials"/> on a public API host.</summary>
public sealed class AuthDevOptions
{
    /// <summary>
    /// When true, POST <c>api/auth/login</c> with <c>portal: Admin</c> accepts any non-empty ID and PIN and issues an Admin JWT
    /// (impersonating the first active Admin/Manager in the database when one exists).
    /// </summary>
    public bool DesktopAdminAcceptAnyCredentials { get; set; }
}
