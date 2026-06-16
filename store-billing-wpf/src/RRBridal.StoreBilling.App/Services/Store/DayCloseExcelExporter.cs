using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace RRBridal.StoreBilling.App.Services.Store;

public static class DayCloseExcelExporter
{
    public static void ExportToFile(string filePath, DayCloseReportData data)
    {
        using var workbook = BuildWorkbook(data);
        workbook.SaveAs(filePath);
    }

    public static XLWorkbook BuildWorkbook(DayCloseReportData data)
    {
        var workbook = new XLWorkbook();
        AddKeyValueSheet(workbook, "METADATA", DayCloseReportSectionBuilder.BuildMetadataRows(data));
        AddKeyValueSheet(workbook, "SUMMARY", DayCloseReportSectionBuilder.BuildSummaryRows(data));

        foreach (var section in DayCloseReportSectionBuilder.BuildAllDetailSections(data))
            AddTableSheet(workbook, SanitizeSheetName(section.Name), section.Headers, section.Rows);

        return workbook;
    }

    private static void AddKeyValueSheet(
        XLWorkbook workbook,
        string sheetName,
        IReadOnlyList<(string Label, string Value)> rows)
    {
        var ws = workbook.Worksheets.Add(SanitizeSheetName(sheetName));
        ws.Cell(1, 1).Value = "Label";
        ws.Cell(1, 2).Value = "Value";
        ws.Row(1).Style.Font.Bold = true;
        var row = 2;
        foreach (var (label, value) in rows)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = value;
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void AddTableSheet(
        XLWorkbook workbook,
        string sheetName,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var ws = workbook.Worksheets.Add(sheetName);
        for (var c = 0; c < headers.Count; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        ws.Row(1).Style.Font.Bold = true;

        var rowIndex = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Count; c++)
                ws.Cell(rowIndex, c + 1).Value = row[c];
            rowIndex++;
        }

        ws.Columns().AdjustToContents();
    }

    private static string SanitizeSheetName(string name)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var sanitized = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return sanitized.Length <= 31 ? sanitized : sanitized[..31];
    }
}
