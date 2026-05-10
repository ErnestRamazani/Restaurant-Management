using ClosedXML.Excel;

namespace EliteRestaurant.Core.Utils;

public static class ExcelExportService
{
    public static void ExportSingleSheet(
        string filePath,
        string sheetName,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        ExportWorkbook(filePath, [(sheetName, headers, rows)]);
    }

    public static void ExportWorkbook(
        string filePath,
        IReadOnlyList<(string SheetName, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows)> sheets)
    {
        using var workbook = new XLWorkbook();
        PopulateSheets(workbook, sheets);
        workbook.SaveAs(filePath);
    }

    public static byte[] ExportWorkbookToByteArray(
        IReadOnlyList<(string SheetName, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows)> sheets)
    {
        using var workbook = new XLWorkbook();
        PopulateSheets(workbook, sheets);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void PopulateSheets(
        XLWorkbook workbook,
        IReadOnlyList<(string SheetName, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows)> sheets)
    {
        foreach (var sheet in sheets)
        {
            var worksheet = workbook.Worksheets.Add(NormalizeSheetName(sheet.SheetName));
            for (var i = 0; i < sheet.Headers.Count; i++)
                worksheet.Cell(1, i + 1).Value = sheet.Headers[i];

            var headerRange = worksheet.Range(1, 1, 1, Math.Max(sheet.Headers.Count, 1));
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEE8D5");

            for (var rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
            {
                var row = sheet.Rows[rowIndex];
                for (var colIndex = 0; colIndex < row.Count; colIndex++)
                    worksheet.Cell(rowIndex + 2, colIndex + 1).Value = row[colIndex];
            }

            worksheet.ColumnsUsed().AdjustToContents();
            worksheet.SheetView.FreezeRows(1);
        }
    }

    private static string NormalizeSheetName(string sheetName)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
            return "Sheet";

        var invalid = new[] { '[', ']', '*', '?', '/', '\\', ':' };
        var normalized = new string(sheetName.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray());
        return normalized[..Math.Min(normalized.Length, 31)];
    }
}
