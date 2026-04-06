using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using EliteRestaurantPro.Data;
using EliteRestaurantPro.Models;
using EliteRestaurantPro.Utils;
using Microsoft.Win32;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ModelTable = EliteRestaurantPro.Models.Table;

namespace EliteRestaurantPro.ViewModels;

public sealed class CashierQueueRow
{
    public int OrderId { get; init; }
    public string OrderCode { get; init; } = string.Empty;
    public string TableLabel { get; init; } = string.Empty;
    public string ServerName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string CreatedAtText { get; init; } = string.Empty;
    public decimal GrandTotalUsd { get; init; }
    public string GrandTotalText { get; init; } = string.Empty;
    public string LinesSummary { get; init; } = string.Empty;
}

public class AdminOrdersViewModel : AdminBaseViewModel
{
    private const int MaxPastOrdersToDisplay = 250;

    public class PastOrderDayGroup
    {
        public DateTime Day { get; set; }
        public string DayText { get; set; } = string.Empty;
        public bool IsExpanded { get; set; }
        public ObservableCollection<OrderEntry> Orders { get; } = new();
        public int Count => Orders.Count;
    }

    public class TicketLineViewModel
    {
        public int Quantity { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    private int _selectedTableId;
    private string _selectedOrderStatus = "Waiting";
    private bool _isTicketPreviewOpen;
    private string _ticketOrderId = string.Empty;
    private string _ticketStatus = string.Empty;
    private string _ticketTable = string.Empty;
    private string _ticketServer = string.Empty;
    private DateTime _ticketDateTime = DateTime.Now;
    private decimal _ticketSubtotal;
    private decimal _ticketTaxAmount;
    private decimal _ticketServiceAmount;
    private decimal _ticketGrandTotal;
    private decimal _ticketDiscountAmount;
    private string _ticketDiscountLineText = string.Empty;
    private bool _ticketShowDiscount;
    private string _ticketEquivalentFcText = string.Empty;
    private string _ticketPaymentText = string.Empty;
    private string _ticketPaidBreakdownText = string.Empty;
    private string _ticketChangeBreakdownText = string.Empty;
    private string _ticketVerification = string.Empty;
    private string _ticketRestaurantName = "ELITE RESTAURANT PRO";
    private string _ticketTaxLabel = "TVA (7%)";
    private string _ticketServiceLabel = "Service (10%)";
    private bool _isLoading;
    private bool _showPendingCashierSection;
    private bool _isPaymentModalOpen;
    private int _pendingCompleteOrderId;
    private string _pendingCompleteOrderCode = string.Empty;
    private string _paymentMode = "USD";
    private string _paidUsdInput = string.Empty;
    private string _paidFcInput = string.Empty;
    private decimal _paymentDueUsd;
    private decimal _paymentDueFc;
    private bool _isChangeModalOpen;
    private string _changeUsdInput = string.Empty;
    private string _changeFcInput = string.Empty;
    private string _numpadTarget = "PaidUsd";

    public override string ActivePage => "Orders";

    public ObservableCollection<CashierQueueRow> PendingCashierOrders { get; } = new();

    public bool ShowPendingCashierSection
    {
        get => _showPendingCashierSection;
        private set => SetField(ref _showPendingCashierSection, value);
    }

    public string PendingCashierSectionTitle =>
        $"Awaiting cashier validation ({PendingCashierOrders.Count})";

    public ObservableCollection<OrderEntry> ActiveOrders { get; } = new();
    public ObservableCollection<OrderEntry> PastOrders { get; } = new();
    public ObservableCollection<PastOrderDayGroup> PastOrderDayGroups { get; } = new();
    public ObservableCollection<ModelTable> AvailableTables { get; } = new();
    public ObservableCollection<string> OrderStatuses { get; } =
        new(["Waiting", "In Kitchen", "Ready"]);
    public ObservableCollection<ProductSelectionItemViewModel> ProductSelections { get; } = new();
    public ObservableCollection<TicketLineViewModel> TicketLines { get; } = new();

    public int SelectedTableId
    {
        get => _selectedTableId;
        set => SetField(ref _selectedTableId, value);
    }

    public string SelectedOrderStatus
    {
        get => _selectedOrderStatus;
        set => SetField(ref _selectedOrderStatus, value);
    }

    public bool IsTicketPreviewOpen
    {
        get => _isTicketPreviewOpen;
        set => SetField(ref _isTicketPreviewOpen, value);
    }

    public string TicketOrderId
    {
        get => _ticketOrderId;
        set => SetField(ref _ticketOrderId, value);
    }

    public string TicketStatus
    {
        get => _ticketStatus;
        set => SetField(ref _ticketStatus, value);
    }

    public string TicketTable
    {
        get => _ticketTable;
        set => SetField(ref _ticketTable, value);
    }

    public string TicketServer
    {
        get => _ticketServer;
        set => SetField(ref _ticketServer, value);
    }

    public DateTime TicketDateTime
    {
        get => _ticketDateTime;
        set => SetField(ref _ticketDateTime, value);
    }

    public decimal TicketSubtotal
    {
        get => _ticketSubtotal;
        set => SetField(ref _ticketSubtotal, value);
    }

    public decimal TicketTaxAmount
    {
        get => _ticketTaxAmount;
        set => SetField(ref _ticketTaxAmount, value);
    }

    public decimal TicketServiceAmount
    {
        get => _ticketServiceAmount;
        set => SetField(ref _ticketServiceAmount, value);
    }

    public decimal TicketGrandTotal
    {
        get => _ticketGrandTotal;
        set => SetField(ref _ticketGrandTotal, value);
    }

    public decimal TicketDiscountAmount
    {
        get => _ticketDiscountAmount;
        set => SetField(ref _ticketDiscountAmount, value);
    }

    public string TicketDiscountLineText
    {
        get => _ticketDiscountLineText;
        set => SetField(ref _ticketDiscountLineText, value);
    }

    public bool TicketShowDiscount
    {
        get => _ticketShowDiscount;
        set => SetField(ref _ticketShowDiscount, value);
    }

    public string TicketEquivalentFcText
    {
        get => _ticketEquivalentFcText;
        set => SetField(ref _ticketEquivalentFcText, value);
    }

    public string TicketPaymentText
    {
        get => _ticketPaymentText;
        set => SetField(ref _ticketPaymentText, value);
    }

    public string TicketPaidBreakdownText
    {
        get => _ticketPaidBreakdownText;
        set => SetField(ref _ticketPaidBreakdownText, value);
    }

    public string TicketChangeBreakdownText
    {
        get => _ticketChangeBreakdownText;
        set => SetField(ref _ticketChangeBreakdownText, value);
    }

    public string TicketVerification
    {
        get => _ticketVerification;
        set => SetField(ref _ticketVerification, value);
    }

    public string TicketRestaurantName
    {
        get => _ticketRestaurantName;
        set => SetField(ref _ticketRestaurantName, value);
    }

    public string TicketTaxLabel
    {
        get => _ticketTaxLabel;
        set => SetField(ref _ticketTaxLabel, value);
    }

    public string TicketServiceLabel
    {
        get => _ticketServiceLabel;
        set => SetField(ref _ticketServiceLabel, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public bool IsPaymentModalOpen
    {
        get => _isPaymentModalOpen;
        private set => SetField(ref _isPaymentModalOpen, value);
    }

    public string PendingCompleteOrderCode
    {
        get => _pendingCompleteOrderCode;
        set => SetField(ref _pendingCompleteOrderCode, value);
    }

    public string PaymentMode
    {
        get => _paymentMode;
        set
        {
            if (!SetField(ref _paymentMode, value))
                return;
            if (value == "USD")
                PaidFcInput = string.Empty;
            else if (value == "FC")
                PaidUsdInput = string.Empty;
            OnPaymentInputsChanged();
        }
    }

    public string NumpadTarget
    {
        get => _numpadTarget;
        set => SetField(ref _numpadTarget, value);
    }

    public string PaidUsdInput
    {
        get => _paidUsdInput;
        set
        {
            if (!SetField(ref _paidUsdInput, value))
                return;
            OnPaymentInputsChanged();
        }
    }

    public string PaidFcInput
    {
        get => _paidFcInput;
        set
        {
            if (!SetField(ref _paidFcInput, value))
                return;
            OnPaymentInputsChanged();
        }
    }

    public decimal PaymentDueUsd
    {
        get => _paymentDueUsd;
        private set => SetField(ref _paymentDueUsd, value);
    }

    public decimal PaymentDueFc
    {
        get => _paymentDueFc;
        private set => SetField(ref _paymentDueFc, value);
    }

    public bool IsChangeModalOpen
    {
        get => _isChangeModalOpen;
        private set => SetField(ref _isChangeModalOpen, value);
    }

    public string ChangeUsdInput
    {
        get => _changeUsdInput;
        set
        {
            if (!SetField(ref _changeUsdInput, value))
                return;
            OnPaymentInputsChanged();
        }
    }

    public string ChangeFcInput
    {
        get => _changeFcInput;
        set
        {
            if (!SetField(ref _changeFcInput, value))
                return;
            OnPaymentInputsChanged();
        }
    }

    public bool CanEditPaidUsd => true;
    public bool CanEditPaidFc => true;
    public decimal PaidUsd => ParseAmount(PaidUsdInput);
    public decimal PaidFc => ParseAmount(PaidFcInput);
    public decimal PaidFcInUsd => CurrencyHelper.ConvertFcToUsd(PaidFc);
    public decimal TotalPaidUsdEquivalent => Math.Round(PaidUsd + PaidFcInUsd, 2);
    public decimal RemainingUsd => Math.Max(0m, Math.Round(PaymentDueUsd - TotalPaidUsdEquivalent, 2));
    public decimal ChangeUsd => Math.Max(0m, Math.Round(TotalPaidUsdEquivalent - PaymentDueUsd, 2));
    public decimal RemainingUsdInFc => CurrencyHelper.ConvertUsdToFc(RemainingUsd);
    public decimal ChangeUsdInFc => CurrencyHelper.ConvertUsdToFc(ChangeUsd);
    public decimal RemainingFc => Math.Max(0m, Math.Round(PaymentDueFc - PaidFc - CurrencyHelper.ConvertUsdToFc(PaidUsd), 2));
    public decimal ChangeFc => Math.Max(0m, Math.Round(PaidFc + CurrencyHelper.ConvertUsdToFc(PaidUsd) - PaymentDueFc, 2));
    public bool CanConfirmPayment => IsPaymentModalOpen && RemainingUsd <= 0m && (PaidUsd > 0m || PaidFc > 0m);
    public decimal ChangeAllocationUsd => ParseAmount(ChangeUsdInput);
    public decimal ChangeAllocationFc => ParseAmount(ChangeFcInput);
    public decimal ChangeAllocationUsdEquivalent => Math.Round(ChangeAllocationUsd + CurrencyHelper.ConvertFcToUsd(ChangeAllocationFc), 2);
    public decimal RemainingChangeUsdToAllocate => Math.Max(0m, Math.Round(ChangeUsd - ChangeAllocationUsdEquivalent, 2));
    public decimal RemainingChangeFcToAllocate => CurrencyHelper.ConvertUsdToFc(RemainingChangeUsdToAllocate);
    public bool CanConfirmChange => IsChangeModalOpen && Math.Abs(ChangeAllocationUsdEquivalent - ChangeUsd) <= 0.01m;
    public string PaymentSummaryLine =>
        $"Due: {CurrencyHelper.FormatAmount(PaymentDueUsd, CurrencyHelper.Usd)} | Paid(eq): {CurrencyHelper.FormatAmount(TotalPaidUsdEquivalent, CurrencyHelper.Usd)} | Remaining: {CurrencyHelper.FormatAmount(RemainingUsd, CurrencyHelper.Usd)}";

    public ICommand CreateOrderCommand { get; }
    public ICommand ReleasePendingCashierCommand { get; }
    public ICommand CancelPendingCashierCommand { get; }
    public ICommand AdvanceOrderCommand { get; }
    public ICommand CompleteOrderCommand { get; }
    public ICommand CancelOrderCommand { get; }
    public ICommand PrintTicketCommand { get; }
    public ICommand CloseTicketPreviewCommand { get; }
    public ICommand ExportTicketPdfCommand { get; }
    public ICommand ExportClientTicketPdfCommand { get; }
    public ICommand ViewOrderCommand { get; }
    public ICommand ConfirmPaymentCommand { get; }
    public ICommand CancelPaymentCommand { get; }
    public ICommand GoToChangeCommand { get; }
    public ICommand ConfirmChangeCommand { get; }
    public ICommand CancelChangeCommand { get; }
    public ICommand SelectNumpadTargetCommand { get; }
    public ICommand NumpadDigitCommand { get; }
    public ICommand NumpadDotCommand { get; }
    public ICommand NumpadBackspaceCommand { get; }
    public ICommand NumpadClearCommand { get; }

    public OrderDetailPanelViewModel OrderDetail { get; } = new();

    public AdminOrdersViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        CreateOrderCommand = new RelayCommand(_ => CreateOrder());
        ReleasePendingCashierCommand = new RelayCommand(p =>
        {
            if (p is CashierQueueRow row)
                ReleasePendingToKitchen(row);
        });
        CancelPendingCashierCommand = new RelayCommand(p =>
        {
            if (p is CashierQueueRow row)
                CancelPendingCashier(row);
        });
        AdvanceOrderCommand = new RelayCommand(order => AdvanceOrder(order as OrderEntry));
        CompleteOrderCommand = new RelayCommand(order => CompleteOrder(order as OrderEntry));
        CancelOrderCommand = new RelayCommand(order => CancelOrder(order as OrderEntry));
        PrintTicketCommand = new RelayCommand(order => OpenTicketPreview(order as OrderEntry));
        CloseTicketPreviewCommand = new RelayCommand(_ => IsTicketPreviewOpen = false);
        ExportTicketPdfCommand = new RelayCommand(_ => ExportTicketPdf());
        ExportClientTicketPdfCommand = new RelayCommand(_ => ExportClientTicketPdf());
        ViewOrderCommand = new RelayCommand(p =>
        {
            var id = p switch
            {
                OrderEntry e => e.Id,
                CashierQueueRow r => r.OrderId,
                _ => 0
            };
            if (id > 0)
                OrderDetail.Load(id);
        });
        ConfirmPaymentCommand = new RelayCommand(_ => ConfirmCompletePayment(), _ => CanConfirmPayment);
        CancelPaymentCommand = new RelayCommand(_ => ClosePaymentModal());
        GoToChangeCommand = new RelayCommand(_ => OpenChangeModal(), _ => CanConfirmPayment);
        ConfirmChangeCommand = new RelayCommand(_ => ConfirmChangeAndComplete(), _ => CanConfirmChange);
        CancelChangeCommand = new RelayCommand(_ => CloseChangeModal());
        SelectNumpadTargetCommand = new RelayCommand(target => SetNumpadTarget(target as string));
        NumpadDigitCommand = new RelayCommand(digit => AppendNumpadDigit(digit as string));
        NumpadDotCommand = new RelayCommand(_ => AppendNumpadDot());
        NumpadBackspaceCommand = new RelayCommand(_ => BackspaceNumpad());
        NumpadClearCommand = new RelayCommand(_ => ClearNumpadTarget());

        _ = LoadOrdersAsync();
    }

    private static decimal ParseAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0m;
        var t = text.Trim();
        if (decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var inv))
            return Math.Max(0m, inv);
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.CurrentCulture, out var cur) ? Math.Max(0m, cur) : 0m;
    }

