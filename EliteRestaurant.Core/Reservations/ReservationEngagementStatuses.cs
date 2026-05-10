namespace EliteRestaurant.Core.Reservations;

public static class ReservationEngagementStatuses
{
    public const string Scheduled = "Scheduled";
    public const string CheckedIn = "CheckedIn";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string NoShow = "NoShow";

    public static bool BlocksOverlapWindow(string status) =>
        string.Equals(status, Scheduled, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, CheckedIn, StringComparison.OrdinalIgnoreCase);
}
