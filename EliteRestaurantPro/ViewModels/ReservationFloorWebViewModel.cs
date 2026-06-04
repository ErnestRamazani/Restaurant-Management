using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.Globalization;

using System.Windows.Input;

using EliteRestaurant.Core.Utils;

using EliteRestaurantPro.ApiClients;

using EliteRestaurantPro.Localization;



namespace EliteRestaurantPro.ViewModels;



public sealed class ReservationEngagementListItem

{

    public ReservationEngagementListItem(CashierEngagementListRow source) => Source = source;



    public CashierEngagementListRow Source { get; }



    public int Id => Source.Id;

    public string GuestName => Source.GuestName;

    public string StatusText { get; init; } = string.Empty;

    public string RefText { get; init; } = string.Empty;

    public string PlannedStartText { get; init; } = string.Empty;

    public string TableTagText { get; init; } = string.Empty;

    public string PartyTagText { get; init; } = string.Empty;

    public string TelTagText { get; init; } = string.Empty;

    public bool IsRecentlyClosed =>
        !string.Equals(Source.Status, "Scheduled", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(Source.Status, "CheckedIn", StringComparison.OrdinalIgnoreCase);

}



public sealed class ReservationFloorWebViewModel : AdminBaseViewModel

{

    private readonly CashierReservationsApiClient _reservations = new();

    private readonly List<CashierEngagementListRow> _cachedRows = [];

    private bool _isLoading;

    private bool _isDetailOpen;

    private string _statusMessage = string.Empty;

    private string _rescheduleLocalText = string.Empty;

    private CashierEngagementDetailDto? _selectedDetail;



    public override string ActivePage => "Reservations";



    public ObservableCollection<ReservationEngagementListItem> Reservations { get; } = [];



    public string ResTitle => Loc.Admin("resTitle", "Reservations");

    public string ResSubtitle => Loc.Admin("resSubtitle",
        "Active bookings plus reservations closed in the last 7 days. Tap a row for details and updates.");

    public string ResRefreshLabel => Loc.Admin("refresh", "Refresh");

    public string ResLoadingText => Loc.Admin("resLoading", "Loading reservations…");

    public string ResCloseLabel => Loc.Common("close", "Close");

    public string ResSectionVisitLabel => Loc.Admin("resSectionVisit", "VISIT");

    public string ResSectionGuestLabel => Loc.Admin("resSectionGuest", "GUEST");

    public string ResSectionActionsLabel => Loc.Admin("resSectionActions", "ACTIONS");

    public string ResArrivedLabel => Loc.Admin("resArrived", "Arrived");

    public string ResRescheduleLabel => Loc.Admin("resReschedule", "Reschedule");

    public string ResNoShowLabel => Loc.Admin("resNoShow", "No show");

    public string ResCancelledActionLabel => Loc.Admin("resCancelledAction", "Cancelled");

    public string ResRescheduleStartLocalLabel => Loc.Admin("resRescheduleStartLocal", "Reschedule start (local)");

    public string ResSaveNewTimeLabel => Loc.Admin("resSaveNewTime", "Save new time");



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