    private void SetNumpadTarget(string? target)
    {
        if (target is "PaidUsd" or "PaidFc" or "ChangeUsd" or "ChangeFc")
            NumpadTarget = target;
    }

    private string GetNumpadTargetText() => NumpadTarget switch
    {
        "PaidUsd" => PaidUsdInput,
        "PaidFc" => PaidFcInput,
        "ChangeUsd" => ChangeUsdInput,
        "ChangeFc" => ChangeFcInput,
        _ => string.Empty
    };

    private void SetNumpadTargetText(string value)
    {
        switch (NumpadTarget)
        {
            case "PaidUsd":
                PaidUsdInput = value;
                break;
            case "PaidFc":
                PaidFcInput = value;
                break;
            case "ChangeUsd":
                ChangeUsdInput = value;
                break;
            case "ChangeFc":
                ChangeFcInput = value;
                break;
        }
    }

    private void AppendNumpadDigit(string? digit)
    {
        if (string.IsNullOrWhiteSpace(digit))
            return;
        var current = GetNumpadTargetText();
        SetNumpadTargetText(current + digit.Trim());
    }

    private void AppendNumpadDot()
    {
        var current = GetNumpadTargetText();
        if (current.Contains('.'))
            return;
        SetNumpadTargetText(string.IsNullOrWhiteSpace(current) ? "0." : current + ".");
    }

