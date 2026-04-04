using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Views.Components;

public partial class AdminSidebar : UserControl
{
    public AdminSidebar()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyActiveState();
        DataContextChanged += (_, _) => ApplyActiveState();
    }

    private void ApplyActiveState()
    {
        if (DataContext is not AdminBaseViewModel viewModel)
        {
            return;
        }

        ResetButton(DashboardButton);
        ResetButton(EmployeesButton);
        ResetButton(MenuButton);
        ResetButton(InventoryButton);
        ResetButton(AttendanceButton);
        ResetButton(KitchenQueueButton);
        ResetButton(TablesButton);
        ResetButton(ServerPickupButton);
        ResetButton(OrdersButton);
        ResetButton(CreateOrderButton);
        ResetButton(MoneyButton);
        ResetButton(ReportsButton);
        ResetButton(AppearanceButton);

        switch (viewModel.ActivePage)
        {
            case "Dashboard":
                ActivateButton(DashboardButton);
                break;
            case "Employees":
                ActivateButton(EmployeesButton);
                break;
            case "Menu":
                ActivateButton(MenuButton);
                break;
            case "Inventory":
                ActivateButton(InventoryButton);
                break;
            case "Attendance":
                ActivateButton(AttendanceButton);
                break;
            case "Tables":
                ActivateButton(TablesButton);
                break;
            case "KitchenQueue":
                ActivateButton(KitchenQueueButton);
                break;
            case "ServerPickup":
                ActivateButton(ServerPickupButton);
                break;
            case "Orders":
                ActivateButton(OrdersButton);
                break;
            case "CreateOrder":
                ActivateButton(CreateOrderButton);
                break;
            case "Money":
                ActivateButton(MoneyButton);
                break;
            case "Reports":
                ActivateButton(ReportsButton);
                break;
            case "AppearanceSettings":
                ActivateButton(AppearanceButton);
                break;
        }
    }

    private void ResetButton(Button button)
    {
        button.Style = (Style)FindResource("SidebarNavButton");
        button.Foreground = (Brush)FindResource("TextSecondaryBrush");
    }

    private void ActivateButton(Button button)
    {
        button.Style = (Style)FindResource("SidebarNavButtonActive");
        button.Foreground = (Brush)FindResource("GoldAccentBrush");
    }
}
