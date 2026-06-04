using System.Windows;
using System.Windows.Input;
using EliteRestaurantPro.Localization;

namespace EliteRestaurantPro.Views;

public sealed class ConfirmCreateOrderDialogViewModel
{
    public string DialogTitle { get; }
    public string QuestionText { get; }
    public string DetailsText { get; }
    public string SubmitPromptLabel { get; }
    public string ConfirmButtonLabel { get; }
    public string CancelButtonLabel { get; }

    public ConfirmCreateOrderDialogViewModel(bool isTabletStaffFlow, string headlineQuestion, string detailsBlock)
    {
        DialogTitle = CreateOrderUiLocalizer.ConfirmDialogTitle(isTabletStaffFlow);
        QuestionText = headlineQuestion;
        DetailsText = detailsBlock;
        SubmitPromptLabel = Loc.Admin("createOrderDlgSubmitTicket", "Submit this ticket?");
        ConfirmButtonLabel = CreateOrderUiLocalizer.ConfirmDialogPrimaryButton(isTabletStaffFlow);
        CancelButtonLabel = Loc.Admin("createOrderDlgCancel", "Cancel");
    }
}

public partial class ConfirmCreateOrderDialog : Window
{
    public ConfirmCreateOrderDialog(bool isTabletStaffFlow, string headlineQuestion, string detailsBlock)
    {
        DataContext = new ConfirmCreateOrderDialogViewModel(isTabletStaffFlow, headlineQuestion, detailsBlock);
        InitializeComponent();
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
