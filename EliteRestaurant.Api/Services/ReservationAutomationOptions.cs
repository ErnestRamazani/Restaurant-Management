namespace EliteRestaurant.Api.Services;

public sealed class ReservationAutomationOptions
{
    public int NoShowGraceMinutes { get; set; } = 15;
    public int DefaultTurnMinutes { get; set; } = 105;
    public double ReminderHoursBefore { get; set; } = 2;
    public int ScannerIntervalSeconds { get; set; } = 60;
}
