using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using EliteRestaurant.Core.Staff;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.Localization;

namespace EliteRestaurantPro.ViewModels;

public class RoleSelectionViewModel : LocalizableViewModel
{
    private string _roleHeaderRestaurantName = string.Empty;
    private ImageSource? _roleHeaderLogoImage;
    private ImageSource? _roleHeaderBackgroundImage;
    private string _roleHeaderAddressText = string.Empty;
    private string _roleHeaderWebsiteText = string.Empty;
    private string _roleHeaderSocialText = string.Empty;

    public string RoleAdminTitle => Loc.Auth("roleSelectAdminTitle", "Admin");
    public string RoleAdminDesc => Loc.Auth("roleSelectAdminDesc", "Full management access");
    public string RoleCashierTitle => Loc.Auth("roleSelectCashierTitle", "Cashier");
    public string RoleCashierDesc => Loc.Auth("roleSelectCashierDesc", "Validate orders and handoff");
    public string RoleServerTitle => Loc.Auth("roleSelectServerTitle", "Server");
    public string RoleServerDesc => Loc.Auth("roleSelectServerDesc", "Send requests to cashier");
    public string RoleKitchenBarTitle => Loc.Auth("roleSelectKitchenBarTitle", "Kitchen / Bar");
    public string RoleKitchenBarDesc => Loc.Auth("roleSelectKitchenBarDesc", "Prep queue and pickup");

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

    protected override void RefreshLocalizedStrings()
    {
        Notify(
            nameof(RoleAdminTitle),
            nameof(RoleAdminDesc),
            nameof(RoleCashierTitle),
            nameof(RoleCashierDesc),
            nameof(RoleServerTitle),
            nameof(RoleServerDesc),
            nameof(RoleKitchenBarTitle),
            nameof(RoleKitchenBarDesc));
        LoadBrandingFromSettings();
    }

    private void LoadBrandingFromSettings()
    {
        var business = SettingsManager.Load().BusinessProfile;
        var defaultName = Loc.Auth("roleSelectDefaultRestaurantName", "Elite Restaurant");
        RoleHeaderRestaurantName = string.IsNullOrWhiteSpace(business.RestaurantName)
            ? defaultName
            : business.RestaurantName.Trim();

        var addressLabel = Loc.Auth("roleSelectAddressLabel", "Address");
        var address = string.IsNullOrWhiteSpace(business.Address)
            ? Loc.Auth("roleSelectAddressPlaceholder", "Add your business address in Settings")
            : business.Address.Trim();
        RoleHeaderAddressText = $"{addressLabel}: {address}";

        var websiteLabel = Loc.Auth("roleSelectWebsiteLabel", "Website");
        var website = string.IsNullOrWhiteSpace(business.WebsiteDomain)
            ? Loc.Auth("roleSelectWebsitePlaceholder", "yourdomain.com")
            : business.WebsiteDomain.Trim();
        RoleHeaderWebsiteText = $"{websiteLabel}: {website}";

        var socialLabel = Loc.Auth("roleSelectSocialLabel", "Social");
        var social = string.IsNullOrWhiteSpace(business.SocialMedia)
            ? Loc.Auth("roleSelectSocialPlaceholder", "@yourbrand")
            : business.SocialMedia.Trim();
        RoleHeaderSocialText = $"{socialLabel}: {social}";

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
