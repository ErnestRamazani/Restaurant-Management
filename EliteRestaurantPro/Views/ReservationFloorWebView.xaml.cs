using System.Windows;
using System.Windows.Controls;
using EliteRestaurantPro.ViewModels;
using Microsoft.Web.WebView2.Wpf;

namespace EliteRestaurantPro.Views;

public partial class ReservationFloorWebView : UserControl
{
    private string? _documentCreatedScriptId;
    private ReservationFloorWebViewModel? _vm;

    public ReservationFloorWebView()
    {
        InitializeComponent();
        Loaded += (_, _) => _ = OnLoadedAsync();
        Unloaded += (_, _) => DetachViewModel();
        DataContextChanged += (_, _) => AttachViewModel();
        PasscodeBox.PasswordChanged += PasscodeBoxOnPasswordChanged;
    }

    private void PasscodeBoxOnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReservationFloorWebViewModel vm)
            vm.Passcode = PasscodeBox.Password;
    }

    private void AttachViewModel()
    {
        DetachViewModel();
        if (DataContext is ReservationFloorWebViewModel vm)
        {
            _vm = vm;
            vm.FloorSessionTokenReady += OnFloorSessionTokenReady;
        }
    }

    private void DetachViewModel()
    {
        if (_vm is not null)
        {
            _vm.FloorSessionTokenReady -= OnFloorSessionTokenReady;
            _vm = null;
        }
    }

    private async Task OnLoadedAsync()
    {
        AttachViewModel();
        if (_vm is null)
            return;

        try
        {
            await Browser.EnsureCoreWebView2Async().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Microsoft Edge WebView2 Runtime is required for the reservation floor.\n\n" + ex.Message,
                "Reservation floor",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _vm.OnWebViewHostReady();
    }

    private async void OnFloorSessionTokenReady(object? sender, string token)
    {
        if (sender is not ReservationFloorWebViewModel vm)
            return;

        try
        {
            await ApplyTokenAndNavigateAsync(vm, token).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Could not open the reservation floor.\n\n" + ex.Message,
                "Reservation floor",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task ApplyTokenAndNavigateAsync(ReservationFloorWebViewModel vm, string token)
    {
        var core = Browser.CoreWebView2;
        if (core is null)
            return;

        var trimmed = (token ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return;

        if (_documentCreatedScriptId is not null)
        {
            core.RemoveScriptToExecuteOnDocumentCreated(_documentCreatedScriptId);
            _documentCreatedScriptId = null;
        }

        var script = ReservationFloorWebViewModel.SessionStorageTokenScript(trimmed);
        _documentCreatedScriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(script).ConfigureAwait(true);
        var url = vm.FloorPageUrl;
        if (!string.IsNullOrWhiteSpace(url))
            core.Navigate(url);
    }
}
