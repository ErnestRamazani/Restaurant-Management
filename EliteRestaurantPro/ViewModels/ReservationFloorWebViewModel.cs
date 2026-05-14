using System.Collections.ObjectModel;
using System.Windows.Input;
using EliteRestaurantPro.ApiClients;

namespace EliteRestaurantPro.ViewModels;

public sealed class ReservationFloorWebViewModel : AdminBaseViewModel
{
    private readonly CashierReservationsApiClient _reservations = new();
    private bool _isLoading;
    private bool _isDetailOpen;
    private string _statusMessage = string.Empty;
    private string _rescheduleLocalText = string.Empty;
    private CashierEngagementDetailDto? _selectedDetail;

    public override string ActivePage => "Reservations";

    public ObservableCollection<CashierEngagementListRow> Reservations { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public bool IsDetailOpen
    {
        get => _isDetailOpen;
        private set => SetField(ref _isDetailOpen, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public string RescheduleLocalText
    {
        get => _rescheduleLocalText;
        set => SetField(ref _rescheduleLocalText, value);
    }

    public CashierEngagementDetailDto? SelectedDetail
    {
        get => _selectedDetail;
        private set
        {
            if (!SetField(ref _selectedDetail, value))
                return;
            OnPropertyChanged(nameof(SelectedStatusBadge));
            OnPropertyChanged(nameof(SelectedStatusText));
            OnPropertyChanged(nameof(CanRunScheduledActions));
            OnPropertyChanged(nameof(IsCheckedInSelection));
            OnPropertyChanged(nameof(SelectedArrivalText));
            OnPropertyChanged(nameof(SelectedEndText));
            OnPropertyChanged(nameof(SelectedActualArrivalText));
            OnPropertyChanged(nameof(SelectedActualReleaseText));
            OnPropertyChanged(nameof(SelectedCreatedText));
            OnPropertyChanged(nameof(SelectedUpdatedText));
        }
    }

    public string SelectedStatusText => EngagementStatusLabel(SelectedDetail?.Status ?? string.Empty);
    public string SelectedStatusBadge => SelectedDetailStatusClass(SelectedDetail?.Status ?? string.Empty);
    public bool CanRunScheduledActions => string.Equals(SelectedDetail?.Status, "Scheduled", StringComparison.OrdinalIgnoreCase);
    public bool IsCheckedInSelection => string.Equals(SelectedDetail?.Status, "CheckedIn", StringComparison.OrdinalIgnoreCase);
    public string SelectedArrivalText => FormatDateTimeLocal(SelectedDetail?.PlannedStartUtc);
    public string SelectedEndText => FormatDateTimeLocal(SelectedDetail?.PlannedEndUtc);
    public string SelectedActualArrivalText => FormatDateTimeLocal(SelectedDetail?.ActualStartUtc);
    public string SelectedActualReleaseText => FormatDateTimeLocal(SelectedDetail?.ActualEndUtc);
    public string SelectedCreatedText => FormatDateTimeLocal(SelectedDetail?.CreatedAtUtc);
    public string SelectedUpdatedText => FormatDateTimeLocal(SelectedDetail?.UpdatedAtUtc);

    public ICommand RefreshCommand { get; }
    public ICommand OpenDetailCommand { get; }
    public ICommand CloseDetailCommand { get; }
    public ICommand MarkArrivedCommand { get; }
    public ICommand MarkNoShowCommand { get; }
    public ICommand MarkCancelledCommand { get; }
    public ICommand ShowReschedulePanelCommand { get; }
    public ICommand SaveRescheduleCommand { get; }

    public ReservationFloorWebViewModel(Action<BaseViewModel> navigate)
        : base(navigate)
    {
        RefreshCommand = new RelayCommand(async _ => await LoadReservationsAsync());
        OpenDetailCommand = new RelayCommand(async id => await OpenDetailAsync(id));
        CloseDetailCommand = new RelayCommand(_ => CloseDetail());
        MarkArrivedCommand = new RelayCommand(async _ => await RunScheduledActionAsync("arrived"));
        MarkNoShowCommand = new RelayCommand(async _ => await RunScheduledActionAsync("no-show"));
        MarkCancelledCommand = new RelayCommand(async _ => await RunScheduledActionAsync("cancel"));
        ShowReschedulePanelCommand = new RelayCommand(_ => ShowReschedulePanel());
        SaveRescheduleCommand = new RelayCommand(async _ => await SaveRescheduleAsync());
        _ = LoadReservationsAsync();
    }

    private async Task LoadReservationsAsync()
    {
        IsLoading = true;
        StatusMessage = string.Empty;
        try
        {
            var rows = await _reservations.ListEngagementsAsync().ConfigureAwait(true);
            Reservations.Clear();
            foreach (var row in rows)
                Reservations.Add(row);
            if (rows.Count == 0)
                StatusMessage = "No upcoming reservations. New guest bookings appear automatically.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.GetBaseException().Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task OpenDetailAsync(object? engagementId)
    {
        if (!TryResolveId(engagementId, out var id))
            return;
        try
        {
            var detail = await _reservations.GetEngagementAsync(id).ConfigureAwait(true);
            if (detail is null)
                return;
            SelectedDetail = detail;
            IsDetailOpen = true;
            RescheduleLocalText = ToLocalDatetimeLocalValue(detail.PlannedStartUtc);
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.GetBaseException().Message;
        }
    }

    private async Task RunScheduledActionAsync(string action)
    {
        if (SelectedDetail is null || !CanRunScheduledActions)
            return;
        try
        {
            if (action == "arrived")
                await _reservations.MarkArrivedAsync(SelectedDetail.Id).ConfigureAwait(true);
            else if (action == "no-show")
                await _reservations.MarkNoShowAsync(SelectedDetail.Id).ConfigureAwait(true);
            else if (action == "cancel")
                await _reservations.MarkCancelledAsync(SelectedDetail.Id).ConfigureAwait(true);

            IsDetailOpen = false;
            SelectedDetail = null;
            await LoadReservationsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.GetBaseException().Message;
        }
    }

    private void ShowReschedulePanel()
    {
        if (SelectedDetail is null)
            return;
        RescheduleLocalText = ToLocalDatetimeLocalValue(SelectedDetail.PlannedStartUtc);
    }

    private async Task SaveRescheduleAsync()
    {
        if (SelectedDetail is null || !CanRunScheduledActions)
            return;
        if (!TryParseLocalDatetimeValue(RescheduleLocalText, out var localValue))
        {
            StatusMessage = "Choose a valid local date/time to reschedule.";
            return;
        }

        try
        {
            var utc = DateTime.SpecifyKind(localValue, DateTimeKind.Local).ToUniversalTime();
            await _reservations.RescheduleAsync(SelectedDetail.Id, utc).ConfigureAwait(true);
            await OpenDetailAsync(SelectedDetail.Id).ConfigureAwait(true);
            await LoadReservationsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.GetBaseException().Message;
        }
    }

    private void CloseDetail()
    {
        IsDetailOpen = false;
        SelectedDetail = null;
        RescheduleLocalText = string.Empty;
    }

    private static bool TryResolveId(object? value, out int id)
    {
        id = 0;
        return value switch
        {
            int i when i > 0 => (id = i) > 0,
            string s when int.TryParse(s, out var parsed) && parsed > 0 => (id = parsed) > 0,
            CashierEngagementListRow row when row.Id > 0 => (id = row.Id) > 0,
            _ => false
        };
    }

    private static string ToLocalDatetimeLocalValue(DateTime utc)
    {
        var local = DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        return local.ToString("yyyy-MM-ddTHH:mm");
    }

    private static bool TryParseLocalDatetimeValue(string? text, out DateTime value)
    {
        var trimmed = (text ?? string.Empty).Trim();
        return DateTime.TryParse(trimmed, out value);
    }

    private static string FormatDateTimeLocal(DateTime? utc) =>
        utc is null ? "—" : DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToLocalTime().ToString("g");

    public static string EngagementStatusLabel(string raw)
    {
        var s = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return s switch
        {
            "scheduled" => "Scheduled",
            "checkedin" => "Checked in",
            "noshow" => "No show",
            "cancelled" => "Cancelled",
            "completed" => "Completed",
            _ => string.IsNullOrWhiteSpace(raw) ? "—" : raw
        };
    }

    public static string SelectedDetailStatusClass(string raw)
    {
        var s = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return s switch
        {
            "scheduled" => "Scheduled",
            "checkedin" => "CheckedIn",
            _ => "Muted"
        };
    }
}
