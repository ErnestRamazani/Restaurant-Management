using System.Windows;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.Views;

public partial class DatabasePipeSetupDialog : Window
{
    public string? PipeInput { get; private set; }

    public DatabasePipeSetupDialog(string? initialPipe = null)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(initialPipe))
            PipeInputBox.Text = initialPipe;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var raw = PipeInputBox.Text ?? string.Empty;
        if (!PostgresBootstrapPipe.TryParse(raw.Trim(), out _, out _, out _, out _, out _))
        {
            ErrorText.Text = "Invalid format. Use: host|port|database|username|password";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        PipeInput = raw.Trim();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
