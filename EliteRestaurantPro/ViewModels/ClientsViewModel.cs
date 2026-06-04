using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Contracts.Clients;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Localization;
using EliteRestaurantPro.Views;

namespace EliteRestaurantPro.ViewModels;

public sealed class ClientOrderListItem
{
    public ClientOrderListItem(ClientOrderTicketDto dto) => Dto = dto;

    public ClientOrderTicketDto Dto { get; }
    public int OrderId => Dto.OrderId;
    public string OrderCode => Dto.OrderCode;
    public string CreatedText => Dto.CreatedAt.ToString("MMM d, yyyy · HH:mm");
    public string TotalText => $"${Dto.GrandTotalUsd:N2}";
    public string Status => OrderDisplayStatus.ForOrder(Dto.Status, Dto.ClientSettlement, Dto.AmountOnAccountUsd, Dto.ClientDebtSettledUsd);
    public string SettlementText => FormatSettlement(Dto);

    public string DisplayStatus { get; set; } = string.Empty;
    public string DisplaySettlementText { get; set; } = string.Empty;
    public string DisplayCreatedText { get; set; } = string.Empty;

    private static string FormatSettlement(ClientOrderTicketDto d)
    {
        if (ClientSettlement.IsOnAccount(d.ClientSettlement))
        {
            if (d.ClientDebtSettledUsd >= d.AmountOnAccountUsd - 0.01m)
                return "On account · settled";
            return $"On account · ${d.AmountOnAccountUsd:N2} open";
        }

        if (d.RevenueRecognized)
            return "Paid · revenue recognized";
        if (ClientSettlement.IsPaidAtCompletion(d.ClientSettlement) || d.ClientSettlement == ClientSettlement.None)
            return "Paid at completion";
        return string.IsNullOrWhiteSpace(d.ClientSettlement) ? "—" : d.ClientSettlement;
    }
}

public sealed class ClientLedgerListItem
{
    public ClientLedgerListItem(ClientLedgerEntryDto dto) => Dto = dto;

    public ClientLedgerEntryDto Dto { get; }
    public int? OrderId => Dto.OrderId;
    public bool HasOrder => Dto.OrderId is > 0;
    public string TypeLabel => Dto.EntryType switch
    {
        ClientDebtLedgerEntryType.Charge => "Charge",
        ClientDebtLedgerEntryType.Payment => "Payment",
        ClientDebtLedgerEntryType.RevenueRecognized => "Revenue",
        _ => Dto.EntryType
    };

    public string AmountText
    {
        get
        {
            var sign = Dto.EntryType == ClientDebtLedgerEntryType.Payment ? "-" : "+";
            return $"{sign}${Math.Abs(Dto.AmountUsd):N2}";
        }
    }

    public string BalanceText => $"${Dto.BalanceAfterUsd:N2}";
    public string NoteText => string.IsNullOrWhiteSpace(Dto.Note) ? "—" : Dto.Note.Trim();
    public string CreatedText => RestaurantTimeZone.FormatUtc(
        Dto.CreatedAtUtc,
        SettingsManager.Load().BusinessProfile.RestaurantTimeZoneId,
        "MMM d · HH:mm");
    public string OrderCodeText => string.IsNullOrWhiteSpace(Dto.OrderCode) ? string.Empty : Dto.OrderCode;

    public string DisplayTypeLabel { get; set; } = string.Empty;
    public string DisplayNoteText { get; set; } = string.Empty;
    public string DisplayCreatedText { get; set; } = string.Empty;
    public string DisplayOrderPrefix { get; set; } = string.Empty;
    public string DisplayBalPrefix { get; set; } = string.Empty;
}

public sealed class ClientsViewModel : AdminBaseViewModel
{
    private readonly ClientsApiClient _clients = new();
    private RestaurantClientListItemDto? _selectedListItem;
    private RestaurantClientProfileDto? _profile;
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isDialogOpen;
    private bool _isSettleDialogOpen;
    private bool _isSettling;
    private int? _editingClientId;
    private string _dialogFullName = string.Empty;
    private string _dialogPhone = string.Empty;
    private string _dialogEmail = string.Empty;
    private string _dialogNotes = string.Empty;
    private string _dialogTitle = "New client";
    private string _settleAmountText = string.Empty;
    private string _settlePasscode = string.Empty;
    private string _settleNote = string.Empty;
    private int? _lastListedClientCount;

