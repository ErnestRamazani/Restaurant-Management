using System.ComponentModel.DataAnnotations.Schema;
using EliteRestaurant.Core.Tenancy;

namespace EliteRestaurant.Core.Models;

public class ReservationBooking : IRestaurantScoped
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public string ReservationName { get; set; } = string.Empty;

    public int? CustomerProfileId { get; set; }
    public CustomerProfile? CustomerProfile { get; set; }

    public string GuestName { get; set; } = string.Empty;
    public string GuestPhone { get; set; } = string.Empty;
    public int PartySize { get; set; } = 2;
    public DateTime ReservedFor { get; set; } = DateTime.Now;
    public string Channel { get; set; } = "Phone";
    public string Status { get; set; } = "Pending";
    public string UserNotes { get; set; } = string.Empty;

    public int? TableId { get; set; }
    public Table? Table { get; set; }

    public bool DepositPaid { get; set; }
    public decimal DepositAmountUsd { get; set; }
    public string DepositCurrencyCode { get; set; } = "USD";
    public bool DepositForfeited { get; set; }

    public int? CreatedByEmployeeId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public int DaysUntilReservation => (ReservedFor.Date - DateTime.Today).Days;

    public string ReservationUrgency
    {
        get
        {
            if (string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Status, "NoShow", StringComparison.OrdinalIgnoreCase))
                return "Closed";

            if (DaysUntilReservation < 0)
                return "Past";
            if (DaysUntilReservation == 0)
                return "Today";
            if (DaysUntilReservation <= 2)
                return "Soon";
            return "Future";
        }
    }

    public int ReservationHealthPercent
    {
        get
        {
            if (!string.Equals(Status, "Confirmed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Status, "Arrived", StringComparison.OrdinalIgnoreCase))
                return 0;

            if (string.Equals(Status, "Arrived", StringComparison.OrdinalIgnoreCase))
                return 100;

            if (DaysUntilReservation >= 7)
                return 100;
            if (DaysUntilReservation >= 3)
                return 70;
            if (DaysUntilReservation >= 1)
                return 40;
            if (DaysUntilReservation == 0)
                return 20;
            return 5;
        }
    }

    public string ReservationHealthState
    {
        get
        {
            if (ReservationHealthPercent == 0)
                return "Idle";
            if (DaysUntilReservation <= 0)
                return "Critical";
            if (DaysUntilReservation <= 2)
                return "Warning";
            return "Healthy";
        }
    }

    public string TableDisplayName
    {
        get
        {
            if (Table is not null && !string.IsNullOrWhiteSpace(Table.Name))
                return Table.Name;
            return TableId is int id ? $"Table #{id}" : "-";
        }
    }

    public string ReservationDisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ReservationName))
                return ReservationName.Trim();
            if (!string.IsNullOrWhiteSpace(GuestName))
                return GuestName.Trim();
            return UniqueId;
        }
    }

    [NotMapped]
    public bool IsExpanded { get; set; }

    public bool CanShowEditAction =>
        string.Equals(Status, "Pending", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Status, "Confirmed", StringComparison.OrdinalIgnoreCase);

    public bool CanShowConfirmAction =>
        string.Equals(Status, "Pending", StringComparison.OrdinalIgnoreCase);

    public bool CanShowArrivedAction =>
        string.Equals(Status, "Confirmed", StringComparison.OrdinalIgnoreCase)
        && ReservedFor.Date <= DateTime.Today;

    public bool CanShowNoShowAction =>
        string.Equals(Status, "Confirmed", StringComparison.OrdinalIgnoreCase)
        && ReservedFor.Date <= DateTime.Today;

    public bool CanShowCancelAction =>
        string.Equals(Status, "Pending", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Status, "Confirmed", StringComparison.OrdinalIgnoreCase);
}
