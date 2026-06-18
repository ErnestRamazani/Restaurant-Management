using System.Windows;
using System.Windows.Input;

namespace EliteRestaurantPro.Views;

public partial class EmployeeDeletePasscodeDialog : Window
{
    public string? EnteredPasscode { get; private set; }

    private readonly string _emptyPasscodeMessage;

    public EmployeeDeletePasscodeDialog(
        string title,
        string summary,
        string confirmLabel,
        string backLabel,
        string emptyPasscodeMessage)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        SummaryText.Text = summary;
        ConfirmButton.Content = confirmLabel;
        BackButton.Content = backLabel;
        _emptyPasscodeMessage = emptyPasscodeMessage;
    }

    public static string? Prompt(
        Window? owner,
        string title,
        string summary,
        string confirmLabel,
        string backLabel,
        string emptyPasscodeMessage)
    {
        var dlg = new EmployeeDeletePasscodeDialog(title, summary, confirmLabel, backLabel, emptyPasscodeMessage);
        if (owner is not null)
            dlg.Owner = owner;
        return dlg.ShowDialog() == true ? dlg.EnteredPasscode : null;
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
        EnteredPasscode = PasscodeBox.Password.Trim();
        if (string.IsNullOrEmpty(EnteredPasscode))
        {
            MessageBox.Show(_emptyPasscodeMessage, Title, MessageBoxButton.OK, MessageBoxImage.Information);
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