    private void BackspaceNumpad()
    {
        var current = GetNumpadTargetText();
        if (string.IsNullOrEmpty(current))
            return;
        SetNumpadTargetText(current[..^1]);
    }

    private void ClearNumpadTarget() => SetNumpadTargetText(string.Empty);

    private void OnPaymentInputsChanged()
    {
        OnPropertyChanged(nameof(CanEditPaidUsd));
        OnPropertyChanged(nameof(CanEditPaidFc));
        OnPropertyChanged(nameof(PaidUsd));
        OnPropertyChanged(nameof(PaidFc));
        OnPropertyChanged(nameof(PaidFcInUsd));
        OnPropertyChanged(nameof(TotalPaidUsdEquivalent));
        OnPropertyChanged(nameof(RemainingUsd));
        OnPropertyChanged(nameof(ChangeUsd));
        OnPropertyChanged(nameof(RemainingUsdInFc));
        OnPropertyChanged(nameof(ChangeUsdInFc));
        OnPropertyChanged(nameof(RemainingFc));
        OnPropertyChanged(nameof(ChangeFc));
        OnPropertyChanged(nameof(CanConfirmPayment));
        OnPropertyChanged(nameof(PaymentSummaryLine));
        OnPropertyChanged(nameof(ChangeAllocationUsd));
        OnPropertyChanged(nameof(ChangeAllocationFc));
        OnPropertyChanged(nameof(ChangeAllocationUsdEquivalent));
        OnPropertyChanged(nameof(RemainingChangeUsdToAllocate));
        OnPropertyChanged(nameof(RemainingChangeFcToAllocate));
        OnPropertyChanged(nameof(CanConfirmChange));
    }

    private void OpenPaymentModal(OrderEntry entry)
    {
        _pendingCompleteOrderId = entry.Id;
        PendingCompleteOrderCode = entry.OrderId;
        PaymentDueUsd = Math.Round(entry.Total, 2);
        PaymentDueFc = CurrencyHelper.ConvertUsdToFc(PaymentDueUsd);
        PaymentMode = "MIXED";
        NumpadTarget = "PaidUsd";
        PaidUsdInput = string.Empty;
        PaidFcInput = string.Empty;
        ChangeUsdInput = string.Empty;
        ChangeFcInput = string.Empty;
        IsChangeModalOpen = false;
        IsPaymentModalOpen = true;
        OnPaymentInputsChanged();
    }

    private void ClosePaymentModal()
    {
        IsPaymentModalOpen = false;
        IsChangeModalOpen = false;
        _pendingCompleteOrderId = 0;
        PendingCompleteOrderCode = string.Empty;
        PaidUsdInput = string.Empty;
        PaidFcInput = string.Empty;
        ChangeUsdInput = string.Empty;
        ChangeFcInput = string.Empty;
        NumpadTarget = "PaidUsd";
        OnPaymentInputsChanged();
    }

    private void ConfirmCompletePayment()
    {
        if (!CanConfirmPayment || _pendingCompleteOrderId <= 0)
            return;

        OpenChangeModal();
    }

    private void OpenChangeModal()
    {
        if (!CanConfirmPayment)
            return;

        var suggestedUsd = ChangeUsd;
        ChangeUsdInput = suggestedUsd <= 0m ? string.Empty : suggestedUsd.ToString("0.##", CultureInfo.InvariantCulture);
        ChangeFcInput = string.Empty;
        NumpadTarget = "ChangeUsd";
        IsChangeModalOpen = true;
    }

    private void CloseChangeModal()
    {
        IsChangeModalOpen = false;
        ChangeUsdInput = string.Empty;
        ChangeFcInput = string.Empty;
        NumpadTarget = "PaidUsd";
        OnPaymentInputsChanged();
    }

    private void ConfirmChangeAndComplete()
    {
        if (!CanConfirmChange || _pendingCompleteOrderId <= 0)
            return;

        var entry = ActiveOrders.FirstOrDefault(o => o.Id == _pendingCompleteOrderId);
        if (entry is null)
        {
            ClosePaymentModal();
            _ = LoadOrdersAsync();
            return;
        }

        var paymentCurrencyCode = "MIXED";

        UpdateOrderStatus(
            entry,
            "Completed",
            paymentCurrencyCode,
            PaidUsd,
            PaidFc,
            ChangeAllocationUsd,
            ChangeAllocationFc);
        ClosePaymentModal();
    }

