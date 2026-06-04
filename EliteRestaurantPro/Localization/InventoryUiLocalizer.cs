using EliteRestaurant.Core.Models;

namespace EliteRestaurantPro.Localization;

public static class InventoryUiLocalizer
{
    public static void Apply(InventoryItem item)
    {
        item.DisplayQuantityStatus = AdminTextLocalizer.TranslateInventoryQuantityBand(item.QuantityStatus);
        item.DisplayShelfStatusLine = AdminTextLocalizer.FormatInventoryShelfStatusLine(item);
        item.DisplayExpirationDateText = AdminTextLocalizer.FormatInventoryExpirationDateText(item.ExpirationDate);
        item.DisplayNotesForView = TranslateNotes(item.Notes);
    }

    public static void ApplyAll(IEnumerable<InventoryItem> items)
    {
        foreach (var item in items)
            Apply(item);
    }

    /// <summary>Translated multiline notes for edit dialog / cards. Empty input yields empty string.</summary>
    public static string TranslateNotesForDisplay(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return string.Empty;

        return string.Join("\n", notes.Split('\n').Select(line =>
        {
            var trimmed = line.Trim();
            return trimmed.Length == 0 ? string.Empty : DashboardTextLocalizer.TranslateInventoryNoteLine(trimmed);
        }));
    }

    private static string TranslateNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return Loc.Admin("invNoNotesCard", "No notes");

        return TranslateNotesForDisplay(notes);
    }
}
