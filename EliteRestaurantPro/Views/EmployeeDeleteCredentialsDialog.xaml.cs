using System.Windows;
using System.Windows.Input;

namespace EliteRestaurantPro.Views;

public partial class EmployeeDeleteCredentialsDialog : Window
{
    public string? EnteredSignInId { get; private set; }
    public string? EnteredPin { get; private set; }

    private readonly string _emptyFieldsMessage;

    public EmployeeDeleteCredentialsDialog(
        string title,
        string summary,
        string signInIdLabel,
        string pinLabel,
        string confirmLabel,
        string backLabel,
        string emptyFieldsMessage)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        SummaryText.Text = summary;
        SignInIdLabelText.Text = signInIdLabel;
        PinLabelText.Text = pinLabel;
        ConfirmButton.Content = confirmLabel;
        BackButton.Content = backLabel;
        _emptyFieldsMessage = emptyFieldsMessage;
    }

    public static (string? SignInId, string? Pin) Prompt(
        Window? owner,
        string title,
        string summary,
        string signInIdLabel,
        string pinLabel,
        string confirmLabel,
        string backLabel,
        string emptyFieldsMessage)
    {
        var dlg = new EmployeeDeleteCredentialsDialog(
            title, summary, signInIdLabel, pinLabel, confirmLabel, backLabel, emptyFieldsMessage);
        if (owner is not null)
            dlg.Owner = owner;
        if (dlg.ShowDialog() != true)
            return (null, null);
        return (dlg.EnteredSignInId, dlg.EnteredPin);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); }
            catch { /* HWND not ready */ }
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        EnteredSignInId = SignInIdBox.Text.Trim();
        EnteredPin = PinBox.Password.Trim();
        if (string.IsNullOrEmpty(EnteredSignInId) || string.IsNullOrEmpty(EnteredPin))
        {
            MessageBox.Show(_emptyFieldsMessage, Title, MessageBoxButton.OK, MessageBoxImage.Information);
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
