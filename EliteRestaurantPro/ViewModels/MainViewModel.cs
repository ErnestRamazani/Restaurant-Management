using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Services;

namespace EliteRestaurantPro.ViewModels;

public class MainViewModel : BaseViewModel
{
    private static readonly HttpClient CloudStatusHttp = new();
    private BaseViewModel _currentViewModel = null!;
    private ImageSource? _menuBackgroundImage;
    private double _menuBackgroundDimOpacity = 0.18;
    private double _menuBackgroundImageOpacity = 0.22;
    private string _cloudStatusText = "Cloud: Checking";
    private Brush _cloudStatusBrush = Brushes.Gray;
    private string _syncStatusText = "Cloud DB only";
    private Brush _syncStatusBrush = Brushes.Gray;
    private readonly DispatcherTimer _cloudStatusTimer;

    public BaseViewModel CurrentViewModel
    {
        get => _currentViewModel;
        set
        {
            if (!SetField(ref _currentViewModel, value))
                return;
            UpdateBackgroundForCurrentView();
        }
    }

    public ImageSource? MenuBackgroundImage
    {
        get => _menuBackgroundImage;
        set => SetField(ref _menuBackgroundImage, value);
    }

    public double MenuBackgroundDimOpacity
    {
        get => _menuBackgroundDimOpacity;
        set => SetField(ref _menuBackgroundDimOpacity, value);
    }

    public double MenuBackgroundImageOpacity
    {
        get => _menuBackgroundImageOpacity;
        set => SetField(ref _menuBackgroundImageOpacity, value);
    }

    public bool HasMenuBackgroundImage => MenuBackgroundImage is not null;

    public string CloudStatusText
    {
        get => _cloudStatusText;
        private set => SetField(ref _cloudStatusText, value);
    }

    public Brush CloudStatusBrush
    {
        get => _cloudStatusBrush;
        private set => SetField(ref _cloudStatusBrush, value);
    }

    public string SyncStatusText
    {
        get => _syncStatusText;
        private set => SetField(ref _syncStatusText, value);
    }

    public Brush SyncStatusBrush
    {
        get => _syncStatusBrush;
        private set => SetField(ref _syncStatusBrush, value);
    }

    public MainViewModel()
    {
        SettingsManager.SettingsChanged += OnSettingsChanged;
        CloudFirstSyncService.StatusChanged += OnSyncStatusChanged;
        _cloudStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _cloudStatusTimer.Tick += async (_, _) => await RefreshCloudStatusAsync();
        Navigate(new RoleSelectionViewModel(Navigate));
        _cloudStatusTimer.Start();
        _ = InitializeNavigationAsync();
        _ = RefreshCloudStatusAsync();
        OnSyncStatusChanged();
    }

    private async Task InitializeNavigationAsync()
    {
        var settings = SettingsManager.Load();
        var baseUrl = CloudEndpoints.NormalizeApiBaseUrl(settings.CloudApi.BaseUrl);

        try
        {
            var status = await new SetupApiClient().GetStatusAsync(baseUrl);
            if (status is null)
            {
                // Wrong URL or offline — stay on sign-in; do not show empty-site wizard.
                if (CurrentViewModel is FirstSiteSetupViewModel)
                    Navigate(new RoleSelectionViewModel(Navigate));
                return;
            }

            if (!status.SetupRequired)
            {
                if (!settings.FirstSiteSetupCompleted)
                {
                    settings.FirstSiteSetupCompleted = true;
                    SettingsManager.Save(settings);
                }

                if (CurrentViewModel is FirstSiteSetupViewModel)
                    Navigate(new RoleSelectionViewModel(Navigate));
                return;
            }

            // Empty cloud database only — first restaurant not created yet.
            if (!settings.FirstSiteSetupCompleted)
                NavigateToFirstSiteSetup();
        }
        catch
        {
            if (CurrentViewModel is FirstSiteSetupViewModel)
                Navigate(new RoleSelectionViewModel(Navigate));
        }
    }

    private void NavigateToFirstSiteSetup()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
            dispatcher.Invoke(() => Navigate(new FirstSiteSetupViewModel(Navigate)));
        else
            Navigate(new FirstSiteSetupViewModel(Navigate));
    }

    public void Navigate(BaseViewModel viewModel)
    {
        CurrentViewModel = viewModel;
    }

    private void OnSettingsChanged()
    {
        UpdateBackgroundForCurrentView();
        _ = RefreshCloudStatusAsync();
    }

    private async Task RefreshCloudStatusAsync()
    {
        var baseUrl = CloudEndpoints.NormalizeApiBaseUrl(SettingsManager.Load().CloudApi.BaseUrl);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/health");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var response = await CloudStatusHttp.SendAsync(request, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                CloudStatusText = "Cloud: Online";
                CloudStatusBrush = Brushes.LimeGreen;
            }
            else
            {
                CloudStatusText = "Cloud: Offline";
                CloudStatusBrush = Brushes.IndianRed;
            }
        }
        catch
        {
            CloudStatusText = "Cloud: Offline";
            CloudStatusBrush = Brushes.IndianRed;
        }
    }

    private void OnSyncStatusChanged()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(OnSyncStatusChanged);
            return;
        }

        var pending = CloudFirstSyncService.PendingCount;
        SyncStatusText = pending == 0 ? "Cloud DB only" : $"Sync: {pending} pending";
        SyncStatusBrush = pending == 0 ? Brushes.LimeGreen : Brushes.Goldenrod;
    }

    private void UpdateBackgroundForCurrentView()
    {
        var appSettings = SettingsManager.Load();
        var settings = appSettings.NavigationBackgrounds;
        var activePage = (CurrentViewModel as AdminBaseViewModel)?.ActivePage ?? string.Empty;
        settings.PageImagePaths.TryGetValue(activePage, out var imagePath);
        var resolvedImage = TryLoadImage(imagePath);
        if (resolvedImage is null)
            resolvedImage = TryLoadImage(appSettings.BusinessProfile.HomepageBackgroundImagePath);
        MenuBackgroundImage = resolvedImage;

        var dim = Math.Clamp(settings.DimStrength, 0, 0.5);
        var contrast = Math.Clamp(settings.ContrastIntensity, 0, 0.5);
        if (MenuBackgroundImage is null)
        {
            MenuBackgroundDimOpacity = 0;
            MenuBackgroundImageOpacity = 0;
        }
        else
        {
            MenuBackgroundDimOpacity = Math.Clamp(dim, 0.12, 0.36);
            MenuBackgroundImageOpacity = Math.Clamp(0.2 + (contrast * 0.55), 0.2, 0.5);
        }
        OnPropertyChanged(nameof(HasMenuBackgroundImage));
    }

    private static ImageSource? TryLoadImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            var full = path.Trim();
            if (!Path.IsPathRooted(full))
                full = Path.GetFullPath(full);
            if (!File.Exists(full))
                return null;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(full, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
