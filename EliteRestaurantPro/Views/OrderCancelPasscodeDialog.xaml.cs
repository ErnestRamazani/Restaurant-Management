using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EliteRestaurantPro.Localization;

namespace EliteRestaurantPro.Views;

public partial class OrderCancelPasscodeDialog : Window
{
    public string? EnteredPasscode { get; private set; }

    private OrderCancelPasscodeDialog(string orderLabel, bool forRefund)
    {
        InitializeComponent();
        ApplyLocalizedText(orderLabel, forRefund);
    }

    public static string? Prompt(Window? owner, string orderLabel) =>
        Show(owner, orderLabel, forRefund: false);

    public static string? PromptForRefund(Window? owner, string orderLabel) =>
        Show(owner, orderLabel, forRefund: true);

    private static string? Show(Window? owner, string orderLabel, bool forRefund)
    {
        var dlg = new OrderCancelPasscodeDialog(orderLabel, forRefund);
        if (owner is not null)
            dlg.Owner = owner;
        return dlg.ShowDialog() == true ? dlg.EnteredPasscode : null;
    }

    private void ApplyLocalizedText(string orderLabel, bool forRefund)
    {
        Title = forRefund
            ? Loc.Admin("ordRefundPasscodeTitle", "Issue refund")
            : Loc.Admin("ordCancelPasscodeTitle", "Cancel order");
        TitleText.Text = Loc.Admin("ordPasscodeRequired", "Admin passcode required");
        SummaryText.Text = forRefund
            ? Loc.Admin("ordRefundPasscodeBody", "Enter the admin passcode to issue a refund for {{orderId}}.",
                new Dictionary<string, string> { ["orderId"] = orderLabel })
            : Loc.Admin("ordCancelPasscodeBody", "Enter the admin cancel passcode to cancel {{orderId}}.",
                new Dictionary<string, string> { ["orderId"] = orderLabel });
        ConfirmButton.Content = forRefund
            ? Loc.Admin("ordRefundPasscodeConfirm", "Issue refund")
            : Loc.Admin("ordCancelPasscodeConfirm", "Cancel order");
        BackButton.Content = Loc.Common("back", "Back");
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // HWND not ready — ignore.
            }
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        EnteredPasscode = PasscodeBox.Password.Trim();
        if (string.IsNullOrEmpty(EnteredPasscode))
        {
            MessageBox.Show(
                Loc.Admin("ordPasscodeEmpty", "Enter the admin passcode."),
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
