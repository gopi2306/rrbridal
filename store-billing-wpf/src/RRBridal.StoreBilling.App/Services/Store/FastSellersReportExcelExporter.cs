using System.Linq;
using ClosedXML.Excel;

namespace RRBridal.StoreBilling.App.Services.Store;

public static class FastSellersReportExcelExporter
{
    private static readonly string[] Headers =
    [
        "Rank", "SKU", "Description", "Sold Qty", "Return Qty", "Net Qty", "Net Amount",
    ];

    public static void ExportToFile(string filePath, FastSellersReportResponse report)
    {
        using var workbook = BuildWorkbook(report);
        workbook.SaveAs(filePath);
    }

    public static XLWorkbook BuildWorkbook(FastSellersReportResponse report)
    {
        var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Fast Sellers");
        var row = SupplierWiseReportExcelExporter.AddPrefix(ws, report.Period, "Fast Sellers Report");
        var values = new[]
        {
            SupplierWiseReportExcelExporter.Value("POS Counter", report.Period.PosCounter),
            SupplierWiseReportExcelExporter.Value("Search", report.Filters.Search),
            report.Truncated ? $"TRUNCATED: showing {report.Limit} of {report.Total} SKUs" : null,
        }.Where(value => !string.IsNullOrWhiteSpace(value));
        var text = string.Join(" | ", values!);
        if (!string.IsNullOrWhiteSpace(text))
            SupplierWiseReportExcelExporter.SetRow(ws, row++, [text]);

        var totals = report.Totals;
        SupplierWiseReportExcelExporter.SetRow(ws, row++, [
            "", $"TOTAL ({totals.SkuCount} SKUs)", "", totals.SoldQty, totals.ReturnQty,
            totals.NetQty, totals.NetAmount,
        ]);
        SupplierWiseReportExcelExporter.SetRow(ws, row++, Headers.Cast<object>().ToArray());
        ws.Row(row - 1).Style.Font.Bold = true;
        foreach (var item in report.Data)
        {
            SupplierWiseReportExcelExporter.SetRow(ws, row++, [
                item.Rank, item.Sku, item.Description, item.SoldQty, item.ReturnQty,
                item.NetQty, item.NetAmount,
            ]);
        }
        SupplierWiseReportExcelExporter.Format(ws, row - 1, 4, 7);
        return workbook;
    }
}
