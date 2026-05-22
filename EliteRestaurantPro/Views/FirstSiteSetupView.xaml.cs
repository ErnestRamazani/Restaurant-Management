using System.Windows.Controls;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Views;

public partial class FirstSiteSetupView : UserControl
{
    public FirstSiteSetupView() => InitializeComponent();

    private void AdminPinBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is FirstSiteSetupViewModel vm)
            vm.AdminPin = AdminPinBox.Password;
    }

    private void ConfirmPinBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is FirstSiteSetupViewModel vm)
            vm.ConfirmPin = ConfirmPinBox.Password;
    }

    private void SetupSecretBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is FirstSiteSetupViewModel vm)
            vm.SetupPlatformSecret = SetupSecretBox.Password;
    }
}
