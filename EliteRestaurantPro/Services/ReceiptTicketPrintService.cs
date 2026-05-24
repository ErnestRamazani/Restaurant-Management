using System.Diagnostics;
using System.IO;
using System.Printing;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace EliteRestaurantPro.Services;

/// <summary>Prints receipt PDFs to a named Windows printer queue (no default printer required).</summary>
public static class ReceiptTicketPrintService
{
    public static void Print(byte[] pdfBytes, string? printerName, string documentName)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new InvalidOperationException("Receipt PDF is empty.");

        var bytes = pdfBytes.ToArray();
        var printer = ResolvePrinterName(printerName);
        var docName = string.IsNullOrWhiteSpace(documentName) ? "Elite Receipt" : documentName.Trim();

        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("Printing requires the WPF application.");

        _ = dispatcher.InvokeAsync(async () =>
        {
            Exception? failure = null;
            try
            {
                if (await TryWebView2PrintAsync(bytes, docName, printer).ConfigureAwait(true))
                    return;

                if (!string.IsNullOrWhiteSpace(printer) && TryCmdPrint(bytes, docName, printer))
                    return;

                if (TryShellPrint(bytes, docName))
                    return;

                failure = new InvalidOperationException(BuildPrinterHelpMessage(printer));
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            if (failure is null)
                return;

            MessageBox.Show(
                failure.GetBaseException().Message,
                "Print receipt",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        });
    }

    public static IReadOnlyList<string> GetInstalledPrinterNames()
    {
        try
        {
            using var server = new LocalPrintServer();
            return server.GetPrintQueues()
                .Select(q => q.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static string? ResolvePrinterName(string? configuredName)
    {
        var configured = (configuredName ?? string.Empty).Trim();
        if (configured.Length > 0 && PrinterExists(configured))
            return configured;

        var auto = GetInstalledPrinterNames()
            .FirstOrDefault(n => n.Contains("EliteRestaurant", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(auto))
            return auto;

        return configured.Length > 0 ? configured : null;
    }

    private static bool PrinterExists(string printerName)
    {
        return GetInstalledPrinterNames().Any(n => string.Equals(n, printerName, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<bool> TryWebView2PrintAsync(byte[] pdfBytes, string documentName, string? printerName)
    {
        var dir = Path.Combine(Path.GetTempPath(), "EliteRestaurant", "print");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, pdfBytes).ConfigureAwait(true);

        Window? host = null;
        WebView2? webView = null;
        try
        {
            host = new Window
            {
                Width = 1,
                Height = 1,
                Left = -20000,
                Top = -20000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Visibility = Visibility.Hidden,
                Title = documentName
            };

            webView = new WebView2();
            host.Content = webView;
            host.Show();

            await webView.EnsureCoreWebView2Async().ConfigureAwait(true);

            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                webView.NavigationCompleted -= OnNavigationCompleted;
                if (!e.IsSuccess)
                {
                    done.TrySetResult(false);
                    return;
                }

                _ = PrintLoadedPdfAsync(webView, printerName, done);
            }

            webView.NavigationCompleted += OnNavigationCompleted;
            webView.Source = new Uri(path);

            return await done.Task.WaitAsync(TimeSpan.FromSeconds(90)).ConfigureAwait(true);
        }
        catch
        {
            return false;
        }
        finally
        {
            host?.Close();
            TryDeleteFile(path);
        }
    }

    private static async Task PrintLoadedPdfAsync(WebView2 webView, string? printerName, TaskCompletionSource<bool> done)
    {
        try
        {
            await Task.Delay(400).ConfigureAwait(true);
            var core = webView.CoreWebView2;
            if (core is null)
            {
                done.TrySetResult(false);
                return;
            }

            var settings = core.Environment.CreatePrintSettings();
            settings.ShouldPrintBackgrounds = true;
            if (!string.IsNullOrWhiteSpace(printerName))
                settings.PrinterName = printerName;

            await core.PrintAsync(settings).ConfigureAwait(true);
            done.TrySetResult(true);
        }
        catch
        {
            done.TrySetResult(false);
        }
    }

    private static bool TryCmdPrint(byte[] pdfBytes, string documentName, string printerName)
    {
        try
        {
            var path = WriteTempPdf(pdfBytes, documentName);
            var escapedPrinter = printerName.Replace("\"", "\\\"", StringComparison.Ordinal);
            var escapedPath = path.Replace("\"", "\\\"", StringComparison.Ordinal);
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c print /D:\"{escapedPrinter}\" \"{escapedPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });

            if (process is null)
                return false;

            process.WaitForExit(8000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryShellPrint(byte[] pdfBytes, string documentName)
    {
        try
        {
            var path = WriteTempPdf(pdfBytes, documentName);
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                Verb = "print",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            return process is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string WriteTempPdf(byte[] pdfBytes, string documentName)
    {
        var dir = Path.Combine(Path.GetTempPath(), "EliteRestaurant", "print");
        Directory.CreateDirectory(dir);
        var safeName = documentName;
        foreach (var c in Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(c, '_');

        var path = Path.Combine(dir, $"{safeName}-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, pdfBytes);
        return path;
    }

    private static string BuildPrinterHelpMessage(string? printerName)
    {
        var installed = GetInstalledPrinterNames();
        var list = installed.Count > 0
            ? string.Join("\n", installed.Select(n => "  • " + n))
            : "  (none found)";

        var target = string.IsNullOrWhiteSpace(printerName)
            ? "No receipt printer is configured."
            : $"Printer \"{printerName}\" was not found or could not print.";

        return target + "\n\nInstalled printers:\n" + list
            + "\n\nOpen Settings → Appearance → Tickets & receipts, choose your receipt printer, and save.";
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }
}
