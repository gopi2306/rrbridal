using System;
using System.Globalization;
using System.Linq;
using ClosedXML.Excel;

namespace RRBridal.StoreBilling.App.Services.Store;

public static class GoingOutOfStockReportExcelExporter
{
    private static readonly string[] Headers =
    [
        "SKU", "Product Name", "Store Qty", "Threshold", "Status", "Supplier",
    ];

    public static void ExportToFile(string filePath, GoingOutOfStockReportResponse report)
    {
        using var workbook = BuildWorkbook(report);
        workbook.SaveAs(filePath);
    }

    public static XLWorkbook BuildWorkbook(GoingOutOfStockReportResponse report)
    {
        var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Going Out Of Stock");
        SetRow(ws, 1, [""]);
        SetRow(ws, 2, [report.Period.StoreName.ToUpperInvariant()]);
        SetRow(ws, 3, ["Going Out Of Stock Report"]);
        SetRow(ws, 4, [$"DATE : as of {LegacyDate(report.Period.AsOf)} ;"]);
        var row = 5;

        var filters = new[]
        {
            Value("Search", report.Filters.Search),
            Value("Status", report.Filters.Status),
            report.Truncated ? $"TRUNCATED: showing {report.Limit} of {report.Total} SKUs" : null,
        }.Where(value => !string.IsNullOrWhiteSpace(value));
        var text = string.Join(" | ", filters!);
        if (!string.IsNullOrWhiteSpace(text))
            SetRow(ws, row++, [text]);

        var totals = report.Totals;
        SetRow(ws, row++, [
            $"TOTAL ({totals.SkuCount} SKUs)",
            $"{totals.CriticalCount} critical · {totals.LowCount} low · {totals.ZeroQtyCount} at zero",
            "", "", "", "",
        ]);
        SetRow(ws, row++, Headers.Cast<object>().ToArray());
        ws.Row(row - 1).Style.Font.Bold = true;

        foreach (var item in report.Data)
        {
            SetRow(ws, row++, [
                item.Sku, item.ProductName, item.StoreQty, item.Threshold, item.Status, item.SupplierName,
            ]);
        }

        if (row > 1)
            ws.Range(1, 3, row - 1, 4).Style.NumberFormat.Format = "0.00";
        ws.Columns().AdjustToContents();
        return workbook;
    }

    private static string? Value(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : $"{label}: {value}";

    private static string LegacyDate(string ymd) =>
        DateTime.TryParseExact(ymd, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : ymd;

    private static void SetRow(IXLWorksheet ws, int row, object[] values)
    {
        for (var col = 0; col < values.Length; col++)
        {
            var cell = ws.Cell(row, col + 1);
            switch (values[col])
            {
                case int value: cell.Value = value; break;
                case decimal value: cell.Value = (double)value; break;
                default: cell.Value = values[col]?.ToString() ?? ""; break;
            }
        }
    }
}
