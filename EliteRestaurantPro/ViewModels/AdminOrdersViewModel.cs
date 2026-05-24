using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using EliteRestaurant.Core.Tickets;
using EliteRestaurantPro.Services;

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
        ExportTicketPdfCommand = new RelayCommand(_ => PrintPaymentReceipt());
        ExportClientTicketPdfCommand = new RelayCommand(_ => PrintClientTicket());
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
            var snapshot = await AdminOrdersSnapshotLoader.LoadAsync(showAdminAdvance, canViewTicket);

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
            o.ConfirmationCode,
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

        _ = ReleasePendingToKitchenCoreAsync(row);
    }

    private async Task ReleasePendingToKitchenCoreAsync(CashierQueueRow row)
    {
        try
        {
            var result = await _cloudOps.TryReleasePendingToKitchenAsync(row.OrderId);
            if (!result.Ok)
            {
                MessageBox.Show(result.ErrorMessage ?? "Cannot release order.", "Orders", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                await LoadOrdersAsync();
                return;
            }

            MessageBox.Show($"Order {result.ReleasedOrderCode} sent to the kitchen.", "Orders", MessageBoxButton.OK,
                MessageBoxImage.Information);
            await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "Orders", MessageBoxButton.OK, MessageBoxImage.Warning);
            await LoadOrdersAsync();
        }
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

        _ = CancelPendingCashierCoreAsync(row);
    }

    private async Task CancelPendingCashierCoreAsync(CashierQueueRow row)
    {
        try
        {
            var err = await _cloudOps.TryCancelPendingCashierAsync(row.OrderId);
            if (err is not null)
            {
                MessageBox.Show(err, "Orders", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadOrdersAsync();
                return;
            }

            await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "Orders", MessageBoxButton.OK, MessageBoxImage.Warning);
            await LoadOrdersAsync();
        }
    }

    private void CreateOrder()
    {
        var selectedProducts = ProductSelections.Where(p => p.IsSelected).ToList();
        if (SelectedTableId == 0 || !selectedProducts.Any())
        {
            return;
        }

        _ = CreateOrderCoreAsync(selectedProducts);
    }

    private async Task CreateOrderCoreAsync(List<ProductSelectionItemViewModel> selectedProducts)
    {
        var data = new ApiClients.AdminDataApiClient();
        List<Table> tables;
        List<Employee> employees;
        try
        {
            tables = (await data.GetTablesAsync().ConfigureAwait(false)).ToList();
            employees = (await data.GetEmployeesAsync().ConfigureAwait(false)).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "Orders", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var table = tables.SingleOrDefault(t => t.Id == SelectedTableId);
        if (table is null)
            return;

        Employee? assigned = null;
        if (table.AssignedServerId is int sid)
            assigned = employees.FirstOrDefault(e => e.Id == sid);
        if (assigned is null)
        {
            MessageBox.Show("This table must have an assigned server.", "Orders", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var confirmAdd = MessageBox.Show(
            $"Create order for Table {table.TableNumber} ({table.Name}) assigned to {assigned.Name}?",
            "Confirm Create Order",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmAdd != MessageBoxResult.Yes)
            return;

        var lines = selectedProducts
            .Select(p => new AdminWalkInLine(p.ProductId, p.Quantity))
            .ToList();

        try
        {
            var errCreate = await _cloudOps.TryCreateWalkInOrderAsync(table.Id, SelectedOrderStatus, lines);
            if (errCreate is not null)
            {
                MessageBox.Show(errCreate, "Insufficient Inventory", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "Orders", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        foreach (var selection in ProductSelections)
        {
            selection.IsSelected = false;
            selection.Quantity = 1;
        }

        await LoadOrdersAsync();
    }

    private void AdvanceOrder(OrderEntry? entry)
    {
        if (entry is null) return;

        if (!ShowFullAdminNav)
            return;

        _ = AdvanceOrderCoreAsync(entry);
    }

    private async Task AdvanceOrderCoreAsync(OrderEntry entry)
    {
        try
        {
            var msg = await _cloudOps.TryAdvanceOrderAsync(entry.Id);
            if (msg == string.Empty)
                return;
            if (msg is not null)
            {
                MessageBox.Show(msg, "Orders", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "Orders", MessageBoxButton.OK, MessageBoxImage.Warning);
            await LoadOrdersAsync();
        }
    }

    private void CompleteOrder(OrderEntry? entry)
    {
        if (entry is null)
            return;

        if (!OrderWorkflow.CanCashierComplete(entry.Status, entry.OrderOrigin))
        {
            MessageBox.Show(
                OrderOrigin.IsOnline(entry.OrderOrigin)
                    ? "Complete payment for guest online orders is available once the kitchen marks the ticket Ready.\n\nYou do not need to wait for a server to mark Served."
                    : "Complete is only available when the order is Served.\n\nFlow: kitchen marks Ready → server uses Pick up & serve (or admin uses Advance on Ready) → Served → then complete payment.",
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
        _ = UpdateOrderStatusAsync(entry, status, paymentCurrencyOverride, paidUsd, paidFc, changeGivenUsd, changeGivenFc);
    }

    private async Task UpdateOrderStatusAsync(
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
            await _cloudOps.UpdateOrderStatusAsync(
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
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "Orders", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await LoadOrdersAsync();
    }

    private void OpenTicketPreview(OrderEntry? entry)
    {
        if (entry is null) return;

        if (!entry.ShowViewTicketInOrders)
            return;

        _ = OpenTicketPreviewAsync(entry);
    }

    private async Task OpenTicketPreviewAsync(OrderEntry entry)
    {
        try
        {
            var data = new ApiClients.AdminDataApiClient();
            var ordersTask = data.GetOrdersAsync();
            var productsTask = data.GetProductsAsync();
            var tablesTask = data.GetTablesAsync();
            var employeesTask = data.GetEmployeesAsync();
            await Task.WhenAll(ordersTask, productsTask, tablesTask, employeesTask).ConfigureAwait(false);

            var order = (await ordersTask.ConfigureAwait(false)).FirstOrDefault(o => o.Id == entry.Id);
            if (order is null)
                return;

            var products = await productsTask.ConfigureAwait(false);
            var productById = products.ToDictionary(p => p.Id);
            foreach (var item in order.Items)
            {
                if (productById.TryGetValue(item.ProductId, out var p))
                    item.Product = p;
            }

            if (order.TableId is int tid)
                order.Table = (await tablesTask.ConfigureAwait(false)).FirstOrDefault(t => t.Id == tid);
            if (order.ServerId is int sid)
                order.Server = (await employeesTask.ConfigureAwait(false)).FirstOrDefault(e => e.Id == sid);

            await Application.Current.Dispatcher.InvokeAsync(() => ApplyTicketPreview(order));
        }
        catch
        {
            // Best-effort preview
        }
    }

    private void ApplyTicketPreview(OrderRecord order)
    {
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
        TicketConfirmationCode = (order.ConfirmationCode ?? string.Empty).Trim();
        TicketStatus = order.Status;
        TicketLocationLine = OrderRecordUiLabels.TicketLocationLine(order);
        TicketTable = OrderRecordUiLabels.TableCaption(order);
        ApplyTicketDeliveryInfo(
            OrderRecordUiLabels.TryGetOnlineGuestTicketInfo(order),
            OrderRecordUiLabels.IsDeliveryOrder(order));
        TicketShowServer = OrderRecordUiLabels.ShowServerOnTicket(order);
        TicketServer = OrderRecordUiLabels.ServerCaption(order);
        TicketDateTime = order.CreatedAt;
        var lineSum = TicketLines.Sum(l => l.LineTotal);
        var totals = OrderTotalsHelper.ComputeTotalsWithDeliveryFee(
            lineSum,
            order.DiscountMode,
            order.DiscountValue,
            order.DeliveryFeeUsd);
        TicketSubtotal = lineSum;
        TicketDiscountAmount = totals.DiscountApplied;
        TicketDiscountLineText = totals.DiscountApplied > 0m
            ? $"{OrderTotalsHelper.FormatDiscountLabel(order.DiscountMode, order.DiscountValue, totals.DiscountApplied)}: -$ {totals.DiscountApplied:N2}"
            : string.Empty;
        TicketShowDiscount = totals.DiscountApplied > 0m;
        TicketTaxAmount = totals.Tax;
        TicketServiceAmount = totals.Service;
        TicketDeliveryFeeUsd = Math.Round(Math.Max(0m, order.DeliveryFeeUsd), 2);
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
        var uid = string.IsNullOrWhiteSpace(order.UniqueId) ? string.Empty : order.UniqueId;
        TicketVerification = $"ERP-DB-{order.Id}-{uid[..Math.Min(4, uid.Length)]}";
        var settings = SettingsManager.Load();
        TicketRestaurantName = string.IsNullOrWhiteSpace(settings.BusinessProfile.RestaurantName)
            ? "ELITE RESTAURANT PRO"
            : settings.BusinessProfile.RestaurantName.ToUpperInvariant();
        TicketTaxLabel = $"TVA ({settings.CurrencyPricing.TaxPercent:0.##}%)";
        TicketServiceLabel = $"Service ({settings.CurrencyPricing.ServicePercent:0.##}%)";

        OnPropertyChanged(nameof(TicketOrderId));
        OnPropertyChanged(nameof(TicketConfirmationCode));
        OnPropertyChanged(nameof(TicketStatus));
        IsTicketPreviewOpen = true;
    }

    private void ApplyTicketDeliveryInfo(DeliveryTicketInfo? delivery, bool isDelivery)
    {
        TicketIsDeliveryFulfillment = isDelivery;
        if (delivery is null)
        {
            TicketShowDeliverySection = false;
            TicketDeliveryCustomerName = string.Empty;
            TicketDeliveryPhone = string.Empty;
            TicketDeliveryAddress = string.Empty;
            TicketDeliveryInstructions = string.Empty;
            return;
        }

        TicketShowDeliverySection = true;
        TicketDeliveryCustomerName = delivery.CustomerName;
        TicketDeliveryPhone = delivery.Phone;
        TicketDeliveryAddress = delivery.Address;
        TicketDeliveryInstructions = delivery.Instructions;
    }

    private DeliveryTicketInfo? BuildTicketDeliveryInfoForPdf()
    {
        if (!TicketShowDeliverySection)
            return null;
        return new DeliveryTicketInfo(
            TicketDeliveryCustomerName,
            TicketDeliveryPhone,
            TicketDeliveryAddress,
            TicketDeliveryInstructions);
    }

    private static string FormatReceiptWebsiteLine(string? domain)
    {
        var d = (domain ?? "").Trim();
        if (string.IsNullOrEmpty(d))
            return string.Empty;
        if (d.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            d.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return d;
        return $"https://{d}";
    }

    private TicketReceiptPdfModel BuildTicketReceiptPdfModel()
    {
        var settings = SettingsManager.Load();
        var business = settings.BusinessProfile;
        var ticketReceipt = settings.TicketReceipt ?? new TicketReceiptSettings();
        var pricing = settings.CurrencyPricing;

        var socialRows = new List<TicketSocialMediaPdfRow>();
        foreach (var row in ticketReceipt.SocialMediaRows)
        {
            var plat = (row.PlatformName ?? string.Empty).Trim();
            var user = (row.UserText ?? string.Empty).Trim();
            if (plat.Length == 0 && user.Length == 0)
                continue;
            var iconBytes = TicketReceiptPdfImageHelper.TryLoadRasterImage(row.IconPath);
            socialRows.Add(new TicketSocialMediaPdfRow(plat, user, iconBytes));
        }

        var headerBytes = TicketReceiptPdfImageHelper.TryLoadRasterImage(ticketReceipt.HeaderLogoPath);

        return new TicketReceiptPdfModel
        {
            Lines = TicketLines.Select(l => new TicketPdfLine(l.Quantity, l.Name, l.UnitPrice, l.LineTotal)).ToList(),
            TicketOrderId = TicketOrderId,
            TicketConfirmationCode = TicketConfirmationCode,
            TicketStatus = TicketStatus,
            TicketTable = TicketTable,
            TicketLocationLine = TicketLocationLine,
            DeliveryInfo = BuildTicketDeliveryInfoForPdf(),
            TicketIsDeliveryFulfillment = TicketIsDeliveryFulfillment,
            ShowServerOnTicket = TicketShowServer,
            TicketServer = TicketServer,
            TicketDateTime = TicketDateTime,
            TicketSubtotal = TicketSubtotal,
            TicketDiscountAmount = TicketDiscountAmount,
            TicketDiscountLineText = TicketDiscountLineText,
            TicketTaxAmount = TicketTaxAmount,
            TicketServiceAmount = TicketServiceAmount,
            TicketDeliveryFeeUsd = TicketDeliveryFeeUsd,
            TicketGrandTotal = TicketGrandTotal,
            TicketEquivalentFcText = TicketEquivalentFcText,
            TicketPaymentText = TicketPaymentText,
            TicketPaidBreakdownText = TicketPaidBreakdownText,
            TicketChangeBreakdownText = TicketChangeBreakdownText,
            TicketVerification = TicketVerification,
            TaxPercent = pricing.TaxPercent,
            ServicePercent = pricing.ServicePercent,
            HeaderLogoBytes = headerBytes,
            RestaurantTitle = string.IsNullOrWhiteSpace(business.RestaurantName)
                ? "ELITE RESTAURANT PRO"
                : business.RestaurantName.ToUpperInvariant(),
            RestaurantPhone = (business.Phone ?? string.Empty).Trim(),
            FooterText = string.IsNullOrWhiteSpace(business.TicketFooterText) ? "MERCI / THANK YOU" : business.TicketFooterText.Trim(),
            ReceiptAddress = (business.Address ?? string.Empty).Trim(),
            ReceiptWebsiteLine = FormatReceiptWebsiteLine(business.WebsiteDomain),
            SocialFooterRows = socialRows,
            LegalInfo = business.TaxIdLegalInfo
        };
    }

    private void PrintPaymentReceipt()
    {
        if (!TicketLines.Any())
            return;

        try
        {
            var settings = SettingsManager.Load();
            var printer = (settings.TicketReceipt?.ReceiptPrinterName ?? string.Empty).Trim();
            var bytes = AdminTicketPdfExportService.GeneratePaymentReceiptPdfBytes(BuildTicketReceiptPdfModel());
            var name = $"{AdminTicketPdfExportService.SanitizeFileName(TicketOrderId)}-payment";
            ReceiptTicketPrintService.Print(bytes, printer, name);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                "Print payment receipt",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void PrintClientTicket()
    {
        if (!TicketLines.Any())
            return;

        try
        {
            var settings = SettingsManager.Load();
            var printer = (settings.TicketReceipt?.ReceiptPrinterName ?? string.Empty).Trim();
            var bytes = AdminTicketPdfExportService.GenerateClientTicketPdfBytes(BuildTicketReceiptPdfModel());
            var name = $"{AdminTicketPdfExportService.SanitizeFileName(TicketOrderId)}-client";
            ReceiptTicketPrintService.Print(bytes, printer, name);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                "Print client ticket",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
