namespace EliteRestaurantPro.ViewModels;

public sealed class ShiftHistoryRowViewModel
{
    public string WorkDateDisplay { get; init; } = string.Empty;
    public string ShiftType { get; init; } = string.Empty;
    public string ClockIn { get; init; } = string.Empty;
    public string ClockOut { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Justification { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}
