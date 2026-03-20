using ClosedXML.Excel;

namespace Framework.Data;

/// <summary>
/// Reads test data from Excel workbooks (.xlsx). <see cref="ReadSheet"/> opens the specified
/// sheet, treats the first row as column headers, and returns all subsequent rows as a list of
/// case-insensitive dictionaries keyed by those headers. <see cref="ReadFirstSheet"/> uses the
/// workbook's first sheet when the sheet name is not known in advance. Throws
/// <see cref="FileNotFoundException"/> if the file does not exist.
/// </summary>
public static class ExcelDataProvider
{
    public static IReadOnlyList<Dictionary<string, string>> ReadSheet(string filePath, string sheetName)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Excel file not found: {filePath}", filePath);
        }

        using var workbook = new XLWorkbook(filePath);
        if (!workbook.Worksheets.Contains(sheetName))
            throw new ArgumentException($"Sheet not found: {sheetName}");
        return ReadWorksheet(workbook.Worksheet(sheetName));
    }

    public static IReadOnlyList<Dictionary<string, string>> ReadFirstSheet(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Excel file not found: {filePath}", filePath);
        }

        using var workbook = new XLWorkbook(filePath);
        return ReadWorksheet(workbook.Worksheets.First());
    }

    private static IReadOnlyList<Dictionary<string, string>> ReadWorksheet(IXLWorksheet worksheet)
    {
        var usedRange = worksheet.RangeUsed();

        if (usedRange is null)
        {
            return Array.Empty<Dictionary<string, string>>();
        }

        var rows = usedRange.RowsUsed().ToList();
        if (rows.Count <= 1)
        {
            return Array.Empty<Dictionary<string, string>>();
        }

        var headers = rows.First()
            .CellsUsed()
            .Select(cell => (cell.GetString() ?? string.Empty).Trim())
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .ToList();

        var result = new List<Dictionary<string, string>>();

        foreach (var row in rows.Skip(1))
        {
            var rowData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < headers.Count; index++)
            {
                var cellValue = row.Cell(index + 1).GetValue<object>() as string ?? string.Empty;
                rowData[headers[index]] = cellValue.Trim();
            }

            result.Add(rowData);
        }

        return result;
    }
}
