using System.Collections.Generic;
using ClosedXML.Excel;

namespace RRBridal.StoreBilling.App.Services.Store;

public static class BillMarginExcelExporter
{
    private static readonly string[] Headers =
    [
        "Bill no",
        "Posted (local)",
        "Bill date",
        "Counter",
        "Customer",
        "Salesman",
        "Qty",
        "Cost (ex GST)",
        "Selling (ex GST)",
        "Discount (ex GST)",
        "Margin %",
        "Margin amt (ex GST)",
        "Returned",
        "Return no",
        "Adjusted",
        "Adjustment no",
    ];

    public static void ExportToFile(string filePath, IReadOnlyList<BillMarginRow> rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Bill margin");

        for (var c = 0; c < Headers.Length; c++)
            ws.Cell(1, c + 1).Value = Headers[c];
        ws.Row(1).Style.Font.Bold = true;

        var rowIndex = 2;
        foreach (var row in rows)
        {
            ws.Cell(rowIndex, 1).Value = row.BillNo;
            ws.Cell(rowIndex, 2).Value = row.PostedAtLocal;
            ws.Cell(rowIndex, 3).Value = row.BillDate;
            ws.Cell(rowIndex, 4).Value = row.CounterDisplay;
            ws.Cell(rowIndex, 5).Value = row.CustomerName;
            ws.Cell(rowIndex, 6).Value = row.SalesmanDisplay;
            ws.Cell(rowIndex, 7).Value = (double)row.TotalQty;
            ws.Cell(rowIndex, 8).Value = (double)row.CostPrice;
            ws.Cell(rowIndex, 9).Value = (double)row.SellingPrice;
            ws.Cell(rowIndex, 10).Value = (double)row.Discount;
            ws.Cell(rowIndex, 11).Value = (double)row.MarginPercent;
            ws.Cell(rowIndex, 12).Value = (double)row.MarginAmount;
            ws.Cell(rowIndex, 13).Value = row.ReturnedDisplay;
            ws.Cell(rowIndex, 14).Value = row.ReturnNo;
            ws.Cell(rowIndex, 15).Value = row.AdjustmentDisplay;
            ws.Cell(rowIndex, 16).Value = row.AdjustmentNo;

            ws.Cell(rowIndex, 7).Style.NumberFormat.Format = "0.##";
            ws.Cell(rowIndex, 8).Style.NumberFormat.Format = "0.00";
            ws.Cell(rowIndex, 9).Style.NumberFormat.Format = "0.00";
            ws.Cell(rowIndex, 10).Style.NumberFormat.Format = "0.00";
            ws.Cell(rowIndex, 11).Style.NumberFormat.Format = "0.00";
            ws.Cell(rowIndex, 12).Style.NumberFormat.Format = "0.00";
            rowIndex++;
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }
}
