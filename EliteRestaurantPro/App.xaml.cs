using System.Windows;
using System.IO;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.Utils;
using EliteRestaurantPro.Views;
using Npgsql;
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
        // Do not set Npgsql.EnableLegacyTimestampBehavior — use UTC in the model (AppDbContext value converters).

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

    private static bool EnsureDatabaseConnectionConfigured()
    {
        if (AppDbContext.TryGetPostgreSqlConnectionString(out var configuredConnectionString)
            && IsCloudDatabaseTarget(configuredConnectionString))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            MessageBox.Show(
                "The desktop app is now configured to use the cloud PostgreSQL database only.\n\n" +
                "The current saved database target appears to be local, so it will not be used for live operations. " +
                "Please enter the DigitalOcean PostgreSQL connection details.",
                "Cloud Database Required",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        var dialog = new DatabasePipeSetupDialog(GetCloudDatabasePipeHint());
        if (Current.MainWindow != null)
            dialog.Owner = Current.MainWindow;
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.PipeInput))
        {
            MessageBox.Show(
                "A cloud PostgreSQL connection is required. Set DATABASE_URL / ELITE_POSTGRES_CONNECTION, or complete this dialog.",
                "Database Setup Required",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        var input = dialog.PipeInput;
        if (!PostgresBootstrapPipe.TryParse(input.Trim(), out var host, out var port, out var database, out var user, out var password))
            return false;

        if (IsLocalDatabaseHost(host))
        {
            MessageBox.Show(
                "Local PostgreSQL is no longer used for live restaurant data. Please enter the DigitalOcean PostgreSQL host.",
                "Cloud Database Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        if (!string.IsNullOrEmpty(password) && !DatabaseConnectionSecret.IsDpapiAvailable)
        {
            MessageBox.Show(
                "Cannot store a password on this OS. Use ELITE_POSTGRES_CONNECTION, or leave the password segment empty for trust auth.",
                "Database Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        var settings = SettingsManager.Load();
        ApplyBootstrapDatabaseSettings(settings.Database, host, port, database, user, password);
        SettingsManager.Save(settings);
        return true;
    }

    private static string GetCloudDatabasePipeHint() =>
        "elite-restaurant-db-postgresql-er4124-do-user-36989587-0.f.db.ondigitalocean.com|25060|defaultdb|doadmin|";

    private static bool IsCloudDatabaseTarget(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return !IsLocalDatabaseHost(builder.Host);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsLocalDatabaseHost(string? host)
    {
        host = (host ?? string.Empty).Trim();
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
               || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyBootstrapDatabaseSettings(
        DatabaseSettings db,
        string host,
        int port,
        string database,
        string user,
        string password)
    {
        db.Provider = "PostgreSql";
        db.PostgreSqlHost = host;
        db.PostgreSqlPort = port;
        db.PostgreSqlDatabase = database;
        db.PostgreSqlUsername = user;
        if (!string.IsNullOrEmpty(password) && DatabaseConnectionSecret.IsDpapiAvailable)
            db.PostgreSqlPasswordProtected = DatabaseConnectionSecret.ProtectUtf8(password);
        else
            db.PostgreSqlPasswordProtected = string.Empty;
        db.PostgreSqlConnectionString = null;
    }

    private static bool TryInitializeDatabase()
    {
        while (true)
        {
            try
            {
                DatabaseInitializer.Initialize();
                return true;
            }
            catch (Exception ex)
            {
                if (IsEfMigrationDuplicateTableError(ex))
                {
                    MessageBox.Show(
                        "Your database already has tables from a previous EliteRestaurant version, but EF Core migration history is missing.\n\n" +
                        "This is not a wrong password. Run the following once in pgAdmin: open your database → Tools → Query Tool, paste and execute:\n\n" +
                        DatabaseMigrationMetadata.BaselineInitialMigrationSql +
                        "\n\nThen click OK here to retry startup.",
                        "Baseline database migrations",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    continue;
                }

                var retry = MessageBox.Show(
                    BuildDatabaseConnectionFailureMessage(ex),
                    "PostgreSQL Connection Failed",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (retry != MessageBoxResult.Yes)
                    return false;

                var settings = SettingsManager.Load();
                var db = settings.Database ?? new DatabaseSettings();
                var defaultPipe = string.IsNullOrWhiteSpace(db.PostgreSqlHost) || IsLocalDatabaseHost(db.PostgreSqlHost)
                    ? GetCloudDatabasePipeHint()
                    : $"{db.PostgreSqlHost}|{db.PostgreSqlPort}|{db.PostgreSqlDatabase}|{db.PostgreSqlUsername}|";

                var setupDialog = new DatabasePipeSetupDialog(defaultPipe);
                if (Current.MainWindow != null)
                    setupDialog.Owner = Current.MainWindow;
                if (setupDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(setupDialog.PipeInput))
                    return false;

                var updated = setupDialog.PipeInput;
                if (!PostgresBootstrapPipe.TryParse(updated.Trim(), out var host, out var port, out var database, out var user, out var password))
                {
                    MessageBox.Show(
                        "Invalid format. Use: host|port|database|username|password",
                        "Database Setup",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    continue;
                }

                if (IsLocalDatabaseHost(host))
                {
                    MessageBox.Show(
                        "Local PostgreSQL is no longer used for live restaurant data. Please enter the DigitalOcean PostgreSQL host.",
                        "Cloud Database Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    continue;
                }

                if (string.IsNullOrEmpty(password) && IndicatesPostgreSqlPasswordRequired(ex))
                {
                    MessageBox.Show(
                        "No password was entered after the last |, but this server requires password authentication (SCRAM).\n\n" +
                        "Enter the full line again with your password as the last segment, for example:\n" +
                        "localhost|5432|elite_restaurant|postgres|your_password",
                        "Password Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    continue;
                }

                if (!string.IsNullOrEmpty(password) && !DatabaseConnectionSecret.IsDpapiAvailable)
                {
                    MessageBox.Show(
                        "Cannot store a password on this OS. Use ELITE_POSTGRES_CONNECTION or leave password empty for trust auth.",
                        "Database Setup",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    continue;
                }

                ApplyBootstrapDatabaseSettings(db, host, port, database, user, password);
                settings.Database = db;
                SettingsManager.Save(settings);

                if (!AppDbContext.TryGetPostgreSqlConnectionString(out var cs))
                {
                    MessageBox.Show(
                        "Could not build a connection string from the entered values.",
                        "PostgreSQL",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    continue;
                }

                if (!CanConnect(cs, out var error))
                {
                    MessageBox.Show(
                        $"Connection test failed.\n\n{error}",
                        "PostgreSQL Test Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    continue;
                }
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

    private static string BuildDatabaseConnectionFailureMessage(Exception ex)
    {
        var baseMsg = ex.GetBaseException().Message ?? ex.Message;
        var body =
            $"Database connection failed.\n\n{baseMsg}\n\nClick Yes to update connection string, or No to exit.";
        if (!IndicatesPostgreSqlPasswordRequired(ex))
            return body;

        return body +
               "\n\n— If you see SCRAM or SASL/password errors: the app must store your PostgreSQL user password. " +
               "On Yes, put it as the last segment: host|port|database|username|password\n\n" +
               "Or set environment variables ELITE_DB_PROVIDER=PostgreSql and ELITE_POSTGRES_CONNECTION " +
               "(full Npgsql connection string including Password=…).";
    }

    private static bool IsEfMigrationDuplicateTableError(Exception ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
        {
            if (e is PostgresException pg && pg.SqlState == "42P07")
                return true;
            var msg = e.Message ?? string.Empty;
            if (msg.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                && msg.Contains("relation", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IndicatesPostgreSqlPasswordRequired(Exception ex)
    {
        var m = ex.GetBaseException().Message ?? string.Empty;
        return m.Contains("password", StringComparison.OrdinalIgnoreCase)
               || m.Contains("SCRAM", StringComparison.OrdinalIgnoreCase)
               || m.Contains("SASL", StringComparison.OrdinalIgnoreCase)
               || m.Contains("28P01", StringComparison.OrdinalIgnoreCase);
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
