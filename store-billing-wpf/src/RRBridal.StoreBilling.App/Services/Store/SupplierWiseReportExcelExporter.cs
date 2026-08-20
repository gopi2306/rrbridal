using System;
using System.Globalization;
using System.Linq;
using ClosedXML.Excel;

namespace RRBridal.StoreBilling.App.Services.Store;

public static class SupplierWiseReportExcelExporter
{
    private static readonly string[] SummaryHeaders =
    [
        "Supplier", "Product Count", "Sold Qty", "Return Qty", "Net Qty",
        "Sold Amount", "Return Amount", "Net Amount",
    ];

    private static readonly string[] DetailHeaders =
    [
        "Supplier", "SKU", "Description", "Sold Qty", "Return Qty", "Net Qty",
        "Sold Amount", "Return Amount", "Net Amount",
    ];

    public static void ExportToFile(string filePath, SupplierWiseReportResponse report)
    {
        using var workbook = BuildWorkbook(report);
        workbook.SaveAs(filePath);
    }

    public static XLWorkbook BuildWorkbook(SupplierWiseReportResponse report)
    {
        var workbook = new XLWorkbook();
        AddSummary(workbook.Worksheets.Add("Summary"), report);
        AddDetails(workbook.Worksheets.Add("Product Details"), report);
        return workbook;
    }

    private static void AddSummary(IXLWorksheet ws, SupplierWiseReportResponse report)
    {
        var row = AddPrefix(ws, report.Period, "Supplier-Wise Sales Report");
        row = AddFilters(ws, row, report, invoices: true);
        var totals = report.Totals;
        SetRow(ws, row++, [
            $"TOTAL ({totals.SupplierCount} suppliers)", totals.SkuCount, totals.SoldQty, totals.ReturnQty,
            totals.NetQty, totals.SoldAmount, totals.ReturnAmount, totals.NetAmount,
        ]);
        SetRow(ws, row++, SummaryHeaders.Cast<object>().ToArray());
        ws.Row(row - 1).Style.Font.Bold = true;
        foreach (var supplier in report.Data)
        {
            SetRow(ws, row++, [
                supplier.SupplierName, supplier.ProductCount, supplier.SoldQty, supplier.ReturnQty,
                supplier.NetQty, supplier.SoldAmount, supplier.ReturnAmount, supplier.NetAmount,
            ]);
        }
        Format(ws, row - 1, 3, 8);
    }

    private static void AddDetails(IXLWorksheet ws, SupplierWiseReportResponse report)
    {
        var row = AddPrefix(ws, report.Period, "Supplier-Wise Sales Report");
        row = AddFilters(ws, row, report, invoices: true);
        SetRow(ws, row++, DetailHeaders.Cast<object>().ToArray());
        ws.Row(row - 1).Style.Font.Bold = true;
        foreach (var supplier in report.Data)
        {
            foreach (var product in supplier.Products)
            {
                SetRow(ws, row++, [
                    supplier.SupplierName, product.Sku, product.Description, product.SoldQty, product.ReturnQty,
                    product.NetQty, product.SoldAmount, product.ReturnAmount, product.NetAmount,
                ]);
            }
        }
        Format(ws, row - 1, 4, 9);
    }

    internal static int AddPrefix(IXLWorksheet ws, SkuSalesReportPeriod period, string title)
    {
        SetRow(ws, 1, [""]);
        SetRow(ws, 2, [period.StoreName.ToUpperInvariant()]);
        SetRow(ws, 3, [title]);
        SetRow(ws, 4, [$"DATE : from {LegacyDate(period.From)} {LegacyDate(period.To)} ;"]);
        return 5;
    }

    internal static int AddFilters(IXLWorksheet ws, int row, SupplierWiseReportResponse report, bool invoices)
    {
        var values = new[]
        {
            Value("POS Counter", report.Period.PosCounter),
            Value("Search", report.Filters.Search),
            report.Truncated
                ? $"TRUNCATED: showing {report.Limit} of {report.Total} {(invoices ? "invoices" : "SKUs")}"
                : null,
        }.Where(value => !string.IsNullOrWhiteSpace(value));
        var text = string.Join(" | ", values!);
        if (!string.IsNullOrWhiteSpace(text))
            SetRow(ws, row++, [text]);
        return row;
    }

    internal static string? Value(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : $"{label}: {value}";

    internal static string LegacyDate(string ymd) =>
        DateTime.TryParseExact(ymd, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : ymd;

    internal static void SetRow(IXLWorksheet ws, int row, object[] values)
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

    internal static void Format(IXLWorksheet ws, int lastRow, int firstMoneyColumn, int lastMoneyColumn)
    {
        if (lastRow > 0)
            ws.Range(1, firstMoneyColumn, lastRow, lastMoneyColumn).Style.NumberFormat.Format = "0.00";
        ws.Columns().AdjustToContents();
    }
}
