using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EliteRestaurantPro.Utils;

namespace EliteRestaurantPro.ViewModels;

public class MainViewModel : BaseViewModel
{
    private BaseViewModel _currentViewModel = null!;
    private ImageSource? _menuBackgroundImage;
    private double _menuBackgroundDimOpacity = 0.18;
    private double _menuBackgroundImageOpacity = 0.22;

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

    public MainViewModel()
    {
        SettingsManager.SettingsChanged += OnSettingsChanged;
        Navigate(new RoleSelectionViewModel(Navigate));
    }

    public void Navigate(BaseViewModel viewModel)
    {
        CurrentViewModel = viewModel;
    }

    private void OnSettingsChanged()
    {
        UpdateBackgroundForCurrentView();
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
