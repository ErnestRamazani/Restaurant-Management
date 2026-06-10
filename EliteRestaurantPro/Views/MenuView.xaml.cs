using System.Windows.Controls;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Views;

public partial class MenuView : UserControl
{
    public MenuView()
    {
        InitializeComponent();
    }

    private void IngredientQuantityBox_OnLostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: InventorySelectionItemViewModel item })
            item.CommitQuantityFromText();
    }
}
