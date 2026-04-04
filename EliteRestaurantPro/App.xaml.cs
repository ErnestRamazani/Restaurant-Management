using System.Windows;
using System.IO;
using EliteRestaurantPro.Data;
using EliteRestaurantPro.Utils;
using QuestPDF.Infrastructure;

namespace EliteRestaurantPro;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            LogUnhandledException("DispatcherUnhandledException", args.Exception);
            MessageBox.Show(
                args.Exception.ToString(),
                "Application Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                LogUnhandledException("AppDomainUnhandledException", ex);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogUnhandledException("TaskSchedulerUnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        QuestPDF.Settings.License = LicenseType.Community;
        AppDbContext.Initialize();
        ThemeManager.ApplySavedPalette();
        base.OnStartup(e);
    }

    private static void LogUnhandledException(string source, Exception exception)
    {
        try
        {
            var appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EliteRestaurantPro",
                "logs");
            Directory.CreateDirectory(appFolder);
            var path = Path.Combine(appFolder, "app-crash.log");
            File.AppendAllText(
                path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}{Environment.NewLine}{exception}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}");
        }
        catch
        {
            // Ignore logging failures.
        }
    }
}
