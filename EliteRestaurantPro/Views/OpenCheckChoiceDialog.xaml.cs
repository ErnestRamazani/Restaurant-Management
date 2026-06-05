using System.Windows;
using System.Windows.Input;
using EliteRestaurantPro.Localization;

namespace EliteRestaurantPro.Views;

public enum OpenCheckChoice
{
    Cancel,
    AppendToSameTicket,
    NewSeparateTicket,
}

public sealed class OpenCheckChoiceDialogViewModel
{
    public string DialogTitle { get; }
    public string DialogHeading { get; }
    public string SummaryText { get; }
    public string PromptText { get; }
    public string SubtotalText { get; }
    public string ChooseHowLabel { get; }
    public string AppendButtonLabel { get; }
    public string NewTicketButtonLabel { get; }
    public string BackButtonLabel { get; }

    public OpenCheckChoiceDialogViewModel(
        int tableNumber,
        string tableName,
        string checkCode,
        string? rawStatus,
        int newLineCount,
        decimal newLinesSubtotalUsd)
    {
        var name = string.IsNullOrWhiteSpace(tableName) ? $"{CreateOrderUiLocalizer.TableComboPrefix}{tableNumber}" : tableName;
        DialogTitle = CreateOrderUiLocalizer.OpenCheckDialogTitle;
        DialogHeading = CreateOrderUiLocalizer.OpenCheckDialogHeading;
        SummaryText = CreateOrderUiLocalizer.OpenCheckDialogSummary(name, checkCode, rawStatus);
        PromptText = CreateOrderUiLocalizer.OpenCheckNewLinesPrompt(newLineCount);
        SubtotalText = CreateOrderUiLocalizer.OpenCheckNewLinesSubtotal(newLinesSubtotalUsd);
        ChooseHowLabel = Loc.Admin("createOrderDlgChooseHow", "Choose how to send these items");
        AppendButtonLabel = Loc.Admin("createOrderDlgAppendTicket", "Add to this ticket");
        NewTicketButtonLabel = Loc.Admin("createOrderDlgNewSeparateTicket", "New separate ticket");
        BackButtonLabel = Loc.Admin("createOrderDlgBack", "Back");
    }
}

public partial class OpenCheckChoiceDialog : Window
{
    public OpenCheckChoice Choice { get; private set; } = OpenCheckChoice.Cancel;

    public OpenCheckChoiceDialog(
        int tableNumber,
        string tableName,
        string checkCode,
        string status,
        int newLineCount,
        decimal newLinesSubtotalUsd)
    {
        DataContext = new OpenCheckChoiceDialogViewModel(
            tableNumber, tableName, checkCode, status, newLineCount, newLinesSubtotalUsd);
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

    private void Append_Click(object sender, RoutedEventArgs e)
    {
        Choice = OpenCheckChoice.AppendToSameTicket;
        DialogResult = true;
        Close();
    }

    private void NewTicket_Click(object sender, RoutedEventArgs e)
    {
        Choice = OpenCheckChoice.NewSeparateTicket;
        DialogResult = true;
        Close();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        Choice = OpenCheckChoice.Cancel;
        DialogResult = false;
        Close();
    }
}