    private sealed class OrdersSnapshot
    {
        public List<CashierQueueRow> PendingCashier { get; init; } = [];
        public List<OrderEntry> ActiveOrders { get; init; } = [];
        public List<OrderEntry> PastOrders { get; init; } = [];
        public List<ModelTable> AvailableTables { get; init; } = [];
        public List<ProductSelectionItemViewModel> ProductSelections { get; init; } = [];
    }

    private async Task LoadOrdersAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        try
        {
            var showAdminAdvance = ShowFullAdminNav;
            var canViewTicket = ShowFullAdminNav || AppSession.IsCashierTablet;
            var snapshot = await Task.Run(() =>
            {
                using var db = new AppDbContext();

                var activeOrders = db.Orders
                    .AsNoTracking()
                    .Include(o => o.Table)
                    .Include(o => o.Server)
                    .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                    .Where(o => o.Status == "Waiting" || o.Status == "In Kitchen" || o.Status == "Ready" ||
                                o.Status == OrderWorkflow.Served)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToList()
                    .Select(o => MapOrder(o, false, showAdminAdvance, canViewTicket))
                    .ToList();

                var pastOrders = db.Orders
                    .AsNoTracking()
                    .Include(o => o.Table)
                    .Include(o => o.Server)
                    .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                    .Where(o => o.Status == "Completed" || o.Status == "Cancelled")
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(MaxPastOrdersToDisplay)
                    .ToList()
                    .Select(o => MapOrder(o, true, showAdminAdvance, canViewTicket))
                    .ToList();

                var tables = db.Tables
                    .AsNoTracking()
                    .Include(t => t.AssignedServer)
                    .Where(t => t.Status == "Available" && t.AssignedServerId != null)
                    .OrderBy(t => t.TableNumber)
                    .ToList();

                var products = db.Products
                    .AsNoTracking()
                    .OrderBy(p => p.Category)
                    .ThenBy(p => p.Name)
                    .Select(product => new ProductSelectionItemViewModel
                    {
                        ProductId = product.Id,
                        Name = product.Name,
                        Category = product.Category,
                        Price = product.Price
                    })
                    .ToList();

                var pendingOrders = db.Orders
                    .AsNoTracking()
                    .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                    .Where(o => o.Status == OrderWorkflow.PendingCashier)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToList();

                var pendingRows = new List<CashierQueueRow>();
                foreach (var o in pendingOrders)
                {
                    var subtotal = o.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
                    var totals = OrderTotalsHelper.ComputeTotals(subtotal, o.DiscountMode, o.DiscountValue);
                    var lines = string.Join(", ",
                        o.Items.Select(i =>
                            $"{i.Product?.Name ?? "Item"} x{i.Quantity}"));
                    pendingRows.Add(new CashierQueueRow
                    {
                        OrderId = o.Id,
                        OrderCode = string.IsNullOrWhiteSpace(o.UniqueId) ? $"#{o.Id:000}" : o.UniqueId,
                        TableLabel = $"{o.TableCode} · {o.TableName}".Trim(' ', '·'),
                        ServerName = o.ServerName,
                        CreatedAt = o.CreatedAt,
                        CreatedAtText = o.CreatedAt.ToString("MMM d, yyyy · HH:mm"),
                        GrandTotalUsd = totals.GrandTotal,
                        GrandTotalText = $"$ {totals.GrandTotal:N2}",
                        LinesSummary = string.IsNullOrWhiteSpace(lines) ? "No lines" : lines
                    });
                }

                return new OrdersSnapshot
                {
                    PendingCashier = pendingRows,
                    ActiveOrders = activeOrders,
                    PastOrders = pastOrders,
                    AvailableTables = tables,
                    ProductSelections = products
                };
            });

            PendingCashierOrders.Clear();
            foreach (var row in snapshot.PendingCashier)
                PendingCashierOrders.Add(row);
            ShowPendingCashierSection = PendingCashierOrders.Count > 0;
            OnPropertyChanged(nameof(PendingCashierSectionTitle));

            ActiveOrders.Clear();
            PastOrders.Clear();
            PastOrderDayGroups.Clear();
            AvailableTables.Clear();
            ProductSelections.Clear();

            foreach (var order in snapshot.ActiveOrders)
                ActiveOrders.Add(order);

            foreach (var order in snapshot.PastOrders)
                PastOrders.Add(order);

            foreach (var group in snapshot.PastOrders
                         .GroupBy(o => o.CreatedAt.Date)
                         .OrderByDescending(g => g.Key))
            {
                var dayGroup = new PastOrderDayGroup
                {
                    Day = group.Key,
                    DayText = group.Key == DateTime.Today
                        ? $"Today - {group.Key:dddd, MMM dd yyyy}"
                        : group.Key.ToString("dddd, MMM dd yyyy"),
                    IsExpanded = group.Key == DateTime.Today
                };

                foreach (var order in group.OrderByDescending(o => o.CreatedAt))
                    dayGroup.Orders.Add(order);

                PastOrderDayGroups.Add(dayGroup);
            }

            foreach (var table in snapshot.AvailableTables)
                AvailableTables.Add(table);

            foreach (var product in snapshot.ProductSelections)
                ProductSelections.Add(product);

            SelectedTableId = AvailableTables.FirstOrDefault()?.Id ?? 0;
            SelectedOrderStatus = OrderStatuses.First();
            RefreshReadyPickupBanner();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Orders failed to load:\n{ex.Message}",
                "Orders",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ReleasePendingToKitchen(CashierQueueRow? row)
    {
        if (row is null)
            return;

        var confirm = MessageBox.Show(
            $"Release order {row.OrderCode} to the kitchen?\n\nInventory will be deducted and the ticket will appear below as Waiting.",
            "Send to kitchen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        using var db = new AppDbContext();
        var order = db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefault(o => o.Id == row.OrderId && o.Status == OrderWorkflow.PendingCashier);
        if (order is null)
        {
            MessageBox.Show("Order not found or already processed.", "Orders", MessageBoxButton.OK,
                MessageBoxImage.Information);
            _ = LoadOrdersAsync();
            return;
        }

        var err = OrderInventoryDeduction.TryApplyForPlacedOrder(db, order);
        if (err is not null)
        {
            MessageBox.Show(err, "Cannot release", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        order.Status = "Waiting";
        db.SaveChanges();
        AppDbContext.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();

        MessageBox.Show($"Order {order.UniqueId} sent to the kitchen.", "Orders", MessageBoxButton.OK,
            MessageBoxImage.Information);
        _ = LoadOrdersAsync();
    }

    private void CancelPendingCashier(CashierQueueRow? row)
    {
        if (row is null)
            return;

        var confirm = MessageBox.Show(
            $"Cancel order {row.OrderCode}? Nothing was deducted from stock yet.",
            "Cancel order",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        using var db = new AppDbContext();
        var order = db.Orders.FirstOrDefault(o => o.Id == row.OrderId && o.Status == OrderWorkflow.PendingCashier);
        if (order is null)
        {
            MessageBox.Show("Order not found or already processed.", "Orders", MessageBoxButton.OK,
                MessageBoxImage.Information);
            _ = LoadOrdersAsync();
            return;
        }

        order.Status = "Cancelled";
        db.SaveChanges();
        AppDbContext.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();
        _ = LoadOrdersAsync();
    }

    private void CreateOrder()
    {
        var selectedProducts = ProductSelections.Where(p => p.IsSelected).ToList();
        if (SelectedTableId == 0 || !selectedProducts.Any())
        {
            return;
        }

        using var db = new AppDbContext();
        var table = db.Tables.Include(t => t.AssignedServer).SingleOrDefault(t => t.Id == SelectedTableId);
        if (table is null)
        {
            return;
        }

        // Business rule: one server per table, order inherits that table's server
        if (table.AssignedServerId is null || table.AssignedServer is null)
        {
            return;
        }

        var confirmAdd = MessageBox.Show(
            $"Create order for Table {table.TableNumber} ({table.Name}) assigned to {table.AssignedServer.Name}?",
            "Confirm Create Order",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmAdd != MessageBoxResult.Yes)
            return;

        var subtotal = selectedProducts.Sum(p => p.LineTotal);
        var totals = OrderTotalsHelper.ComputeTotals(subtotal, "None", 0m);
        var grandTotalUsd = totals.GrandTotal;

        var order = new OrderRecord
        {
            UniqueId = UniqueIdGenerator.NewId("ORD"),
            TableId = table.Id,
            TableCode = $"Table {table.TableNumber}",
            TableName = string.IsNullOrWhiteSpace(table.Name) ? $"Table {table.TableNumber}" : table.Name,
            ServerId = table.AssignedServerId,
            ServerName = table.AssignedServer.Name,
            Status = SelectedOrderStatus,
            PaymentCurrencyCode = CurrencyHelper.Usd,
            PaymentAmount = Math.Round(grandTotalUsd, 2),
            PaymentAmountUsd = Math.Round(grandTotalUsd, 2),
            PaymentAmountFc = CurrencyHelper.ConvertUsdToFc(grandTotalUsd),
            ExchangeRateUsed = CurrencyHelper.FcPerUsd,
            CreatedAt = DateTime.Now
        };

        var activeStaff = db.Employees
            .AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active")
            .ToList();
        var productById = db.Products
            .AsNoTracking()
            .Where(p => selectedProducts.Select(s => s.ProductId).Contains(p.Id))
            .ToDictionary(p => p.Id, p => p);

        (int? EmployeeId, string Role, string Name) ResolvePreparationAssignee(int productId)
        {
            if (!productById.TryGetValue(productId, out var product))
                return (null, "Unknown", "Unassigned");

            var isDrink = string.Equals(product.Category, "Drink", StringComparison.OrdinalIgnoreCase);
            if (isDrink)
            {
                var barman = activeStaff.FirstOrDefault(e =>
                    e.Role.Equals("Barman", StringComparison.OrdinalIgnoreCase) ||
                    e.Role.Equals("Bartender", StringComparison.OrdinalIgnoreCase));
                return barman is null ? (null, "Barman", "Unassigned Barman") : (barman.Id, "Barman", barman.Name);
            }

            var chef = activeStaff.FirstOrDefault(e =>
                e.Role.Equals("Chef", StringComparison.OrdinalIgnoreCase));
            return chef is null ? (null, "Chef", "Unassigned Chef") : (chef.Id, "Chef", chef.Name);
        }

        foreach (var selection in selectedProducts)
        {
            var assignee = ResolvePreparationAssignee(selection.ProductId);
            order.Items.Add(new OrderItem
            {
                ProductId = selection.ProductId,
                Quantity = selection.Quantity,
                PreparedByEmployeeId = assignee.EmployeeId,
                PreparedByRole = assignee.Role,
                PreparedByName = assignee.Name
            });
        }

        var productIds = selectedProducts.Select(p => p.ProductId).Distinct().ToList();
        var ingredientRows = db.ProductIngredients
            .Include(pi => pi.InventoryItem)
            .Where(pi => productIds.Contains(pi.ProductId))
            .ToList();

        var requiredByInventory = new Dictionary<int, decimal>();
        var requiredByInventoryAndAssignee = new Dictionary<(int InventoryItemId, int? EmployeeId, string Role, string Name), decimal>();
        foreach (var selection in selectedProducts)
        {
            var assignee = ResolvePreparationAssignee(selection.ProductId);
            foreach (var ingredient in ingredientRows.Where(i => i.ProductId == selection.ProductId))
            {
                var required = ingredient.Quantity * selection.Quantity;
                if (!requiredByInventory.TryAdd(ingredient.InventoryItemId, required))
                    requiredByInventory[ingredient.InventoryItemId] += required;

                var actorKey = (ingredient.InventoryItemId, assignee.EmployeeId, assignee.Role, assignee.Name);
                if (!requiredByInventoryAndAssignee.TryAdd(actorKey, required))
                    requiredByInventoryAndAssignee[actorKey] += required;
            }
        }

        var insufficient = ingredientRows
            .Where(i => i.InventoryItem != null && requiredByInventory.TryGetValue(i.InventoryItemId, out var req) && i.InventoryItem.StockQuantity < req)
            .Select(i => $"{i.InventoryItem!.Name} (need {requiredByInventory[i.InventoryItemId]:0.##} {i.InventoryItem.Unit}, have {i.InventoryItem.StockQuantity:0.##})")
            .Distinct()
            .ToList();

        if (insufficient.Any())
        {
            MessageBox.Show(
                "Not enough inventory for this order:\n\n" + string.Join("\n", insufficient),
                "Insufficient Inventory",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        foreach (var (inventoryItemId, required) in requiredByInventory)
        {
            var inventoryItem = ingredientRows
                .Select(i => i.InventoryItem)
                .FirstOrDefault(i => i != null && i.Id == inventoryItemId);
            if (inventoryItem is null) continue;
            inventoryItem.StockQuantity -= required;

            var actorNotes = requiredByInventoryAndAssignee
                .Where(x => x.Key.InventoryItemId == inventoryItemId)
                .Select(x => $"{x.Key.Role} {x.Key.Name}: {x.Value:0.##}")
                .ToList();
            var actorText = actorNotes.Count == 0 ? "Unassigned" : string.Join(", ", actorNotes);
            var deductionNote =
                $"{DateTime.Now:yyyy-MM-dd HH:mm} - {required:0.##} {inventoryItem.Name} deducted from order {order.UniqueId}. Used by {actorText}.";
            inventoryItem.Notes = string.IsNullOrWhiteSpace(inventoryItem.Notes)
                ? deductionNote
                : $"{inventoryItem.Notes}\n{deductionNote}";
        }

        db.Orders.Add(order);
        table.Status = "Occupied";
        db.SaveChanges();

        foreach (var selection in ProductSelections)
        {
            selection.IsSelected = false;
            selection.Quantity = 1;
        }

        _ = LoadOrdersAsync();
    }

    private void AdvanceOrder(OrderEntry? entry)
    {
        if (entry is null) return;

        if (!ShowFullAdminNav)
            return;

        using var db = new AppDbContext();
        var order = db.Orders.SingleOrDefault(o => o.Id == entry.Id);
        if (order is null) return;

        if (!OrderWorkflow.CanAdminAdvanceOrderStatus(order.Status))
        {
            MessageBox.Show(
                "Advance is not available for this status.",
                "Orders",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        order.Status = order.Status switch
        {
            "Waiting" => "In Kitchen",
            "In Kitchen" => "Ready",
            "Ready" => OrderWorkflow.Served,
            _ => order.Status
        };

        db.SaveChanges();
        AppDbContext.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();
        _ = LoadOrdersAsync();
    }

    private void CompleteOrder(OrderEntry? entry)
    {
        if (entry is null)
            return;

        if (!OrderWorkflow.CanCashierComplete(entry.Status))
        {
            MessageBox.Show(
                "Complete is only available when the order is Served.\n\nFlow: kitchen marks Ready → server uses Pick up & serve (or admin uses Advance on Ready) → Served → then complete payment.",
                "Orders",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        OpenPaymentModal(entry);
    }

    private void CancelOrder(OrderEntry? entry)
    {
        if (entry is null) return;

        var confirmDelete = MessageBox.Show(
            $"Cancel order {entry.OrderId}?",
            "Confirm Cancel Order",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmDelete != MessageBoxResult.Yes)
            return;

        UpdateOrderStatus(entry, "Cancelled");
    }

    private void UpdateOrderStatus(
        OrderEntry? entry,
        string status,
        string? paymentCurrencyOverride = null,
        decimal paidUsd = 0m,
        decimal paidFc = 0m,
        decimal changeGivenUsd = 0m,
        decimal changeGivenFc = 0m)
    {
        if (entry is null) return;

        using var db = new AppDbContext();
        var order = db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .SingleOrDefault(o => o.Id == entry.Id);
        if (order is null) return;

        var previousStatus = order.Status;
        if (status == "Completed")
        {
            var lineSubtotal = order.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
            var totals = OrderTotalsHelper.ComputeTotals(lineSubtotal, order.DiscountMode, order.DiscountValue);
            var grandTotalUsd = totals.GrandTotal;
            var paidUsdRounded = Math.Round(Math.Max(0m, paidUsd), 2);
            var paidFcRounded = Math.Round(Math.Max(0m, paidFc), 2);
            var changeUsdRounded = Math.Round(Math.Max(0m, changeGivenUsd), 2);
            var changeFcRounded = Math.Round(Math.Max(0m, changeGivenFc), 2);
            var changeUsdEquivalent = Math.Round(changeUsdRounded + CurrencyHelper.ConvertFcToUsd(changeFcRounded), 2);

            var paymentCurrency = string.IsNullOrWhiteSpace(paymentCurrencyOverride)
                ? (string.IsNullOrWhiteSpace(order.PaymentCurrencyCode) ? CurrencyHelper.Usd : order.PaymentCurrencyCode)
                : paymentCurrencyOverride;

            order.PaymentCurrencyCode = paymentCurrency;
            order.ExchangeRateUsed = CurrencyHelper.FcPerUsd;
            // Revenue posting must remain the amount owed, not the amount handed by client.
            order.PaymentAmount = paymentCurrency == CurrencyHelper.CongoleseFranc
                ? CurrencyHelper.ConvertUsdToFc(grandTotalUsd)
                : Math.Round(grandTotalUsd, 2);
            order.PaymentAmountUsd = Math.Round(grandTotalUsd, 2);
            order.PaymentAmountFc = CurrencyHelper.ConvertUsdToFc(grandTotalUsd);
            order.CustomerPaidUsd = paidUsdRounded;
            order.CustomerPaidFc = paidFcRounded;
            order.ChangeGivenUsd = changeUsdRounded;
            order.ChangeGivenFc = changeFcRounded;
            if (!string.Equals(previousStatus, "Completed", StringComparison.OrdinalIgnoreCase))
                order.CompletedAt = DateTime.Now;

            var expectedChangeUsd = Math.Max(0m, Math.Round((paidUsdRounded + CurrencyHelper.ConvertFcToUsd(paidFcRounded)) - grandTotalUsd, 2));
            if (Math.Abs(expectedChangeUsd - changeUsdEquivalent) > 0.02m)
                throw new InvalidOperationException("Change allocation does not match expected change amount.");
        }

        order.Status = status;
        // Persist the order before recording revenue: RecordCompletedOrderRevenue re-queries the DB
        // with AsNoTracking and requires Status == "Completed"; otherwise no sale row is written.
        db.SaveChanges();

        if (status == "Completed" && previousStatus != "Completed")
        {
            FinancialTransactionService.RecordCompletedOrderRevenue(db, order.Id);
            RecordChangeExpense(db, order);
            db.SaveChanges();
        }

        RefreshTableStatus(db, order.TableId);
        db.SaveChanges();
        _ = LoadOrdersAsync();
    }

    private static void RecordChangeExpense(AppDbContext db, OrderRecord order)
    {
        var usd = Math.Round(Math.Max(0m, order.ChangeGivenUsd), 2);
        var fc = Math.Round(Math.Max(0m, order.ChangeGivenFc), 2);
        if (usd <= 0m && fc <= 0m)
            return;

        var orderCode = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId;
        var usdMarker = $"| CHANGE_ORDER:{order.Id}:USD|";
        var fcMarker = $"| CHANGE_ORDER:{order.Id}:FC|";

        if (usd > 0m && !db.Transactions.Any(t => t.Justification.Contains(usdMarker)))
        {
            db.Transactions.Add(new MoneyTransaction
            {
                Amount = usd,
                AmountUsd = usd,
                AmountFc = CurrencyHelper.ConvertUsdToFc(usd),
                Date = order.CompletedAt ?? DateTime.Now,
                Type = "Expense",
                Category = "Sale Change",
                CurrencyCode = CurrencyHelper.Usd,
                ExchangeRateUsed = CurrencyHelper.FcPerUsd,
                IsFixed = false,
                Justification = $"Cash change returned for order {orderCode} (USD). {usdMarker}"
            });
        }

        if (fc > 0m && !db.Transactions.Any(t => t.Justification.Contains(fcMarker)))
        {
            db.Transactions.Add(new MoneyTransaction
            {
                Amount = fc,
                AmountUsd = CurrencyHelper.ConvertFcToUsd(fc),
                AmountFc = fc,
                Date = order.CompletedAt ?? DateTime.Now,
                Type = "Expense",
                Category = "Sale Change",
                CurrencyCode = CurrencyHelper.CongoleseFranc,
                ExchangeRateUsed = CurrencyHelper.FcPerUsd,
                IsFixed = false,
                Justification = $"Cash change returned for order {orderCode} (FC). {fcMarker}"
            });
        }
    }

    private static void RefreshTableStatus(AppDbContext db, int? tableId)
    {
        if (tableId is null) return;

        var table = db.Tables.SingleOrDefault(t => t.Id == tableId);
        if (table is null) return;

        var hasActiveOrders = db.Orders.Any(o =>
            o.TableId == tableId &&
            (o.Status == "Waiting" || o.Status == "In Kitchen" || o.Status == "Ready" ||
             o.Status == OrderWorkflow.Served ||
             o.Status == OrderWorkflow.PendingCashier));

        if (table.Status == "Maintenance")
            return;

        table.Status = hasActiveOrders ? "Occupied" : "Available";
    }

    private static OrderEntry MapOrder(OrderRecord order, bool isPast, bool showAdminAdvance, bool canViewTicket)
    {
        var lineSubtotal = order.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
        var totals = OrderTotalsHelper.ComputeTotals(lineSubtotal, order.DiscountMode, order.DiscountValue);
        var total = totals.GrandTotal;
        var items = string.Join(", ",
            order.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} x{i.Quantity}"));

        return new OrderEntry
        {
            Id = order.Id,
            OrderId = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId,
            TableNumber = string.IsNullOrWhiteSpace(order.TableCode)
                ? $"Table {order.Table?.TableNumber ?? 0}"
                : $"{order.TableCode} · {order.TableName}",
            ServerName = string.IsNullOrWhiteSpace(order.ServerName)
                ? (order.Server?.Name ?? "Unassigned")
                : order.ServerName,
            Items = items,
            CustomerNotes = order.CustomerNotes ?? string.Empty,
            AllergyNotes = order.AllergyNotes ?? string.Empty,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            Time = order.CreatedAt.ToString("HH:mm"),
            Total = total,
            StatusColor = GetStatusColor(order.Status),
            ShowAdvanceInOrders = !isPast && showAdminAdvance && OrderWorkflow.CanAdminAdvanceOrderStatus(order.Status),
            ShowCompleteInOrders = !isPast && OrderWorkflow.CanCashierComplete(order.Status),
            ShowViewTicketInOrders = canViewTicket
        };
    }

    private static string GetStatusColor(string status) => status switch
    {
        "Waiting" => "#2196F3",
        "In Kitchen" => "#FF9800",
        "Ready" => "#4CAF50",
        OrderWorkflow.Served => "#9C27B0",
        "Completed" => "#4CAF50",
        "Cancelled" => "#F44336",
        var s when string.Equals(s, OrderWorkflow.PendingCashier, StringComparison.OrdinalIgnoreCase) => "#CE93D8",
        _ => "#D4AF37"
    };

    private void OpenTicketPreview(OrderEntry? entry)
    {
        if (entry is null) return;

        if (!entry.ShowViewTicketInOrders)
            return;

        using var db = new AppDbContext();
        var order = db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Include(o => o.Table)
            .Include(o => o.Server)
            .SingleOrDefault(o => o.Id == entry.Id);

        if (order is null)
            return;

        TicketLines.Clear();
        foreach (var item in order.Items)
        {
            var unitPrice = item.Product?.Price ?? 0m;
            TicketLines.Add(new TicketLineViewModel
            {
                Quantity = item.Quantity,
                Name = item.Product?.Name ?? "Unknown",
                UnitPrice = unitPrice,
                LineTotal = unitPrice * item.Quantity
            });
        }

        TicketOrderId = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId;
        TicketStatus = order.Status;
        TicketTable = string.IsNullOrWhiteSpace(order.TableCode)
            ? $"Table {order.Table?.TableNumber ?? 0}"
            : $"{order.TableCode} · {order.TableName}";
        TicketServer = string.IsNullOrWhiteSpace(order.ServerName)
            ? (order.Server?.Name ?? "Unassigned")
            : order.ServerName;
        TicketDateTime = order.CreatedAt;
        var lineSum = TicketLines.Sum(l => l.LineTotal);
        var totals = OrderTotalsHelper.ComputeTotals(lineSum, order.DiscountMode, order.DiscountValue);
        TicketSubtotal = lineSum;
        TicketDiscountAmount = totals.DiscountApplied;
        TicketDiscountLineText = totals.DiscountApplied > 0m
            ? $"{OrderTotalsHelper.FormatDiscountLabel(order.DiscountMode, order.DiscountValue, totals.DiscountApplied)}: -$ {totals.DiscountApplied:N2}"
            : string.Empty;
        TicketShowDiscount = totals.DiscountApplied > 0m;
        TicketTaxAmount = totals.Tax;
        TicketServiceAmount = totals.Service;
        TicketGrandTotal = totals.GrandTotal;
        TicketEquivalentFcText = CurrencyHelper.FormatAmount(
            order.PaymentCurrencyCode == CurrencyHelper.CongoleseFranc && order.PaymentAmount > 0m
                ? order.PaymentAmount
                : CurrencyHelper.ConvertUsdToFc(TicketGrandTotal),
            CurrencyHelper.CongoleseFranc);
        TicketPaymentText = order.PaymentAmount > 0m
            ? CurrencyHelper.FormatAmount(order.PaymentAmount, string.IsNullOrWhiteSpace(order.PaymentCurrencyCode) ? CurrencyHelper.Usd : order.PaymentCurrencyCode)
            : CurrencyHelper.FormatAmount(TicketGrandTotal, CurrencyHelper.Usd);
        TicketPaidBreakdownText =
            $"Paid USD: {CurrencyHelper.FormatAmount(order.CustomerPaidUsd, CurrencyHelper.Usd)} | Paid FC: {CurrencyHelper.FormatAmount(order.CustomerPaidFc, CurrencyHelper.CongoleseFranc)}";
        TicketChangeBreakdownText =
            $"Change USD: {CurrencyHelper.FormatAmount(order.ChangeGivenUsd, CurrencyHelper.Usd)} | Change FC: {CurrencyHelper.FormatAmount(order.ChangeGivenFc, CurrencyHelper.CongoleseFranc)}";
        TicketVerification = $"ERP-DB-{order.Id}-{order.UniqueId[..Math.Min(4, order.UniqueId.Length)]}";
        var settings = SettingsManager.Load();
        TicketRestaurantName = string.IsNullOrWhiteSpace(settings.BusinessProfile.RestaurantName)
            ? "ELITE RESTAURANT PRO"
            : settings.BusinessProfile.RestaurantName.ToUpperInvariant();
        TicketTaxLabel = $"TVA ({settings.CurrencyPricing.TaxPercent:0.##}%)";
        TicketServiceLabel = $"Service ({settings.CurrencyPricing.ServicePercent:0.##}%)";

        IsTicketPreviewOpen = true;
    }

    private void ExportTicketPdf()
    {
        if (!TicketLines.Any())
            return;
        var settings = SettingsManager.Load();
        var business = settings.BusinessProfile;
        var pricing = settings.CurrencyPricing;
        var restaurantTitle = string.IsNullOrWhiteSpace(business.RestaurantName) ? "ELITE RESTAURANT PRO" : business.RestaurantName.ToUpperInvariant();
        var footerText = string.IsNullOrWhiteSpace(business.TicketFooterText) ? "MERCI / THANK YOU" : business.TicketFooterText;
        var legalInfo = business.TaxIdLegalInfo;

        var saveDialog = new SaveFileDialog
        {
            Title = "Save Payment Receipt PDF",
            Filter = "PDF files (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = $"{SanitizeFileName(TicketOrderId)}-payment.pdf"
        };

        if (saveDialog.ShowDialog() != true)
            return;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A6);
                page.Margin(20);
                page.PageColor("#151515");
                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#F0E6C8"));

                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().Text(restaurantTitle).Bold().FontSize(16).FontColor("#D4AF37");
                    column.Item().LineHorizontal(1).LineColor("#7A6231");
                    column.Item().Text($"Date: {TicketDateTime:dd MMM yyyy}    Time: {TicketDateTime:HH:mm}");
                    column.Item().Text($"Order: {TicketOrderId}    Status: {TicketStatus}");
                    column.Item().Text($"Table: {TicketTable}");
                    column.Item().Text($"Server: {TicketServer}");
                    column.Item().LineHorizontal(1).LineColor("#7A6231");

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(24);
                            cols.RelativeColumn(2.8f);
                            cols.RelativeColumn(1.3f);
                            cols.RelativeColumn(1.3f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("QTY").Bold();
                            header.Cell().Text("ITEM").Bold();
                            header.Cell().AlignRight().Text("P.U").Bold();
                            header.Cell().AlignRight().Text("TOTAL").Bold();
                        });

                        foreach (var line in TicketLines)
                        {
                            table.Cell().Text(line.Quantity.ToString());
                            table.Cell().Text(line.Name);
                            table.Cell().AlignRight().Text($"$ {line.UnitPrice:N2}");
                            table.Cell().AlignRight().Text($"$ {line.LineTotal:N2}");
                        }
                    });

                    column.Item().LineHorizontal(1).LineColor("#7A6231");
                    column.Item().AlignRight().Text($"Subtotal: $ {TicketSubtotal:N2}").SemiBold();
                    if (TicketDiscountAmount > 0m)
                    {
                        column.Item().AlignRight().Text(TicketDiscountLineText).FontColor("#E57373");
                        column.Item().AlignRight()
                            .Text($"After discount: $ {TicketSubtotal - TicketDiscountAmount:N2}")
                            .FontSize(9)
                            .FontColor("#C1B28A");
                    }

                    column.Item().AlignRight().Text($"TVA ({pricing.TaxPercent:0.##}%): $ {TicketTaxAmount:N2}");
                    column.Item().AlignRight().Text($"Service ({pricing.ServicePercent:0.##}%): $ {TicketServiceAmount:N2}");
                    column.Item().AlignRight().Text($"GRAND TOTAL USD: $ {TicketGrandTotal:N2}").Bold().FontSize(14).FontColor("#D4AF37");
                    column.Item().AlignRight().Text($"Equivalent FC: {TicketEquivalentFcText}");
                    column.Item().AlignRight().Text($"Collected: {TicketPaymentText}");
                    column.Item().AlignRight().Text(TicketPaidBreakdownText).FontSize(9);
                    column.Item().AlignRight().Text(TicketChangeBreakdownText).FontSize(9);
                    column.Item().LineHorizontal(1).LineColor("#7A6231");
                    if (!string.IsNullOrWhiteSpace(legalInfo))
                        column.Item().Text(legalInfo).FontSize(9).FontColor("#C1B28A");
                    column.Item().Text($"Database Verification: {TicketVerification}").FontSize(9).FontColor("#C1B28A");
                    column.Item().Text(footerText).Bold().FontColor("#D4AF37");
                });
            });
        }).GeneratePdf(saveDialog.FileName);

        MessageBox.Show(
            $"Ticket PDF saved:\n{saveDialog.FileName}",
            "Payment Receipt Export",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ExportClientTicketPdf()
    {
        if (!TicketLines.Any())
            return;
        var settings = SettingsManager.Load();
        var business = settings.BusinessProfile;
        var pricing = settings.CurrencyPricing;
        var restaurantTitle = string.IsNullOrWhiteSpace(business.RestaurantName) ? "ELITE RESTAURANT PRO" : business.RestaurantName.ToUpperInvariant();
        var footerText = string.IsNullOrWhiteSpace(business.TicketFooterText) ? "MERCI / THANK YOU" : business.TicketFooterText;
        var legalInfo = business.TaxIdLegalInfo;

        var saveDialog = new SaveFileDialog
        {
            Title = "Save Client Ticket PDF",
            Filter = "PDF files (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = $"{SanitizeFileName(TicketOrderId)}-client.pdf"
        };

        if (saveDialog.ShowDialog() != true)
            return;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A6);
                page.Margin(20);
                page.PageColor("#151515");
                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#F0E6C8"));

                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().Text(restaurantTitle).Bold().FontSize(16).FontColor("#D4AF37");
                    column.Item().LineHorizontal(1).LineColor("#7A6231");
                    column.Item().Text($"Date: {TicketDateTime:dd MMM yyyy}    Time: {TicketDateTime:HH:mm}");
                    column.Item().Text($"Order: {TicketOrderId}");
                    column.Item().Text($"Table: {TicketTable}");
                    column.Item().Text($"Server: {TicketServer}");
                    column.Item().LineHorizontal(1).LineColor("#7A6231");

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(24);
                            cols.RelativeColumn(2.8f);
                            cols.RelativeColumn(1.3f);
                            cols.RelativeColumn(1.3f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("QTY").Bold();
                            header.Cell().Text("ITEM").Bold();
                            header.Cell().AlignRight().Text("P.U").Bold();
                            header.Cell().AlignRight().Text("TOTAL").Bold();
                        });

                        foreach (var line in TicketLines)
                        {
                            table.Cell().Text(line.Quantity.ToString());
                            table.Cell().Text(line.Name);
                            table.Cell().AlignRight().Text($"$ {line.UnitPrice:N2}");
                            table.Cell().AlignRight().Text($"$ {line.LineTotal:N2}");
                        }
                    });

