using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Views;

public partial class ReservationsView : UserControl
{
    public ReservationsView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is ReservationsViewModel vm)
                await vm.InitializeAsync();
        }
        catch (Exception ex)
        {
            Log("ReservationsView Loaded error: " + ex);
        }
    }

    private static void Log(string message)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EliteRestaurantPro",
                "logs");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "reservations-debug.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // ignore logging errors
        }
    }
}
