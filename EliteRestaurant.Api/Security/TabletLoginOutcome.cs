namespace EliteRestaurant.Api.Security;

/// <summary>Result of a PIN / portal sign-in attempt (opaque session or a safe client message).</summary>
public sealed record TabletLoginOutcome(AuthenticatedStaffSession? Session, string? ErrorMessage);
