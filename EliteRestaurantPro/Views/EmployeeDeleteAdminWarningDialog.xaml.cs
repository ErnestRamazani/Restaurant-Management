using System.Windows;
using System.Windows.Input;

namespace EliteRestaurantPro.Views;

public partial class EmployeeDeleteAdminWarningDialog : Window
{
    public EmployeeDeleteAdminWarningDialog(
        string title,
        string warning,
        string deleteAnywayLabel,
        string backLabel)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        WarningText.Text = warning;
        DeleteAnywayButton.Content = deleteAnywayLabel;
        BackButton.Content = backLabel;
    }

    public static bool Confirm(
        Window? owner,
        string title,
        string warning,
        string deleteAnywayLabel,
        string backLabel)
    {
        var dlg = new EmployeeDeleteAdminWarningDialog(title, warning, deleteAnywayLabel, backLabel);
        if (owner is not null)
            dlg.Owner = owner;
        return dlg.ShowDialog() == true;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); }
            catch { /* HWND not ready */ }
        }
    }

    private void DeleteAnyway_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
