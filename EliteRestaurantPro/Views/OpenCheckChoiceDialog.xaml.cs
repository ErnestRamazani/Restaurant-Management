using System.Windows;

namespace EliteRestaurantPro.Views;

public enum OpenCheckChoice
{
    Cancel,
    AppendToSameTicket,
    NewSeparateTicket,
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
        string newLinesSubtotalText)
    {
        InitializeComponent();

        var name = string.IsNullOrWhiteSpace(tableName) ? $"Table {tableNumber}" : tableName;
        SummaryText.Text =
            $"{name} already has an open ticket {checkCode}.\nStatus: {status}";

        PromptText.Text = newLineCount == 1
            ? "You are sending 1 new line on this order."
            : $"You are sending {newLineCount} new lines on this order.";

        SubtotalText.Text = $"Subtotal for new lines: {newLinesSubtotalText}";
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
