using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EliteRestaurantPro.Views;

public partial class OrderCancelPasscodeDialog : Window
{
    public string? EnteredPasscode { get; private set; }

    public OrderCancelPasscodeDialog(string orderLabel)
    {
        InitializeComponent();
        SummaryText.Text = $"Enter the admin cancel passcode to cancel {orderLabel}.";
    }

    public static string? Prompt(Window? owner, string orderLabel)
    {
        var dlg = new OrderCancelPasscodeDialog(orderLabel);
        if (owner is not null)
            dlg.Owner = owner;
        return dlg.ShowDialog() == true ? dlg.EnteredPasscode : null;
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
            MessageBox.Show("Enter the admin cancel passcode.", "Cancel order", MessageBoxButton.OK,
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
