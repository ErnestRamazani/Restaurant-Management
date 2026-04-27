using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.Services;
using Microsoft.Win32;
using Microsoft.EntityFrameworkCore;
using ModelTable = EliteRestaurant.Core.Models.Table;

namespace EliteRestaurantPro.ViewModels;

public partial class AdminOrdersViewModel : AdminBaseViewModel
{
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

    private async Task LoadOrdersAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        try
        {
            var showAdminAdvance = ShowFullAdminNav;
            var canViewTicket = ShowFullAdminNav || AppSession.IsCashierTablet;
            var snapshot = await Task.Run(() => AdminOrdersSnapshotLoader.Load(showAdminAdvance, canViewTicket));

            PendingCashierOrders.Clear();
            foreach (var row in snapshot.PendingCashier)
                PendingCashierOrders.Add(row);
            ShowPendingCashierSection = PendingCashierOrders.Count > 0;
            OnPropertyChanged(nameof(PendingCashierSectionTitle));

            AvailableTables.Clear();
            ProductSelections.Clear();

            _masterActiveOrders.Clear();
            _masterActiveOrders.AddRange(snapshot.ActiveOrders);
            _masterPastOrders.Clear();
            _masterPastOrders.AddRange(snapshot.PastOrders);

            ApplyOrderSearchFilters();

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

    private void ApplyOrderSearchFilters()
    {
        ActiveOrders.Clear();
        foreach (var o in _masterActiveOrders.Where(o => MatchesOrderFilter(o, _activeOrdersSearchFilter)))
            ActiveOrders.Add(o);

        PastOrders.Clear();
        PastOrderDayGroups.Clear();
        var filteredPast = _masterPastOrders.Where(o => MatchesOrderFilter(o, _pastOrdersSearchFilter)).ToList();
        foreach (var o in filteredPast)
            PastOrders.Add(o);

        foreach (var group in filteredPast
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
    }

    private static bool MatchesOrderFilter(OrderEntry o, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        var needle = filter.Trim().ToLowerInvariant();
        var hay = string.Join(" ",
            o.OrderId,
            o.TableNumber,
            o.ServerName,
            o.Items,
            o.Status,
            o.CustomerNotes,
            o.AllergyNotes,
            o.Id.ToString(CultureInfo.InvariantCulture),
            o.Time,
            o.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            o.CreatedAt.ToString("MMM d", CultureInfo.InvariantCulture),
            o.Total.ToString("0.##", CultureInfo.InvariantCulture)).ToLowerInvariant();
        return hay.Contains(needle);
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

        var result = _orderOps.TryReleasePendingToKitchen(row.OrderId);
        if (!result.Ok)
        {
            MessageBox.Show(result.ErrorMessage ?? "Cannot release order.", "Orders", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            _ = LoadOrdersAsync();
            return;
        }

        MessageBox.Show($"Order {result.ReleasedOrderCode} sent to the kitchen.", "Orders", MessageBoxButton.OK,
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

        var err = _orderOps.TryCancelPendingCashier(row.OrderId);
        if (err is not null)
        {
            MessageBox.Show(err, "Orders", MessageBoxButton.OK, MessageBoxImage.Information);
            _ = LoadOrdersAsync();
            return;
        }

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

        var lines = selectedProducts
            .Select(p => new AdminWalkInLine(p.ProductId, p.Quantity))
            .ToList();

        var errCreate = _orderOps.TryCreateWalkInOrder(table.Id, SelectedOrderStatus, lines);
        if (errCreate is not null)
        {
            MessageBox.Show(errCreate, "Insufficient Inventory", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

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

        var msg = _orderOps.TryAdvanceOrder(entry.Id);
        if (msg == string.Empty)
            return;
        if (msg is not null)
        {
            MessageBox.Show(msg, "Orders", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

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

        try
        {
            _orderOps.UpdateOrderStatus(
                entry.Id,
                status,
                paymentCurrencyOverride,
                paidUsd,
                paidFc,
                changeGivenUsd,
                changeGivenFc);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Orders", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _ = LoadOrdersAsync();
    }

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

    private TicketReceiptPdfModel BuildTicketReceiptPdfModel()
    {
        var settings = SettingsManager.Load();
        var business = settings.BusinessProfile;
        var pricing = settings.CurrencyPricing;
        return new TicketReceiptPdfModel
        {
            Lines = TicketLines.Select(l => new TicketPdfLine(l.Quantity, l.Name, l.UnitPrice, l.LineTotal)).ToList(),
            TicketOrderId = TicketOrderId,
            TicketStatus = TicketStatus,
            TicketTable = TicketTable,
            TicketServer = TicketServer,
            TicketDateTime = TicketDateTime,
            TicketSubtotal = TicketSubtotal,
            TicketDiscountAmount = TicketDiscountAmount,
            TicketDiscountLineText = TicketDiscountLineText,
            TicketTaxAmount = TicketTaxAmount,
            TicketServiceAmount = TicketServiceAmount,
            TicketGrandTotal = TicketGrandTotal,
            TicketEquivalentFcText = TicketEquivalentFcText,
            TicketPaymentText = TicketPaymentText,
            TicketPaidBreakdownText = TicketPaidBreakdownText,
            TicketChangeBreakdownText = TicketChangeBreakdownText,
            TicketVerification = TicketVerification,
            TaxPercent = pricing.TaxPercent,
            ServicePercent = pricing.ServicePercent,
            RestaurantTitle = string.IsNullOrWhiteSpace(business.RestaurantName)
                ? "ELITE RESTAURANT PRO"
                : business.RestaurantName.ToUpperInvariant(),
            FooterText = string.IsNullOrWhiteSpace(business.TicketFooterText) ? "MERCI / THANK YOU" : business.TicketFooterText,
            LegalInfo = business.TaxIdLegalInfo
        };
    }

    private void ExportTicketPdf()
    {
        if (!TicketLines.Any())
            return;

        var saveDialog = new SaveFileDialog
        {
            Title = "Save Payment Receipt PDF",
            Filter = "PDF files (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = $"{AdminTicketPdfExportService.SanitizeFileName(TicketOrderId)}-payment.pdf"
        };

        if (saveDialog.ShowDialog() != true)
            return;

        AdminTicketPdfExportService.ExportPaymentReceiptPdf(saveDialog.FileName, BuildTicketReceiptPdfModel());

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

        var saveDialog = new SaveFileDialog
        {
            Title = "Save Client Ticket PDF",
            Filter = "PDF files (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = $"{AdminTicketPdfExportService.SanitizeFileName(TicketOrderId)}-client.pdf"
        };

        if (saveDialog.ShowDialog() != true)
            return;

        AdminTicketPdfExportService.ExportClientTicketPdf(saveDialog.FileName, BuildTicketReceiptPdfModel());

        MessageBox.Show(
            $"Client ticket saved:\n{saveDialog.FileName}",
            "Client Ticket Export",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
