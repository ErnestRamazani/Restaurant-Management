using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Services;

namespace EliteRestaurantPro.ViewModels;

public sealed class ReservationsViewModel : AdminBaseViewModel
{
    private bool _initialized;
    private readonly List<ReservationBooking> _allReservations = [];
    private readonly AdminDataApiClient _data = new();

    private string _searchText = string.Empty;
    private string _selectedStatusFilter = "All";
    private string _selectedChannelFilter = "All";

    private bool _isReservationDialogOpen;
    private ReservationBooking? _selectedReservation;
    private int? _editingReservationId;
    private string _reservationDialogTitle = "New reservation";
    private string _reservationName = string.Empty;
    private string _reservationGuestName = string.Empty;
    private string _reservationGuestPhone = string.Empty;
    private string _reservationPartySizeText = "2";
    private DateTime _reservationDate = DateTime.Today;
    private string _reservationTimeText = "19:00";
    private string _reservationChannel = "Phone";
    private string _reservationStatus = "Pending";
    private int? _reservationTableId;
    private bool _reservationDepositPaid;
    private string _reservationDepositAmountText = "0";
    private string _reservationNotes = string.Empty;

    public override string ActivePage => "Reservations";

    public bool CanManageReservations => ShowFullAdminNav || AppSession.IsCashierTablet;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value))
                return;
            ApplyReservationFilters();
        }
    }

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (!SetField(ref _selectedStatusFilter, value))
                return;
            ApplyReservationFilters();
        }
    }

    public string SelectedChannelFilter
    {
        get => _selectedChannelFilter;
        set
        {
            if (!SetField(ref _selectedChannelFilter, value))
                return;
            ApplyReservationFilters();
        }
    }

    public ObservableCollection<ReservationBooking> Reservations { get; } = [];
    public ObservableCollection<Table> Tables { get; } = [];

    public ObservableCollection<string> StatusFilters { get; } =
        new(["All", "Pending", "Confirmed", "Arrived", "Cancelled", "NoShow"]);

    public ObservableCollection<string> ChannelFilters { get; } =
        new(["All", "Phone", "WhatsApp", "WalkIn", "ManualAdmin"]);

    public ObservableCollection<string> ReservationChannels { get; } =
        new(["Phone", "WhatsApp", "WalkIn", "ManualAdmin"]);

    public bool IsReservationDialogOpen
    {
        get => _isReservationDialogOpen;
        set => SetField(ref _isReservationDialogOpen, value);
    }

    public ReservationBooking? SelectedReservation
    {
        get => _selectedReservation;
        set
        {
            if (!SetField(ref _selectedReservation, value))
                return;

            OnPropertyChanged(nameof(CanEditSelectedReservation));
            OnPropertyChanged(nameof(CanConfirmSelectedReservation));
            OnPropertyChanged(nameof(CanArriveSelectedReservation));
            OnPropertyChanged(nameof(CanCancelSelectedReservation));
            OnPropertyChanged(nameof(CanSetPendingSelectedReservation));
            OnPropertyChanged(nameof(CanCompleteSelectedReservation));
            OnPropertyChanged(nameof(CanNoShowSelectedReservation));
        }
    }

    public bool CanEditSelectedReservation => CanManageReservations && SelectedReservation is not null;
    public bool CanConfirmSelectedReservation =>
        CanManageReservations && SelectedReservation is not null &&
        string.Equals(SelectedReservation.Status, "Pending", StringComparison.OrdinalIgnoreCase);
    public bool CanArriveSelectedReservation =>
        CanManageReservations && SelectedReservation is not null &&
        string.Equals(SelectedReservation.Status, "Confirmed", StringComparison.OrdinalIgnoreCase) &&
        SelectedReservation.ReservedFor.Date <= DateTime.Today;
    public bool CanCancelSelectedReservation =>
        CanManageReservations && SelectedReservation is not null &&
        (string.Equals(SelectedReservation.Status, "Pending", StringComparison.OrdinalIgnoreCase)
         || string.Equals(SelectedReservation.Status, "Confirmed", StringComparison.OrdinalIgnoreCase)
         || string.Equals(SelectedReservation.Status, "Arrived", StringComparison.OrdinalIgnoreCase));
    public bool CanSetPendingSelectedReservation =>
        CanManageReservations && SelectedReservation is not null &&
        string.Equals(SelectedReservation.Status, "Confirmed", StringComparison.OrdinalIgnoreCase);
    public bool CanCompleteSelectedReservation =>
        CanManageReservations && SelectedReservation is not null &&
        string.Equals(SelectedReservation.Status, "Arrived", StringComparison.OrdinalIgnoreCase);
    public bool CanNoShowSelectedReservation =>
        CanManageReservations && SelectedReservation is not null &&
        string.Equals(SelectedReservation.Status, "Confirmed", StringComparison.OrdinalIgnoreCase) &&
        SelectedReservation.ReservedFor.Date <= DateTime.Today;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    private string _loadStatus = string.Empty;
    public string LoadStatus
    {
        get => _loadStatus;
        private set => SetField(ref _loadStatus, value);
    }

    public string ReservationDialogTitle
    {
        get => _reservationDialogTitle;
        set => SetField(ref _reservationDialogTitle, value);
    }

    public string ReservationName
    {
        get => _reservationName;
        set => SetField(ref _reservationName, value);
    }

    public string ReservationGuestName
    {
        get => _reservationGuestName;
        set => SetField(ref _reservationGuestName, value);
    }

    public string ReservationGuestPhone
    {
        get => _reservationGuestPhone;
        set => SetField(ref _reservationGuestPhone, value);
    }

    public string ReservationPartySizeText
    {
        get => _reservationPartySizeText;
        set => SetField(ref _reservationPartySizeText, value);
    }

    public DateTime ReservationDate
    {
        get => _reservationDate;
        set => SetField(ref _reservationDate, value);
    }

    public string ReservationTimeText
    {
        get => _reservationTimeText;
        set => SetField(ref _reservationTimeText, value);
    }

    public string ReservationChannel
    {
        get => _reservationChannel;
        set => SetField(ref _reservationChannel, value);
    }

    public string ReservationStatus
    {
        get => _reservationStatus;
        set => SetField(ref _reservationStatus, value);
    }

    public int? ReservationTableId
    {
        get => _reservationTableId;
        set => SetField(ref _reservationTableId, value);
    }

    public bool ReservationDepositPaid
    {
        get => _reservationDepositPaid;
        set => SetField(ref _reservationDepositPaid, value);
    }

    public string ReservationDepositAmountText
    {
        get => _reservationDepositAmountText;
        set => SetField(ref _reservationDepositAmountText, value);
    }

    public string ReservationNotes
    {
        get => _reservationNotes;
        set => SetField(ref _reservationNotes, value);
    }

    public ICommand OpenReservationDialogCommand { get; }
    public ICommand EditReservationCommand { get; }
    public ICommand SaveReservationCommand { get; }
    public ICommand CancelReservationDialogCommand { get; }
    public ICommand ConfirmReservationCommand { get; }
    public ICommand MarkArrivedCommand { get; }
    public ICommand MarkCompletedCommand { get; }
    public ICommand MarkCancelledCommand { get; }
    public ICommand MarkNoShowCommand { get; }
    public ICommand MarkPendingCommand { get; }

    public ReservationsViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        OpenReservationDialogCommand = new RelayCommand(_ => OpenReservationDialog(), _ => CanManageReservations);
        EditReservationCommand = new RelayCommand(x => OpenEditReservation(x as ReservationBooking), _ => CanManageReservations);
        SaveReservationCommand = new RelayCommand(_ => _ = SaveReservationAsync(), _ => CanManageReservations);
        CancelReservationDialogCommand = new RelayCommand(_ => IsReservationDialogOpen = false);
        ConfirmReservationCommand = new RelayCommand(x => _ = UpdateReservationStatusAsync(x as ReservationBooking, "Confirmed"), _ => CanManageReservations);
        MarkArrivedCommand = new RelayCommand(x => _ = UpdateReservationStatusAsync(x as ReservationBooking, "Arrived"), _ => CanManageReservations);
        MarkCompletedCommand = new RelayCommand(x => _ = UpdateReservationStatusAsync(x as ReservationBooking, "Completed"), _ => CanManageReservations);
        MarkCancelledCommand = new RelayCommand(x => _ = UpdateReservationStatusAsync(x as ReservationBooking, "Cancelled"), _ => CanManageReservations);
        MarkNoShowCommand = new RelayCommand(x => _ = UpdateReservationStatusAsync(x as ReservationBooking, "NoShow"), _ => CanManageReservations);
        MarkPendingCommand = new RelayCommand(x => _ = UpdateReservationStatusAsync(x as ReservationBooking, "Pending"), _ => CanManageReservations);

        Log("ReservationsViewModel constructed.");
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;
        Log("InitializeAsync started.");
        await LoadAllAsync();
        Log("InitializeAsync finished.");
    }

    private async Task LoadAllAsync()
    {
        IsLoading = true;
        LoadStatus = string.Empty;
        Log("LoadAllAsync started.");
        try
        {
            var expandedIds = _allReservations
                .Where(r => r.IsExpanded)
                .Select(r => r.Id)
                .ToHashSet();

            Log("Cloud API query started.");
            var tablesTask = _data.GetTablesAsync();
            var reservationsTask = _data.GetReservationsAsync();
            await Task.WhenAll(tablesTask, reservationsTask).ConfigureAwait(false);

            var tableList = (await tablesTask.ConfigureAwait(false)).OrderBy(t => t.TableNumber).ToList();
            var now = DateTime.Now;
            var minDate = now.AddDays(-30);
            var maxDate = now.AddDays(30);
            var reservationList = (await reservationsTask.ConfigureAwait(false))
                .Where(r => r.ReservedFor >= minDate && r.ReservedFor <= maxDate)
                .OrderByDescending(r => r.ReservedFor)
                .Take(120)
                .ToList();

            var tableById = tableList.ToDictionary(t => t.Id);
            foreach (var r in reservationList)
            {
                if (r.TableId is int tid && tableById.TryGetValue(tid, out var t))
                    r.Table = t;
            }

            Log($"Cloud API query finished. tables={tableList.Count}, reservations={reservationList.Count}");

            Tables.Clear();
            foreach (var table in tableList)
                Tables.Add(table);

            _allReservations.Clear();
            foreach (var reservation in reservationList)
                reservation.IsExpanded = expandedIds.Contains(reservation.Id);
            _allReservations.AddRange(reservationList);

            ApplyReservationFilters();
            if (SelectedReservation is null && Reservations.Count > 0)
                SelectedReservation = Reservations[0];
            Log("LoadAllAsync UI bind completed.");
        }
        catch (Exception ex)
        {
            LoadStatus = $"Could not load reservations: {ex.Message}";
            Log("LoadAllAsync error: " + ex);
        }
        finally
        {
            IsLoading = false;
            Log("LoadAllAsync finished.");
        }
    }

    private void ApplyReservationFilters()
    {
        IEnumerable<ReservationBooking> query = _allReservations;
        var q = (SearchText ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(r =>
                Contains(r.ReservationName, q) ||
                Contains(r.GuestName, q) ||
                Contains(r.GuestPhone, q) ||
                Contains(r.UniqueId, q) ||
                Contains(r.Table?.Name, q));
        }

        if (!string.Equals(SelectedStatusFilter, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(r => string.Equals(r.Status, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.Equals(SelectedChannelFilter, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(r => string.Equals(r.Channel, SelectedChannelFilter, StringComparison.OrdinalIgnoreCase));

        Reservations.Clear();
        foreach (var row in query.OrderBy(r => r.ReservedFor))
            Reservations.Add(row);

        if (SelectedReservation is not null)
            SelectedReservation = Reservations.FirstOrDefault(r => r.Id == SelectedReservation.Id) ?? Reservations.FirstOrDefault();
    }

    private void OpenReservationDialog()
    {
        if (!CanManageReservations)
            return;

        _editingReservationId = null;
        ReservationDialogTitle = "New reservation";
        ReservationName = string.Empty;
        ReservationGuestName = string.Empty;
        ReservationGuestPhone = string.Empty;
        ReservationPartySizeText = "2";
        ReservationDate = DateTime.Today;
        ReservationTimeText = "19:00";
        ReservationChannel = "Phone";
        ReservationStatus = "Pending";
        ReservationTableId = null;
        ReservationDepositPaid = false;
        ReservationDepositAmountText = "0";
        ReservationNotes = string.Empty;
        IsReservationDialogOpen = true;
    }

    private void OpenEditReservation(ReservationBooking? reservation)
    {
        if (!CanManageReservations || reservation is null)
            return;

        _editingReservationId = reservation.Id;
        ReservationDialogTitle = "Edit reservation";
        ReservationName = reservation.ReservationName;
        ReservationGuestName = reservation.GuestName;
        ReservationGuestPhone = reservation.GuestPhone;
        ReservationPartySizeText = reservation.PartySize.ToString(CultureInfo.InvariantCulture);
        ReservationDate = reservation.ReservedFor.Date;
        ReservationTimeText = reservation.ReservedFor.ToString("HH:mm");
        ReservationChannel = reservation.Channel;
        ReservationStatus = reservation.Status;
        ReservationTableId = reservation.TableId;
        ReservationDepositPaid = reservation.DepositPaid;
        ReservationDepositAmountText = reservation.DepositAmountUsd.ToString("0.##", CultureInfo.InvariantCulture);
        ReservationNotes = reservation.UserNotes;
        IsReservationDialogOpen = true;
    }

    private async Task SaveReservationAsync()
    {
        if (!CanManageReservations)
            return;

        var guestName = (ReservationGuestName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(guestName))
        {
            MessageBox.Show("Guest name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var reservationName = (ReservationName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(reservationName))
            reservationName = guestName;

        if (!int.TryParse(ReservationPartySizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var partySize) || partySize <= 0)
        {
            MessageBox.Show("Party size must be a positive integer.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TimeSpan.TryParseExact(ReservationTimeText.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var time))
        {
            MessageBox.Show("Time must be in HH:mm format (e.g. 19:30).", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var reservedFor = ReservationDate.Date.Add(time);
        var now = DateTime.Now;
        if (reservedFor < now.AddMinutes(-1))
        {
            MessageBox.Show("Reservation time cannot be in the past.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (reservedFor > now.AddDays(30))
        {
            MessageBox.Show("Reservation exceeds 30-day booking window.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var depositAmount = ParseDecimalOrZero(ReservationDepositAmountText);
        if (!ReservationDepositPaid)
            depositAmount = 0m;

        try
        {
            var tableLookup = ReservationTableId.HasValue
                ? Tables.FirstOrDefault(t => t.Id == ReservationTableId.Value)
                : null;
            if (ReservationTableId.HasValue && tableLookup is null)
            {
                MessageBox.Show("Selected table no longer exists. Please select another table.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (tableLookup is not null && string.Equals(tableLookup.Status, "Maintenance", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("This table is in Maintenance and cannot be assigned to a reservation.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool previousDepositPaid;
            decimal previousDepositAmount;
            ReservationBooking reservation;

            if (_editingReservationId is int reservationId)
            {
                reservation = _allReservations.First(r => r.Id == reservationId);
                previousDepositPaid = reservation.DepositPaid;
                previousDepositAmount = reservation.DepositAmountUsd;
            }
            else
            {
                reservation = new ReservationBooking
                {
                    UniqueId = UniqueIdGenerator.NewId("RSV"),
                    CreatedAt = DateTime.Now
                };
                previousDepositPaid = false;
                previousDepositAmount = 0m;
            }

            if (ReservationTableId.HasValue)
            {
                var windowLow = reservedFor.AddMinutes(-120);
                var windowHigh = reservedFor.AddMinutes(120);
                var blocked = _allReservations.Any(r =>
                    r.Id != reservation.Id &&
                    r.TableId == ReservationTableId &&
                    (r.Status == "Pending" || r.Status == "Confirmed" || r.Status == "Arrived") &&
                    r.ReservedFor > windowLow &&
                    r.ReservedFor < windowHigh);

                if (blocked)
                {
                    MessageBox.Show("Selected table already has a nearby active booking. Choose another table.", "Table conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            var customer = await UpsertCustomerProfileCloudAsync(guestName, ReservationGuestPhone, ReservationChannel).ConfigureAwait(true);

            reservation.CustomerProfileId = customer?.Id;
            reservation.ReservationName = reservationName;
            reservation.GuestName = guestName;
            reservation.GuestPhone = (ReservationGuestPhone ?? string.Empty).Trim();
            reservation.PartySize = partySize;
            reservation.ReservedFor = reservedFor;
            reservation.Channel = ReservationChannel;
            reservation.Status = _editingReservationId.HasValue ? reservation.Status : "Pending";
            reservation.UserNotes = ReservationNotes ?? string.Empty;
            reservation.TableId = ReservationTableId;
            reservation.DepositPaid = ReservationDepositPaid;
            reservation.DepositAmountUsd = decimal.Round(depositAmount, 2);
            reservation.DepositCurrencyCode = "USD";
            reservation.DepositForfeited = reservation.Status == "NoShow" && reservation.DepositPaid && reservation.DepositAmountUsd > 0;
            reservation.CreatedByEmployeeId = AppSession.StaffEmployeeId;
            reservation.CreatedByName = SidebarUserDisplayName;
            reservation.UpdatedAt = DateTime.Now;

            DesktopCloudPersistence.PushUpsertBlocking(reservation);
            await SyncDepositLedgerCloudAsync(reservation, previousDepositPaid, previousDepositAmount).ConfigureAwait(true);

            IsReservationDialogOpen = false;
            _ = LoadAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not save reservation in the cloud.\n\n{ex.Message}",
                "Reservations",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task UpdateReservationStatusAsync(ReservationBooking? reservation, string targetStatus)
    {
        if (!CanManageReservations || reservation is null)
            return;

        try
        {
            var existing = _allReservations.FirstOrDefault(r => r.Id == reservation.Id);
            if (existing is null)
                return;

            var previousStatus = existing.Status;
            if (!CanTransition(existing, targetStatus, out var transitionError))
            {
                MessageBox.Show(transitionError, "Reservation status", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            existing.Status = targetStatus;
            existing.UpdatedAt = DateTime.Now;
            if (string.Equals(targetStatus, "NoShow", StringComparison.OrdinalIgnoreCase)
                && existing.DepositPaid
                && existing.DepositAmountUsd > 0)
            {
                existing.DepositForfeited = true;
            }

            await SyncDepositLedgerCloudAsync(existing, previousStatus == "NoShow" && existing.DepositPaid, existing.DepositAmountUsd).ConfigureAwait(true);

            if (existing.CustomerProfileId is int customerId)
            {
                var profiles = await _data.GetCustomerProfilesAsync().ConfigureAwait(true);
                var customer = profiles.FirstOrDefault(c => c.Id == customerId);
                if (customer is not null)
                {
                    if (!string.Equals(previousStatus, "NoShow", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(targetStatus, "NoShow", StringComparison.OrdinalIgnoreCase))
                        customer.NoShowCount += 1;

                    if (!string.Equals(previousStatus, "Completed", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(targetStatus, "Completed", StringComparison.OrdinalIgnoreCase))
                    {
                        customer.CompletedReservationCount += 1;
                        customer.LastVisitAt = DateTime.Now;
                    }

                    DesktopCloudPersistence.PushUpsertBlocking(customer);
                }
            }

            DesktopCloudPersistence.PushUpsertBlocking(existing);
            _ = LoadAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not update reservation in the cloud.\n\n{ex.Message}",
                "Reservations",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task<CustomerProfile?> UpsertCustomerProfileCloudAsync(string guestName, string guestPhone, string channel)
    {
        var normalizedPhone = (guestPhone ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedPhone))
            return null;

        var profiles = await _data.GetCustomerProfilesAsync().ConfigureAwait(true);
        var existing = profiles.FirstOrDefault(c =>
            string.Equals(c.PrimaryPhone, normalizedPhone, StringComparison.Ordinal));
        if (existing is not null)
        {
            var push = false;
            if (string.IsNullOrWhiteSpace(existing.FullName))
            {
                existing.FullName = guestName;
                push = true;
            }

            if (!string.Equals(existing.PreferredContactChannel ?? string.Empty, channel, StringComparison.Ordinal))
            {
                existing.PreferredContactChannel = channel;
                push = true;
            }

            if (push)
                DesktopCloudPersistence.PushUpsertBlocking(existing);
            return existing;
        }

        var uniqueId = UniqueIdGenerator.NewId("CUS");
        var profile = new CustomerProfile
        {
            UniqueId = uniqueId,
            FullName = guestName,
            PrimaryPhone = normalizedPhone,
            PreferredContactChannel = channel
        };
        DesktopCloudPersistence.PushUpsertBlocking(profile);
        profiles = await _data.GetCustomerProfilesAsync().ConfigureAwait(true);
        return profiles.FirstOrDefault(c => c.UniqueId == uniqueId) ?? profile;
    }

    private static decimal ParseDecimalOrZero(string input)
    {
        var normalized = (input ?? string.Empty).Trim().Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            return 0m;
        return parsed < 0 ? 0 : parsed;
    }

    private static bool Contains(string? source, string text)
        => !string.IsNullOrWhiteSpace(source)
           && source.Contains(text, StringComparison.OrdinalIgnoreCase);

    private static bool CanTransition(ReservationBooking reservation, string targetStatus, out string error)
    {
        error = string.Empty;
        var current = reservation.Status?.Trim() ?? string.Empty;

        if (string.Equals(targetStatus, current, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(targetStatus, "Confirmed", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(current, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                error = "Only pending reservations can be confirmed.";
                return false;
            }
            return true;
        }

        if (string.Equals(targetStatus, "Arrived", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(current, "Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                error = "Only confirmed reservations can be marked as arrived.";
                return false;
            }
            if (reservation.ReservedFor.Date > DateTime.Today)
            {
                error = "Arrived is available on the reservation day only.";
                return false;
            }
            return true;
        }

        if (string.Equals(targetStatus, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(current, "Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                error = "Only confirmed reservations can be moved back to pending.";
                return false;
            }
            return true;
        }

        if (string.Equals(targetStatus, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(current, "Arrived", StringComparison.OrdinalIgnoreCase))
            {
                error = "Only arrived reservations can be completed.";
                return false;
            }
            return true;
        }

        if (string.Equals(targetStatus, "NoShow", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(current, "Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                error = "Only confirmed reservations can be marked no-show.";
                return false;
            }
            if (reservation.ReservedFor.Date > DateTime.Today)
            {
                error = "No-show is available on or after the reservation day.";
                return false;
            }
            return true;
        }

        if (string.Equals(targetStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(current, "Completed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(current, "NoShow", StringComparison.OrdinalIgnoreCase))
            {
                error = "Completed or no-show reservations cannot be cancelled.";
                return false;
            }
            return true;
        }

        return true;
    }

    private async Task SyncDepositLedgerCloudAsync(
        ReservationBooking reservation,
        bool previousDepositPaid,
        decimal previousDepositAmountUsd)
    {
        var marker = $"|RSV:{reservation.UniqueId}|DEPOSIT|";
        var txs = await _data.GetMoneyTransactionsAsync().ConfigureAwait(true);
        var existingTx = txs.FirstOrDefault(t => t.Justification?.Contains(marker, StringComparison.Ordinal) == true);
        var hasDeposit = reservation.DepositPaid && reservation.DepositAmountUsd > 0m;

        if (!hasDeposit)
        {
            if (existingTx is not null)
                DesktopCloudPersistence.PushDeleteBlocking(existingTx);
            return;
        }

        var amountUsd = Math.Round(reservation.DepositAmountUsd, 2);
        var amountFc = CurrencyHelper.ConvertUsdToFc(amountUsd);
        var label = string.IsNullOrWhiteSpace(reservation.GuestName) ? reservation.UniqueId : reservation.GuestName.Trim();
        var statusTag = string.Equals(reservation.Status, "NoShow", StringComparison.OrdinalIgnoreCase)
            ? " (forfeited/no-show)"
            : string.Empty;
        var justification = $"Reservation deposit from {label}{statusTag} {marker}";

        if (existingTx is null)
        {
            DesktopCloudPersistence.PushUpsertBlocking(new MoneyTransaction
            {
                Amount = amountUsd,
                AmountUsd = amountUsd,
                AmountFc = amountFc,
                Date = DateTime.Now,
                Type = "Revenue",
                Category = "Other",
                CurrencyCode = CurrencyHelper.Usd,
                ExchangeRateUsed = CurrencyHelper.FcPerUsd,
                IsFixed = false,
                Justification = justification
            });
            return;
        }

        if (previousDepositPaid && previousDepositAmountUsd > 0m && amountUsd != previousDepositAmountUsd)
            existingTx.Date = DateTime.Now;

        existingTx.Amount = amountUsd;
        existingTx.AmountUsd = amountUsd;
        existingTx.AmountFc = amountFc;
        existingTx.Type = "Revenue";
        existingTx.Category = "Other";
        existingTx.CurrencyCode = CurrencyHelper.Usd;
        existingTx.ExchangeRateUsed = CurrencyHelper.FcPerUsd;
        existingTx.IsFixed = false;
        existingTx.Justification = justification;
        DesktopCloudPersistence.PushUpsertBlocking(existingTx);
    }

    private static void Log(string message)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EliteRestaurantPro",
                "logs");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "reservations-debug.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // ignore logging errors
        }
    }
}
