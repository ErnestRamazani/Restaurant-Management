namespace EliteRestaurantPro.Services;

/// <summary>Legacy hook for status bar; desktop writes go directly to <see cref="DesktopCloudPersistence"/> / HTTP.</summary>
public static class CloudFirstSyncService
{
    public static event Action? StatusChanged;

    public static int PendingCount => 0;

    public static string LastSyncError { get; private set; } = string.Empty;

    public static void NotifyStatusChanged() => StatusChanged?.Invoke();
}
