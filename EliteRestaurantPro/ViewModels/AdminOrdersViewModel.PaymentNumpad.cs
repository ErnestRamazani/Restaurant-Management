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
}
