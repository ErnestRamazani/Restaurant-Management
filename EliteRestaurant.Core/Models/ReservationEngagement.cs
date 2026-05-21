namespace EliteRestaurant.Core.Models;

/// <summary>
/// Floor booking instance: ties an optional legacy <see cref="ReservationBooking"/> to placement and timetable.
/// </summary>
public sealed class ReservationEngagement
{
    public int Id { get; set; }

    /// <summary>Guest-facing reference (6 uppercase letters, e.g. KQRTNP).</summary>
    public string? ConfirmationCode { get; set; }

    public int PlacementUnitId { get; set; }
    public PlacementUnit? PlacementUnit { get; set; }

    public int TableId { get; set; }
    public Table? Table { get; set; }

    public int? ReservationBookingId { get; set; }
    public ReservationBooking? ReservationBooking { get; set; }

    public DateTime PlannedStartUtc { get; set; }
    public DateTime PlannedEndUtc { get; set; }
    public DateTime? ActualStartUtc { get; set; }
    public DateTime? ActualEndUtc { get; set; }

    public string GuestName { get; set; } = string.Empty;
    public string GuestPhone { get; set; } = string.Empty;
    public string GuestEmail { get; set; } = string.Empty;
    public int PartySize { get; set; } = 2;
    public string UserNotes { get; set; } = string.Empty;

    /// <summary>Scheduled, CheckedIn, Completed, Cancelled, NoShow</summary>
    public string Status { get; set; } = Reservations.ReservationEngagementStatuses.Scheduled;

    /// <summary>Set when reminder fired (idempotent).</summary>
    public DateTime? ReminderTwoHoursBeforeSentAtUtc { get; set; }

    /// <summary>UI hint: seated past expected turn duration.</summary>
    public bool RotationOrOverstayFlag { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
