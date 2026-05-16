using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Services;
using Microsoft.Win32;
using ModelTable = EliteRestaurant.Core.Models.Table;

namespace EliteRestaurantPro.ViewModels;

public partial class AdminOrdersViewModel : AdminBaseViewModel
{
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
    private string _ticketConfirmationCode = string.Empty;
    private string _ticketStatus = string.Empty;
    private string _ticketTable = string.Empty;
    private string _ticketLocationLine = string.Empty;
    private string _ticketDeliveryCustomerName = string.Empty;
    private string _ticketDeliveryPhone = string.Empty;
    private string _ticketDeliveryAddress = string.Empty;
    private string _ticketDeliveryInstructions = string.Empty;
    private bool _ticketShowServer = true;
    private string _ticketServer = string.Empty;
    private DateTime _ticketDateTime = DateTime.Now;
    private decimal _ticketSubtotal;
    private decimal _ticketTaxAmount;
    private decimal _ticketServiceAmount;
    private decimal _ticketDeliveryFeeUsd;
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
    private readonly List<OrderEntry> _masterActiveOrders = [];
    private readonly List<OrderEntry> _masterPastOrders = [];
    private string _activeOrdersSearchFilter = string.Empty;
    private string _pastOrdersSearchFilter = string.Empty;
    private readonly AdminOrderCloudOperations _cloudOps = new();

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

    /// <summary>Filters active orders by order #, table, server, items, status (case-insensitive).</summary>
    public string ActiveOrdersSearchFilter
    {
        get => _activeOrdersSearchFilter;
        set
        {
            if (!SetField(ref _activeOrdersSearchFilter, value))
                return;
            ApplyOrderSearchFilters();
        }
    }

    /// <summary>Filters past orders by order #, table, server, items, status (case-insensitive).</summary>
    public string PastOrdersSearchFilter
    {
        get => _pastOrdersSearchFilter;
        set
        {
            if (!SetField(ref _pastOrdersSearchFilter, value))
                return;
            ApplyOrderSearchFilters();
        }
    }
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

    public string TicketConfirmationCode
    {
        get => _ticketConfirmationCode;
        set
        {
            if (!SetField(ref _ticketConfirmationCode, value))
                return;
            OnPropertyChanged(nameof(TicketShowConfirmationCode));
        }
    }

    public bool TicketShowConfirmationCode => !string.IsNullOrWhiteSpace(TicketConfirmationCode);

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

    public string TicketLocationLine
    {
        get => _ticketLocationLine;
        set => SetField(ref _ticketLocationLine, value);
    }

    private bool _ticketShowDeliverySection;
    private bool _ticketIsDeliveryFulfillment;

    public bool TicketShowDeliverySection
    {
        get => _ticketShowDeliverySection;
        set => SetField(ref _ticketShowDeliverySection, value);
    }

    public bool TicketIsDeliveryFulfillment
    {
        get => _ticketIsDeliveryFulfillment;
        private set
        {
            if (!SetField(ref _ticketIsDeliveryFulfillment, value))
                return;
            OnPropertyChanged(nameof(TicketFulfillmentSectionTitle));
        }
    }

    public string TicketFulfillmentSectionTitle => TicketIsDeliveryFulfillment ? "DELIVERY" : "PICKUP";

    public string TicketDeliveryCustomerName
    {
        get => _ticketDeliveryCustomerName;
        set
        {
            if (!SetField(ref _ticketDeliveryCustomerName, value))
                return;
            OnPropertyChanged(nameof(TicketShowDeliveryCustomerName));
        }
    }

    public string TicketDeliveryPhone
    {
        get => _ticketDeliveryPhone;
        set
        {
            if (!SetField(ref _ticketDeliveryPhone, value))
                return;
            OnPropertyChanged(nameof(TicketShowDeliveryPhone));
        }
    }

    public string TicketDeliveryAddress
    {
        get => _ticketDeliveryAddress;
        set
        {
            if (!SetField(ref _ticketDeliveryAddress, value))
                return;
            OnPropertyChanged(nameof(TicketShowDeliveryAddress));
        }
    }

    public string TicketDeliveryInstructions
    {
        get => _ticketDeliveryInstructions;
        set
        {
            if (!SetField(ref _ticketDeliveryInstructions, value))
                return;
            OnPropertyChanged(nameof(TicketShowDeliveryInstructions));
        }
    }

    public bool TicketShowDeliveryCustomerName => !string.IsNullOrWhiteSpace(TicketDeliveryCustomerName);
    public bool TicketShowDeliveryPhone => !string.IsNullOrWhiteSpace(TicketDeliveryPhone);
    public bool TicketShowDeliveryAddress => !string.IsNullOrWhiteSpace(TicketDeliveryAddress);
    public bool TicketShowDeliveryInstructions => !string.IsNullOrWhiteSpace(TicketDeliveryInstructions);

    public bool TicketShowServer
    {
        get => _ticketShowServer;
        set => SetField(ref _ticketShowServer, value);
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

    public decimal TicketDeliveryFeeUsd
    {
        get => _ticketDeliveryFeeUsd;
        set
        {
            if (!SetField(ref _ticketDeliveryFeeUsd, value))
                return;
            OnPropertyChanged(nameof(TicketShowDeliveryFee));
        }
    }

    public bool TicketShowDeliveryFee => TicketDeliveryFeeUsd > 0m;

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
}
