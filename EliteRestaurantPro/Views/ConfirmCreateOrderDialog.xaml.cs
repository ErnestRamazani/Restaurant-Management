using System.Windows;
using System.Windows.Input;

namespace EliteRestaurantPro.Views;

public partial class ConfirmCreateOrderDialog : Window
{
    public ConfirmCreateOrderDialog(
        bool isTabletStaffFlow,
        string headlineQuestion,
        string detailsBlock)
    {
        InitializeComponent();

        TitleHeading.Text = isTabletStaffFlow ? "Send to cashier" : "Confirm create order";
        QuestionText.Text = headlineQuestion;
        DetailsText.Text = detailsBlock;
        ConfirmButton.Content = isTabletStaffFlow ? "Send to cashier" : "Create order";
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // HWND not ready — ignore.
            }
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
