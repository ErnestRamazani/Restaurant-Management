namespace EliteRestaurant.Core.Reservations;

public sealed class ReservationSchedulingOptions
{
    /// <summary>Minutes padded before start / after end when testing overlaps.</summary>
    public int BufferMinutes { get; set; } = 15;

    /// <summary>Default seated duration for new engagements when end is omitted.</summary>
    public int DefaultDurationMinutes { get; set; } = 105;

    /// <summary>Spacing between suggested slot starts.</summary>
    public int SuggestionSlotStepMinutes { get; set; } = 30;

    /// <summary>How far ahead public booking searches for slots.</summary>
    public int SuggestionHorizonDays { get; set; } = 14;
}
