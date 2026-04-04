using System.Windows.Controls;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Views;

public partial class StaffLoginView : UserControl
{
    public StaffLoginView() => InitializeComponent();

    private void PinInput_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is StaffLoginViewModel vm)
            vm.Pin = ((PasswordBox)sender).Password;
    }
}
