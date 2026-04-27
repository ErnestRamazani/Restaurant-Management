using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Views;

public partial class EmployeesView : UserControl
{
    public EmployeesView()
    {
        InitializeComponent();
    }

    private void EmployeesView_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is EmployeesViewModel oldVm)
            oldVm.PropertyChanged -= VmOnPropertyChanged;
        if (e.NewValue is EmployeesViewModel newVm)
            newVm.PropertyChanged += VmOnPropertyChanged;
    }

    private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not EmployeesViewModel vm || e.PropertyName != nameof(EmployeesViewModel.IsDialogOpen))
            return;
        if (vm.IsDialogOpen)
            PinPasswordBox.Password = string.Empty;
    }

    private void PinPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is EmployeesViewModel vm)
            vm.PinCode = ((PasswordBox)sender).Password;
    }
}
