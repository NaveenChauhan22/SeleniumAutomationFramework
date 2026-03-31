using ClosedXML.Excel;

namespace Framework.Data;

/// <summary>
/// Reads and writes test data from/to Excel workbooks (.xlsx). <see cref="ReadSheet"/> opens the specified
/// sheet, treats the first row as column headers, and returns all subsequent rows as a list of
/// case-insensitive dictionaries keyed by those headers. <see cref="ReadFirstSheet"/> uses the
/// workbook's first sheet when the sheet name is not known in advance. <see cref="WriteSheet"/>
/// writes rows into the specified sheet, creating the file or sheet when they do not exist and
/// updating existing rows when, matched by a key column. Throws
/// <see cref="FileNotFoundException"/> if the file does not exist (read methods only).
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
                var cellValue = row.Cell(index + 1).GetString() ?? string.Empty;
                rowData[headers[index]] = cellValue.Trim();
            }

            result.Add(rowData);
        }

        return result;
    }

    /// <summary>
    /// Writes rows into the specified sheet of an Excel workbook. Creates the file or sheet when
    /// they do not exist. When the sheet already has data and <paramref name="keyColumn"/> is
    /// provided, existing rows whose key value matches an incoming row are updated in place;
    /// unmatched incoming rows are appended. When <paramref name="keyColumn"/> is <c>null</c>,
    /// all incoming rows are appended after any existing data.
    /// </summary>
    /// <param name="filePath">Path to the .xlsx file (created if missing).</param>
    /// <param name="sheetName">Worksheet name (created if missing).</param>
    /// <param name="rows">Rows to write, each keyed by column header.</param>
    /// <param name="keyColumn">
    /// Optional column name used to match incoming rows against existing rows for updates.
    /// When <c>null</c>, rows are always appended.
    /// </param>
    public static void WriteSheet(
        string filePath,
        string sheetName,
        IReadOnlyList<Dictionary<string, string>> rows,
        string? keyColumn = null)
    {
        if (rows is null || rows.Count == 0)
            return;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        using var workbook = File.Exists(filePath) ? new XLWorkbook(filePath) : new XLWorkbook();

        var worksheet = workbook.Worksheets.Contains(sheetName)
            ? workbook.Worksheet(sheetName)
            : workbook.Worksheets.Add(sheetName);

        // Collect all column headers from incoming rows (preserving insertion order).
        var incomingHeaders = new List<string>();
        var headerSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            foreach (var key in row.Keys)
            {
                if (headerSet.Add(key))
                    incomingHeaders.Add(key);
            }
        }

        // Read existing headers from the sheet (row 1) if any.
        var existingHeaders = new List<string>();
        var usedRange = worksheet.RangeUsed();
        if (usedRange is not null)
        {
            var headerRow = worksheet.Row(1);
            foreach (var cell in headerRow.CellsUsed())
            {
                var h = (cell.GetString() ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(h))
                    existingHeaders.Add(h);
            }
        }

        // Merge existing + incoming headers so new columns are appended at the end.
        var allHeaders = new List<string>(existingHeaders);
        var allHeaderSet = new HashSet<string>(existingHeaders, StringComparer.OrdinalIgnoreCase);
        foreach (var h in incomingHeaders)
        {
            if (allHeaderSet.Add(h))
                allHeaders.Add(h);
        }

        // Write header row.
        for (var i = 0; i < allHeaders.Count; i++)
            worksheet.Cell(1, i + 1).Value = allHeaders[i];

        // Build a lookup of column index by name (1-based).
        var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < allHeaders.Count; i++)
            colIndex[allHeaders[i]] = i + 1;

        // Determine next empty row.
        var lastUsedRow = usedRange is not null ? usedRange.LastRow().RowNumber() : 1;
        var nextRow = lastUsedRow + 1;

        // Build index of existing key values → row number for update-matching.
        var existingKeyRows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (keyColumn is not null && colIndex.TryGetValue(keyColumn, out var keyColIdx))
        {
            for (var r = 2; r <= lastUsedRow; r++)
            {
                var cellValue = (worksheet.Cell(r, keyColIdx).GetString() ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(cellValue))
                    existingKeyRows[cellValue] = r;
            }
        }

        foreach (var row in rows)
        {
            int targetRow;

            if (keyColumn is not null
                && row.TryGetValue(keyColumn, out var keyValue)
                && !string.IsNullOrWhiteSpace(keyValue)
                && existingKeyRows.TryGetValue(keyValue, out var matchedRow))
            {
                targetRow = matchedRow;
            }
            else
            {
                targetRow = nextRow++;
            }

            foreach (var kvp in row)
            {
                if (colIndex.TryGetValue(kvp.Key, out var col))
                    worksheet.Cell(targetRow, col).Value = kvp.Value;
            }
        }

        workbook.SaveAs(filePath);
    }
}
