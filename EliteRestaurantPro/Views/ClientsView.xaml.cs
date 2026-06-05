using System.ComponentModel;
using System.Windows.Controls;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Views;

public partial class ClientsView : UserControl
{
    public ClientsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => HookViewModel(DataContext as ClientsViewModel);
    }

    private void HookViewModel(ClientsViewModel? vm)
    {
        if (vm is null)
            return;
        vm.PropertyChanged -= ViewModel_OnPropertyChanged;
        vm.PropertyChanged += ViewModel_OnPropertyChanged;
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClientsViewModel.IsSettleDialogOpen)
            && sender is ClientsViewModel vm
            && vm.IsSettleDialogOpen)
        {
            SettlePasscodeBox.Password = string.Empty;
        }
    }

    private void SettlePasscodeBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ClientsViewModel vm && sender is PasswordBox pb)
            vm.SettlePasscode = pb.Password;
    }
}
