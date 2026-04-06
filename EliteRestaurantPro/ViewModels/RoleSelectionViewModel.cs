using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using EliteRestaurantPro.Utils;

namespace EliteRestaurantPro.ViewModels;

public class RoleSelectionViewModel : BaseViewModel
{
    private string _roleHeaderRestaurantName = "Elite Restaurant";
    private ImageSource? _roleHeaderLogoImage;
    private ImageSource? _roleHeaderBackgroundImage;
    private string _roleHeaderAddressText = "Address: Add your business address in Settings";
    private string _roleHeaderWebsiteText = "Website: yourdomain.com";
    private string _roleHeaderSocialText = "Social: @yourbrand";

    public string RoleHeaderRestaurantName
    {
        get => _roleHeaderRestaurantName;
        private set => SetField(ref _roleHeaderRestaurantName, value);
    }

    public ImageSource? RoleHeaderLogoImage
    {
        get => _roleHeaderLogoImage;
        private set => SetField(ref _roleHeaderLogoImage, value);
    }

    public ImageSource? RoleHeaderBackgroundImage
    {
        get => _roleHeaderBackgroundImage;
        private set => SetField(ref _roleHeaderBackgroundImage, value);
    }

    public string RoleHeaderAddressText
    {
        get => _roleHeaderAddressText;
        private set => SetField(ref _roleHeaderAddressText, value);
    }

    public string RoleHeaderWebsiteText
    {
        get => _roleHeaderWebsiteText;
        private set => SetField(ref _roleHeaderWebsiteText, value);
    }

    public string RoleHeaderSocialText
    {
        get => _roleHeaderSocialText;
        private set => SetField(ref _roleHeaderSocialText, value);
    }

    public bool RoleHeaderHasLogo => RoleHeaderLogoImage is not null;
    public bool RoleHeaderHasBackgroundImage => RoleHeaderBackgroundImage is not null;

    public ICommand SelectAdminCommand { get; }
    public ICommand SelectServerCommand { get; }
    public ICommand SelectCashierCommand { get; }
    public ICommand SelectKitchenBarCommand { get; }

    public RoleSelectionViewModel(Action<BaseViewModel> navigate)
    {
        AppSession.Clear();
        LoadBrandingFromSettings();
        SelectAdminCommand = new RelayCommand(_ =>
            navigate(new AdminLoginViewModel(navigate)));
        SelectServerCommand = new RelayCommand(_ =>
            navigate(new StaffLoginViewModel(navigate, StaffPortalKind.Server)));
        SelectCashierCommand = new RelayCommand(_ =>
            navigate(new StaffLoginViewModel(navigate, StaffPortalKind.Cashier)));
        SelectKitchenBarCommand = new RelayCommand(_ =>
            navigate(new StaffLoginViewModel(navigate, StaffPortalKind.KitchenBar)));
    }

    private void LoadBrandingFromSettings()
    {
        var business = SettingsManager.Load().BusinessProfile;
        RoleHeaderRestaurantName = string.IsNullOrWhiteSpace(business.RestaurantName)
            ? "Elite Restaurant"
            : business.RestaurantName.Trim();

        var address = string.IsNullOrWhiteSpace(business.Address)
            ? "Add your business address in Settings"
            : business.Address.Trim();
        RoleHeaderAddressText = $"Address: {address}";

        var website = string.IsNullOrWhiteSpace(business.WebsiteDomain)
            ? "yourdomain.com"
            : business.WebsiteDomain.Trim();
        RoleHeaderWebsiteText = $"Website: {website}";

        var social = string.IsNullOrWhiteSpace(business.SocialMedia)
            ? "@yourbrand"
            : business.SocialMedia.Trim();
        RoleHeaderSocialText = $"Social: {social}";

        RoleHeaderLogoImage = TryLoadImage(business.LogoPath);
        RoleHeaderBackgroundImage = TryLoadImage(business.HomepageBackgroundImagePath);
        OnPropertyChanged(nameof(RoleHeaderHasLogo));
        OnPropertyChanged(nameof(RoleHeaderHasBackgroundImage));
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
