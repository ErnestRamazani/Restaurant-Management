using System.Windows;
using System.IO;
using EliteRestaurantPro.Data;
using EliteRestaurantPro.Utils;
using Microsoft.VisualBasic;
using Npgsql;
using QuestPDF.Infrastructure;

namespace EliteRestaurantPro;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Compatibility switch while migrating legacy local DateTime usage to PostgreSQL.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

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
        if (TryHandleImportMode(e))
        {
            Shutdown();
            return;
        }

        if (!EnsureDatabaseConnectionConfigured())
        {
            Shutdown();
            return;
        }

        if (!TryInitializeDatabase())
        {
            Shutdown();
            return;
        }

        ThemeManager.ApplySavedPalette();
        base.OnStartup(e);
    }

    private static bool TryHandleImportMode(StartupEventArgs e)
    {
        var importRequested = false;
        foreach (var arg in e.Args)
        {
            if (arg.Equals("--import-sqlite-now", StringComparison.OrdinalIgnoreCase))
            {
                importRequested = true;
                break;
            }
        }

        if (!importRequested)
            return false;

        if (!EnsureDatabaseConnectionConfigured())
            return true;

        var silent = string.Equals(
            Environment.GetEnvironmentVariable("ELITE_IMPORT_SILENT"),
            "1",
            StringComparison.OrdinalIgnoreCase);

        var ok = AppDbContext.ImportLegacySqliteIntoPostgreSql(out var message);
        if (!silent)
        {
            MessageBox.Show(
                message,
                ok ? "SQLite Import Complete" : "SQLite Import Failed",
                MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        return true;
    }

    private static bool EnsureDatabaseConnectionConfigured()
    {
        var settings = SettingsManager.Load();
        settings.Database.Provider = "PostgreSql";
        var current = settings.Database.PostgreSqlConnectionString?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(current))
            return true;

        var input = Interaction.InputBox(
            "Enter PostgreSQL connection string to start EliteRestaurantPro.",
            "Database Setup",
            "Host=localhost;Port=5432;Database=elite_restaurant;Username=postgres;Password=postgres");

        if (string.IsNullOrWhiteSpace(input))
        {
            MessageBox.Show(
                "A PostgreSQL connection string is required to launch the app.",
                "Database Setup Required",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        settings.Database.PostgreSqlConnectionString = input.Trim();
        SettingsManager.Save(settings);
        return true;
    }

    private static bool TryInitializeDatabase()
    {
        while (true)
        {
            try
            {
                AppDbContext.Initialize();
                return true;
            }
            catch (Exception ex)
            {
                var retry = MessageBox.Show(
                    $"Database connection failed.\n\n{ex.Message}\n\nClick Yes to update connection string, or No to exit.",
                    "PostgreSQL Connection Failed",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (retry != MessageBoxResult.Yes)
                    return false;

                var settings = SettingsManager.Load();
                var updated = Interaction.InputBox(
                    "Update PostgreSQL connection string.",
                    "Database Setup",
                    settings.Database.PostgreSqlConnectionString ?? string.Empty);

                if (string.IsNullOrWhiteSpace(updated))
                    return false;

                if (!CanConnect(updated.Trim(), out var error))
                {
                    MessageBox.Show(
                        $"Connection test failed.\n\n{error}",
                        "PostgreSQL Test Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    continue;
                }

                settings.Database.Provider = "PostgreSql";
                settings.Database.PostgreSqlConnectionString = updated.Trim();
                SettingsManager.Save(settings);
            }
        }
    }

    private static bool CanConnect(string connectionString, out string error)
    {
        try
        {
            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT 1;", conn);
            _ = cmd.ExecuteScalar();
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
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