    public override string ActivePage => "Clients";

    public bool IsAdmin => ShowFullAdminNav;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value))
                return;
            _ = ReloadListAsync();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public ObservableCollection<RestaurantClientListItemDto> RegularClients { get; } = new();
    public ObservableCollection<RestaurantClientListItemDto> StaffClients { get; } = new();
    public ObservableCollection<ClientOrderListItem> ProfileOrders { get; } = new();
    public ObservableCollection<ClientLedgerListItem> ProfileLedger { get; } = new();

    public OrderDetailPanelViewModel OrderDetail { get; } = new();

    public RestaurantClientListItemDto? SelectedListItem
    {
        get => _selectedListItem;
        set
        {
            if (!SetField(ref _selectedListItem, value))
                return;
            OnPropertyChanged(nameof(SelectedClientId));
            _ = LoadProfileAsync();
        }
    }

    public int? SelectedClientId => SelectedListItem?.Id;

    public string ProfileDebtText =>
        _profile is null ? "—" : $"${_profile.Client.DebtBalanceUsd:N2}";

    public string ProfileRevenueText =>
        _profile is null ? "—" : $"${_profile.Client.TotalSettledRevenueUsd:N2}";

    public string ProfileTotalGeneratedText =>
        _profile is null ? "—" : $"${_profile.Client.TotalGeneratedRevenueUsd:N2}";

    public string ProfileOrderCountText =>
        _profile is null ? "—" : $"{_profile.Orders.Count}";

    public string ProfileOpenOnAccountText =>
        _profile is null
            ? "—"
            : $"${_profile.Orders.Where(o => ClientSettlement.IsOnAccount(o.ClientSettlement) && o.ClientDebtSettledUsd < o.AmountOnAccountUsd - 0.01m).Sum(o => o.AmountOnAccountUsd - o.ClientDebtSettledUsd):N2}";

    public string ProfileTitle =>
        _profile?.Client.FullName ?? SelectClientPrompt;

    public string ProfileSubtitle =>
        _profile is null
            ? string.Empty
            : $"{_profile.Client.UniqueId} · {_profile.Client.PrimaryPhone}";

    public string PageTitle => Loc.Admin("cltTitle", "Clients");
    public string SearchTooltip => Loc.Admin("cltSearchTooltip", "Search name, phone, or ID");
    public string RegularSectionLabel => Loc.Admin("cltRegular", "REGULAR");
    public string StaffSectionLabel => Loc.Admin("cltStaff", "STAFF");
    public string StaffListHint => Loc.Admin("cltStaffListHint", "Staff · edit discount in Employees");
    public string SelectClientPrompt => Loc.Admin("cltSelectClient", "Select a client");
    public string DebtBalanceLabel => Loc.Admin("cltDebtBalance", "DEBT BALANCE");
    public string SettledRevenueLabel => Loc.Admin("cltSettledRevenue", "SETTLED REVENUE");
    public string TotalGeneratedLabel => Loc.Admin("cltTotalGenerated", "TOTAL GENERATED");
    public string OrdersLabel => Loc.Admin("cltOrders", "ORDERS");
    public string OpenOnAccountLabel => Loc.Admin("cltOpenOnAccount", "OPEN ON ACCOUNT");
    public string SettleDebtLabel => Loc.Admin("cltSettleDebt", "Settle debt");
    public string EditClientLabel => Loc.Admin("cltEditClient", "Edit client");
    public string NewClientLabel => Loc.Admin("cltNewClient", "New client");
    public string DebtLedgerTitle => Loc.Admin("cltDebtLedger", "Debt ledger");
    public string DebtLedgerSubtitle => Loc.Admin("cltDebtLedgerSub", "Charges, payments, and revenue recognition.");
    public string OrderHistoryTitle => Loc.Admin("cltOrderHistory", "Order history");
    public string OrderHistorySubtitle => Loc.Admin("cltOrderHistorySub", "Tap an order for line items and totals.");
    public string ViewOrderLabel => Loc.Admin("cltViewOrder", "View order");
    public string DialogIntro => Loc.Admin("cltDialogIntro", "Register a client for optional order linking and on-account payments.");
    public string FieldFullNameLabel => Loc.Admin("cltFieldFullName", "FULL NAME");
    public string FieldFullNameTooltip => Loc.Admin("cltFieldFullNameTooltip", "Required — at least 2 characters");
    public string FieldPhoneLabel => Loc.Admin("cltFieldPhone", "PHONE (OPTIONAL)");
    public string FieldPhoneTooltip => Loc.Admin("cltFieldPhoneTooltip", "Must be unique when provided");
    public string FieldEmailLabel => Loc.Admin("cltFieldEmail", "EMAIL (OPTIONAL)");
    public string FieldNotesLabel => Loc.Admin("cltFieldNotes", "INTERNAL NOTES (OPTIONAL)");
    public string SaveClientLabel => Loc.Admin("cltSaveClient", "Save client");
    public string CancelLabel => Loc.Common("cancel", "Cancel");
    public string SettleDialogTitle => Loc.Admin("cltSettleTitle", "Settle debt");
    public string SettleDialogIntro => Loc.Admin("cltSettleIntro", "Record a payment against this client's open balance. Same admin passcode as order cancel.");
    public string SettleAmountLabel => Loc.Admin("cltSettleAmount", "PAYMENT AMOUNT (USD)");
    public string SettleNoteLabel => Loc.Admin("cltSettleNote", "NOTE (OPTIONAL)");
    public string SettlePasscodeLabel => Loc.Admin("cltSettlePasscode", "ADMIN PASSCODE");
    public string ConfirmPaymentLabel => Loc.Admin("cltConfirmPayment", "Confirm payment");
    public string ProcessingLabel => Loc.Admin("cltProcessing", "Processing…");
    public string ClientMsgBoxTitle => Loc.Admin("cltMsgBoxTitle", "Client");
    public string NewClientDialogTitle => Loc.Admin("cltNewClient", "New client");
    public string EditClientDialogTitle => Loc.Admin("cltEditClient", "Edit client");

    public bool CanEditSelected =>
        IsAdmin && _profile is { Client.IsStaffClient: false };

    public bool CanSettleDebt =>
        _profile is { Client.DebtBalanceUsd: > 0 };

    public bool ShowStaffSectionHint => StaffClients.Count == 0;

    public string StaffSectionHint =>
        Loc.Admin("cltStaffEmptyHint",
            "No staff clients yet. Active employees are mirrored here automatically — open Employees or refresh this page.");

    public bool IsDialogOpen
    {
        get => _isDialogOpen;
        set => SetField(ref _isDialogOpen, value);
    }

    public bool IsSettleDialogOpen
    {
        get => _isSettleDialogOpen;
        set => SetField(ref _isSettleDialogOpen, value);
    }

    public bool IsSettling
    {
        get => _isSettling;
        private set
        {
            if (!SetField(ref _isSettling, value))
                return;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string DialogFullName
    {
        get => _dialogFullName;
        set => SetField(ref _dialogFullName, value);
    }

    public string DialogPhone
    {
        get => _dialogPhone;
        set => SetField(ref _dialogPhone, value);
    }

    public string DialogEmail
    {
        get => _dialogEmail;
        set => SetField(ref _dialogEmail, value);
    }

    public string DialogNotes
    {
        get => _dialogNotes;
        set => SetField(ref _dialogNotes, value);
    }

    public string DialogTitle
    {
        get => _dialogTitle;
        set => SetField(ref _dialogTitle, value);
    }

    public string SettleAmountText
    {
        get => _settleAmountText;
        set => SetField(ref _settleAmountText, value);
    }

    public string SettlePasscode
    {
        get => _settlePasscode;
        set => SetField(ref _settlePasscode, value);
    }

    public string SettleNote
    {
        get => _settleNote;
        set => SetField(ref _settleNote, value);
    }

    public ICommand OpenAddDialogCommand { get; }
    public ICommand OpenEditDialogCommand { get; }
    public ICommand SaveClientCommand { get; }
    public ICommand CancelDialogCommand { get; }
    public ICommand OpenSettleDialogCommand { get; }
    public ICommand ConfirmSettleCommand { get; }
    public ICommand CancelSettleDialogCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ViewOrderCommand { get; }
    public ICommand SelectClientCommand { get; }

    public ClientsViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        OpenAddDialogCommand = new RelayCommand(_ => OpenAddDialog(), _ => IsAdmin);
        OpenEditDialogCommand = new RelayCommand(_ => OpenEditDialog(), _ => CanEditSelected);
        SaveClientCommand = new RelayCommand(_ => _ = SaveClientAsync());
        CancelDialogCommand = new RelayCommand(_ => IsDialogOpen = false);
        OpenSettleDialogCommand = new RelayCommand(_ => OpenSettleDialog(), _ => CanSettleDebt);
        ConfirmSettleCommand = new RelayCommand(
            _ => _ = ConfirmSettleAsync(),
            _ => !IsSettling && CanSettleDebt);
        CancelSettleDialogCommand = new RelayCommand(_ => IsSettleDialogOpen = false);
        RefreshCommand = new RelayCommand(_ => _ = ReloadListAsync());
        ViewOrderCommand = new RelayCommand(p =>
        {
            if (p is ClientOrderListItem order)
                OrderDetail.Load(order.OrderId);
            else if (p is ClientLedgerListItem ledger && ledger.OrderId is int oid)
                OrderDetail.Load(oid);
        });
        SelectClientCommand = new RelayCommand(p =>
        {
            if (p is RestaurantClientListItemDto dto)
                SelectedListItem = dto;
        });
        _ = ReloadListAsync();
    }

    protected override void RefreshLocalizedStrings()
    {
        base.RefreshLocalizedStrings();
        Notify(
            nameof(PageTitle),
            nameof(SearchTooltip),
            nameof(RegularSectionLabel),
            nameof(StaffSectionLabel),
            nameof(StaffListHint),
            nameof(SelectClientPrompt),
            nameof(ProfileTitle),
            nameof(DebtBalanceLabel),
            nameof(SettledRevenueLabel),
            nameof(TotalGeneratedLabel),
            nameof(OrdersLabel),
            nameof(OpenOnAccountLabel),
            nameof(SettleDebtLabel),
            nameof(EditClientLabel),
            nameof(NewClientLabel),
            nameof(DebtLedgerTitle),
            nameof(DebtLedgerSubtitle),
            nameof(OrderHistoryTitle),
            nameof(OrderHistorySubtitle),
            nameof(ViewOrderLabel),
            nameof(DialogIntro),
            nameof(FieldFullNameLabel),
            nameof(FieldFullNameTooltip),
            nameof(FieldPhoneLabel),
            nameof(FieldPhoneTooltip),
            nameof(FieldEmailLabel),
            nameof(FieldNotesLabel),
            nameof(SaveClientLabel),
            nameof(CancelLabel),
            nameof(SettleDialogTitle),
            nameof(SettleDialogIntro),
            nameof(SettleAmountLabel),
            nameof(SettleNoteLabel),
            nameof(SettlePasscodeLabel),
            nameof(ConfirmPaymentLabel),
            nameof(ProcessingLabel),
            nameof(ClientMsgBoxTitle),
            nameof(NewClientDialogTitle),
            nameof(EditClientDialogTitle),
            nameof(StaffSectionHint));
        ClientUiLocalizer.ApplyAll(ProfileOrders);
        ClientUiLocalizer.ApplyAll(ProfileLedger);
        if (_lastListedClientCount is int count)
            StatusMessage = ClientUiLocalizer.FormatClientCount(count);
    }

    private async Task ReloadListAsync()
    {
        try
        {
            IReadOnlyList<RestaurantClientListItemDto>? rows;
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                rows = await _clients.ListAsync();
            }
            else
            {
                var found = await _clients.SearchAsync(SearchText.Trim());
                rows = found?.Select(s => new RestaurantClientListItemDto(
                    s.Id,
                    s.UniqueId,
                    s.FullName,
                    s.PrimaryPhone,
                    string.Empty,
                    s.DebtBalanceUsd,
                    s.IsStaffClient,
                    null,
                    true)).ToList();
            }

            RegularClients.Clear();
            StaffClients.Clear();
            if (rows is null)
            {
                _lastListedClientCount = null;
                StatusMessage = Loc.Admin("cltLoadError", "Could not load clients from the API.");
                return;
            }

            foreach (var c in rows.Where(c => !c.IsStaffClient))
                RegularClients.Add(c);
            foreach (var c in rows.Where(c => c.IsStaffClient))
                StaffClients.Add(c);

            _lastListedClientCount = rows.Count;
            StatusMessage = ClientUiLocalizer.FormatClientCount(rows.Count);
            OnPropertyChanged(nameof(ShowStaffSectionHint));
            OnPropertyChanged(nameof(StaffSectionHint));
            if (SelectedListItem is not null)
            {
                var match = rows.FirstOrDefault(c => c.Id == SelectedListItem.Id);
                SelectedListItem = match;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = FormatApiError(ex);
        }
    }

    private static string FormatApiError(Exception ex)
    {
        var msg = ex.Message ?? "Request failed.";
        const string detailMarker = "DETAIL:";
        var detailIdx = msg.IndexOf(detailMarker, StringComparison.OrdinalIgnoreCase);
        if (detailIdx >= 0)
        {
            var detail = msg[(detailIdx + detailMarker.Length)..].Trim();
            var lineEnd = detail.IndexOf('\n');
            if (lineEnd > 0)
                detail = detail[..lineEnd].Trim();
            if (!string.IsNullOrWhiteSpace(detail))
                return detail;
        }

        var firstLine = msg.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(firstLine))
            return Loc.Admin("cltLoadErrorHint", "Could not load clients. Check that the API is running and try Refresh.");
        return firstLine.Length > 220 ? firstLine[..220] + "…" : firstLine;
    }

    private async Task LoadProfileAsync()
    {
        ProfileOrders.Clear();
        ProfileLedger.Clear();
        _profile = null;
        OnPropertyChanged(nameof(ProfileTitle));
        OnPropertyChanged(nameof(ProfileSubtitle));
        OnPropertyChanged(nameof(ProfileDebtText));
        OnPropertyChanged(nameof(ProfileRevenueText));
        OnPropertyChanged(nameof(ProfileTotalGeneratedText));
        OnPropertyChanged(nameof(ProfileOrderCountText));
        OnPropertyChanged(nameof(ProfileOpenOnAccountText));
        OnPropertyChanged(nameof(CanEditSelected));
        OnPropertyChanged(nameof(CanSettleDebt));

        if (SelectedListItem is null)
            return;

        try
        {
            _profile = await _clients.GetProfileAsync(SelectedListItem.Id);
            if (_profile is null)
            {
                StatusMessage = Loc.Admin("cltProfileNotFound", "Profile not found.");
                return;
            }

            foreach (var o in _profile.Orders)
            {
                var item = new ClientOrderListItem(o);
                ClientUiLocalizer.Apply(item);
                ProfileOrders.Add(item);
            }

            foreach (var e in _profile.Ledger)
            {
                var item = new ClientLedgerListItem(e);
                ClientUiLocalizer.Apply(item);
                ProfileLedger.Add(item);
            }

            OnPropertyChanged(nameof(ProfileTitle));
            OnPropertyChanged(nameof(ProfileSubtitle));
            OnPropertyChanged(nameof(ProfileDebtText));
            OnPropertyChanged(nameof(ProfileRevenueText));
            OnPropertyChanged(nameof(ProfileTotalGeneratedText));
            OnPropertyChanged(nameof(ProfileOrderCountText));
            OnPropertyChanged(nameof(ProfileOpenOnAccountText));
            OnPropertyChanged(nameof(CanEditSelected));
            OnPropertyChanged(nameof(CanSettleDebt));
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void OpenAddDialog()
    {
        _editingClientId = null;
        DialogTitle = NewClientDialogTitle;
        DialogFullName = string.Empty;
        DialogPhone = string.Empty;
        DialogEmail = string.Empty;
        DialogNotes = string.Empty;
        IsDialogOpen = true;
    }

    private void OpenEditDialog()
    {
        if (_profile is null || _profile.Client.IsStaffClient)
            return;
        _editingClientId = _profile.Client.Id;
        DialogTitle = EditClientDialogTitle;
        DialogFullName = _profile.Client.FullName;
        DialogPhone = _profile.Client.PrimaryPhone;
        DialogEmail = _profile.Client.Email;
        DialogNotes = _profile.Client.InternalNotes;
        IsDialogOpen = true;
    }

    private async Task SaveClientAsync()
    {
        try
        {
            if (_editingClientId is int id)
            {
                await _clients.UpdateAsync(
                    id,
                    new UpdateRestaurantClientRequest(DialogFullName, DialogPhone, DialogEmail, DialogNotes, true));
            }
            else
            {
                var (created, createErr) = await _clients.CreateAsync(
                    new CreateRestaurantClientRequest(DialogFullName, DialogPhone, DialogEmail, DialogNotes));
                if (createErr is not null)
                {
                    MessageBox.Show(FormatApiError(new Exception(createErr)), ClientMsgBoxTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (created is null)
                {
                    MessageBox.Show(Loc.Admin("cltClientSavedFail", "Client was not saved."), ClientMsgBoxTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            IsDialogOpen = false;
            await ReloadListAsync();
            if (_editingClientId is int reloadId)
                SelectedListItem = RegularClients.FirstOrDefault(c => c.Id == reloadId)
                    ?? StaffClients.FirstOrDefault(c => c.Id == reloadId);
        }
        catch (Exception ex)
        {
            MessageBox.Show(FormatApiError(ex), ClientMsgBoxTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenSettleDialog()
    {
        if (_profile is null)
            return;
        SettleAmountText = _profile.Client.DebtBalanceUsd.ToString("0.##", CultureInfo.InvariantCulture);
        SettlePasscode = string.Empty;
        SettleNote = string.Empty;
        IsSettleDialogOpen = true;
    }

    private async Task ConfirmSettleAsync()
    {
        if (_profile is null || _isSettling)
            return;
        if (!decimal.TryParse(SettleAmountText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            || amount <= 0m)
        {
            MessageBox.Show(Loc.Admin("cltInvalidAmount", "Enter a valid payment amount."), SettleDialogTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var passcode = (SettlePasscode ?? string.Empty).Trim();
        if (passcode.Length == 0)
        {
            MessageBox.Show(Loc.Admin("cltPasscodeRequired", "Enter the admin passcode to confirm debt settlement."), SettleDialogTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsSettling = true;
        try
        {
            var result = await _clients.SettleDebtAsync(
                _profile.Client.Id,
                new SettleClientDebtRequest(passcode, amount, SettleNote));
            if (result is { Ok: false })
            {
                MessageBox.Show(result.Message ?? Loc.Admin("cltSettlementFailed", "Settlement failed."), SettleDialogTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsSettleDialogOpen = false;
            await ReloadListAsync();
            await LoadProfileAsync();
            StatusMessage = Loc.Admin("cltSettlementApplied", "Applied ${{applied}}; remaining ${{remaining}}",
                new Dictionary<string, string>
                {
                    ["applied"] = (result?.AmountAppliedUsd ?? amount).ToString("N2", CultureInfo.InvariantCulture),
                    ["remaining"] = (result?.RemainingDebtUsd ?? 0m).ToString("N2", CultureInfo.InvariantCulture)
                });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, SettleDialogTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsSettling = false;
        }
    }
}