                    column.Item().LineHorizontal(1).LineColor("#7A6231");
                    column.Item().AlignRight().Text($"Subtotal: $ {TicketSubtotal:N2}").SemiBold();
                    if (TicketDiscountAmount > 0m)
                    {
                        column.Item().AlignRight().Text(TicketDiscountLineText).FontColor("#E57373");
                        column.Item().AlignRight()
                            .Text($"After discount: $ {TicketSubtotal - TicketDiscountAmount:N2}")
                            .FontSize(9)
                            .FontColor("#C1B28A");
                    }
                    column.Item().AlignRight().Text($"TVA ({pricing.TaxPercent:0.##}%): $ {TicketTaxAmount:N2}");
                    column.Item().AlignRight().Text($"Service ({pricing.ServicePercent:0.##}%): $ {TicketServiceAmount:N2}");
                    column.Item().AlignRight().Text($"GRAND TOTAL USD: $ {TicketGrandTotal:N2}").Bold().FontSize(14).FontColor("#D4AF37");
                    column.Item().AlignRight().Text($"Equivalent FC: {TicketEquivalentFcText}");
                    column.Item().LineHorizontal(1).LineColor("#7A6231");
                    if (!string.IsNullOrWhiteSpace(legalInfo))
                        column.Item().Text(legalInfo).FontSize(9).FontColor("#C1B28A");
                    column.Item().Text(footerText).Bold().FontColor("#D4AF37");
                });
            });
        }).GeneratePdf(saveDialog.FileName);

        MessageBox.Show(
            $"Client ticket saved:\n{saveDialog.FileName}",
            "Client Ticket Export",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "order-ticket";

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(input.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "order-ticket" : sanitized;
    }
}
