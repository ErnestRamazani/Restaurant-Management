using System.Windows.Controls;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Views;

public partial class AdminLoginView : UserControl
{
    public AdminLoginView() => InitializeComponent();

    private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AdminLoginViewModel vm)
            vm.Password = ((PasswordBox)sender).Password;
    }
}
