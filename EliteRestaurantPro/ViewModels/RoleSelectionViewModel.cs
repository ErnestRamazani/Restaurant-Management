using System.Windows.Input;
using EliteRestaurantPro.Utils;

namespace EliteRestaurantPro.ViewModels;

public class RoleSelectionViewModel : BaseViewModel
{
    public ICommand SelectAdminCommand { get; }
    public ICommand SelectServerCommand { get; }
    public ICommand SelectCashierCommand { get; }
    public ICommand SelectKitchenBarCommand { get; }

    public RoleSelectionViewModel(Action<BaseViewModel> navigate)
    {
        AppSession.Clear();
        SelectAdminCommand = new RelayCommand(_ =>
            navigate(new AdminLoginViewModel(navigate)));
        SelectServerCommand = new RelayCommand(_ =>
            navigate(new StaffLoginViewModel(navigate, StaffPortalKind.Server)));
        SelectCashierCommand = new RelayCommand(_ =>
            navigate(new StaffLoginViewModel(navigate, StaffPortalKind.Cashier)));
        SelectKitchenBarCommand = new RelayCommand(_ =>
            navigate(new StaffLoginViewModel(navigate, StaffPortalKind.KitchenBar)));
    }
}
