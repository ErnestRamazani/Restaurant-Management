using System.Windows;
using System.Windows.Controls;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Views;

public partial class AppearanceSettingsView : UserControl
{
    public AppearanceSettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppearanceSettingsViewModel vm)
        {
            vm.NotifyClearDatabasePassword -= ClearDatabasePasswordBox;
            vm.NotifyClearDatabasePassword += ClearDatabasePasswordBox;
        }
    }

    private void ClearDatabasePasswordBox()
    {
        DatabasePasswordBox.Password = string.Empty;
    }

    private void DatabasePasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppearanceSettingsViewModel vm && sender is PasswordBox pb)
            vm.SetDatabasePasswordFromUi(pb.Password);
    }
}