            NotifyDetailBindings();

        }

    }



    public string SelectedStatusText => AdminTextLocalizer.TranslateEngagementStatus(SelectedDetail?.Status);

    public string SelectedStatusBadge => SelectedDetailStatusClass(SelectedDetail?.Status ?? string.Empty);

    public bool CanRunScheduledActions => string.Equals(SelectedDetail?.Status, "Scheduled", StringComparison.OrdinalIgnoreCase);

    public bool IsCheckedInSelection => string.Equals(SelectedDetail?.Status, "CheckedIn", StringComparison.OrdinalIgnoreCase);

    public string SelectedArrivalText => FormatDateTimeLocal(SelectedDetail?.PlannedStartUtc);

    public string SelectedEndText => FormatDateTimeLocal(SelectedDetail?.PlannedEndUtc);

    public string SelectedActualArrivalText => FormatDateTimeLocal(SelectedDetail?.ActualStartUtc);

    public string SelectedActualReleaseText => FormatDateTimeLocal(SelectedDetail?.ActualEndUtc);

    public string SelectedCreatedText => FormatDateTimeLocal(SelectedDetail?.CreatedAtUtc);

    public string SelectedUpdatedText => FormatDateTimeLocal(SelectedDetail?.UpdatedAtUtc);

    public string SelectedPartySizeLine => SelectedDetail is null

        ? string.Empty

        : Loc.Admin("resPartySizeLine", "Party size: {{value}}",

            new Dictionary<string, string> { ["value"] = SelectedDetail.PartySize.ToString(CultureInfo.InvariantCulture) });

    public string SelectedTableLine => SelectedDetail is null
        ? string.Empty
        : Loc.Admin("resTableLine", "Table: {{value}}",
            new Dictionary<string, string>
            {
                ["value"] = AdminTextLocalizer.FormatReservationTableTag(SelectedDetail.TableLabel)
            });

    public string SelectedPhoneLine => SelectedDetail is null

        ? string.Empty

        : Loc.Admin("resPhoneLine", "Phone: {{value}}",

            new Dictionary<string, string> { ["value"] = SelectedDetail.GuestPhone });

    public string SelectedEmailLine => SelectedDetail is null

        ? string.Empty

        : Loc.Admin("resEmailLine", "Email: {{value}}",

            new Dictionary<string, string> { ["value"] = SelectedDetail.GuestEmail });

    public string SelectedNotesLine => SelectedDetail is null

        ? string.Empty

        : Loc.Admin("resNotesLine", "Notes: {{value}}",

            new Dictionary<string, string> { ["value"] = SelectedDetail.UserNotes });



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



    protected override void RefreshLocalizedStrings()

    {

        base.RefreshLocalizedStrings();

        Notify(

            nameof(ResTitle),

            nameof(ResSubtitle),

            nameof(ResRefreshLabel),

            nameof(ResLoadingText),

            nameof(ResCloseLabel),

            nameof(ResSectionVisitLabel),

            nameof(ResSectionGuestLabel),

            nameof(ResSectionActionsLabel),

            nameof(ResArrivedLabel),

            nameof(ResRescheduleLabel),

            nameof(ResNoShowLabel),

            nameof(ResCancelledActionLabel),

            nameof(ResRescheduleStartLocalLabel),

            nameof(ResSaveNewTimeLabel));

        RebuildReservationListItems();

        if (SelectedDetail is not null)

            NotifyDetailBindings();

    }



    private void NotifyDetailBindings()

    {

        OnPropertyChanged(nameof(SelectedStatusText));

        OnPropertyChanged(nameof(SelectedStatusBadge));

        OnPropertyChanged(nameof(CanRunScheduledActions));

        OnPropertyChanged(nameof(IsCheckedInSelection));

        OnPropertyChanged(nameof(SelectedArrivalText));

        OnPropertyChanged(nameof(SelectedEndText));

        OnPropertyChanged(nameof(SelectedActualArrivalText));

        OnPropertyChanged(nameof(SelectedActualReleaseText));

        OnPropertyChanged(nameof(SelectedCreatedText));

        OnPropertyChanged(nameof(SelectedUpdatedText));

        OnPropertyChanged(nameof(SelectedPartySizeLine));

        OnPropertyChanged(nameof(SelectedTableLine));

        OnPropertyChanged(nameof(SelectedPhoneLine));

        OnPropertyChanged(nameof(SelectedEmailLine));

        OnPropertyChanged(nameof(SelectedNotesLine));

    }



    private async Task LoadReservationsAsync()

    {

        IsLoading = true;

        StatusMessage = string.Empty;

        try

        {

            var rows = await _reservations.ListEngagementsAsync().ConfigureAwait(true);

            _cachedRows.Clear();

            _cachedRows.AddRange(rows);

            RebuildReservationListItems();

            var closedRecently = rows.Count(r =>
                !string.Equals(r.Status, "Scheduled", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(r.Status, "CheckedIn", StringComparison.OrdinalIgnoreCase));

            if (rows.Count == 0)
                StatusMessage = Loc.Admin("resEmptyStatus",
                    "No active or recent reservations. New guest bookings appear automatically.");
            else if (closedRecently > 0)
                StatusMessage = Loc.Admin("resIncludesClosedRecent",
                    "{{count}} reservation(s) closed in the last 7 days — still listed below for reference.",
                    new Dictionary<string, string> { ["count"] = closedRecently.ToString(CultureInfo.InvariantCulture) });
            else
                StatusMessage = string.Empty;

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



    private void RebuildReservationListItems()

    {

        var tz = SettingsManager.Load().BusinessProfile.RestaurantTimeZoneId;

        Reservations.Clear();

        foreach (var row in _cachedRows)

        {

            Reservations.Add(new ReservationEngagementListItem(row)

            {

                StatusText = AdminTextLocalizer.TranslateEngagementStatus(row.Status),

                RefText = Loc.Admin("resRef", "REF · {{id}}",

                    new Dictionary<string, string> { ["id"] = row.Id.ToString(CultureInfo.InvariantCulture) }),

                PlannedStartText = AdminTextLocalizer.FormatReservationPlannedStart(row.PlannedStartUtc, tz),

                TableTagText = AdminTextLocalizer.FormatReservationTableTag(row.TableLabel),

                PartyTagText = Loc.Admin("resPartyTag", "Party {{size}}",

                    new Dictionary<string, string> { ["size"] = row.PartySize.ToString(CultureInfo.InvariantCulture) }),

                TelTagText = Loc.Admin("resTelTag", "Tel {{phone}}",

                    new Dictionary<string, string> { ["phone"] = row.GuestPhone })

            });

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

            StatusMessage = Loc.Admin("resInvalidRescheduleTime",

                "Choose a valid local date/time to reschedule.");

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

            ReservationEngagementListItem row when row.Id > 0 => (id = row.Id) > 0,

            CashierEngagementListRow legacy when legacy.Id > 0 => (id = legacy.Id) > 0,

            _ => false

        };

    }



    private static string ToLocalDatetimeLocalValue(DateTime utc)

    {

        var tz = SettingsManager.Load().BusinessProfile.RestaurantTimeZoneId;

        var local = RestaurantTimeZone.UtcToRestaurant(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);

        return local.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    }



    private static bool TryParseLocalDatetimeValue(string? text, out DateTime value)

    {

        var trimmed = (text ?? string.Empty).Trim();

        return DateTime.TryParse(trimmed, AdminTextLocalizer.UiCulture, DateTimeStyles.None, out value);

    }



    private static string FormatDateTimeLocal(DateTime? utc)

    {

        var tz = SettingsManager.Load().BusinessProfile.RestaurantTimeZoneId;

        return AdminTextLocalizer.FormatReservationDateTimeLocal(utc, tz);

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

