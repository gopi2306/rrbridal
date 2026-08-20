using System;
using System.Globalization;
using System.Linq;
using ClosedXML.Excel;

namespace RRBridal.StoreBilling.App.Services.Store;

public static class CustomerBillingReportExcelExporter
{
    private static readonly string[] SummaryHeaders =
    [
        "Customer Code", "Customer Name", "Customer Phone", "Bill Count", "Qty",
        "Gross Billed", "Returns", "Net Sales", "Cash", "Card", "UPI", "Credit Note",
    ];

    private static readonly string[] DetailHeaders =
    [
        "Customer Code", "Customer Name", "Customer Phone", "Bill Date", "Bill No",
        "POS Counter", "Qty", "Gross Billed", "Returns", "Net Sales", "Cash", "Card",
        "UPI", "Credit Note", "Return Audit",
    ];

    private static readonly string[] LineHeaders =
    [
        "Customer Code", "Customer Name", "Customer Phone", "Bill Date", "Bill No",
        "Line No", "SKU", "Description", "HSN", "Qty", "Rate", "Discount", "Tax", "Amount",
    ];

    public static void ExportToFile(string filePath, CustomerBillingReportResponse report)
    {
        using var workbook = BuildWorkbook(report);
        workbook.SaveAs(filePath);
    }

    public static XLWorkbook BuildWorkbook(CustomerBillingReportResponse report)
    {
        var workbook = new XLWorkbook();
        AddSummary(workbook.Worksheets.Add("Summary"), report);
        AddDetails(workbook.Worksheets.Add("Bill Details"), report);
        AddLineItems(workbook.Worksheets.Add("Line Items"), report);
        return workbook;
    }

    private static void AddSummary(IXLWorksheet ws, CustomerBillingReportResponse report)
    {
        var row = AddPrefix(ws, report);
        row = AddFilters(ws, row, report);

        var totals = report.Totals;
        SetRow(ws, row++, [
            "", $"TOTAL ({totals.CustomerCount} customers)", "", totals.BillCount, totals.Qty,
            totals.GrossAmount, totals.ReturnAmount, totals.NetAmount, totals.Payments.Cash,
            totals.Payments.Card, totals.Payments.Upi, totals.Payments.CreditNote,
        ]);
        SetRow(ws, row++, SummaryHeaders.Cast<object>().ToArray());
        ws.Row(row - 1).Style.Font.Bold = true;

        foreach (var customer in report.Data)
        {
            SetRow(ws, row++, [
                customer.CustomerCode, customer.CustomerName, customer.CustomerPhone,
                customer.BillCount, customer.Qty, customer.GrossAmount, customer.ReturnAmount,
                customer.NetAmount, customer.Payments.Cash, customer.Payments.Card,
                customer.Payments.Upi, customer.Payments.CreditNote,
            ]);
        }

        Format(ws, row - 1, 5, 12);
    }

    private static void AddDetails(IXLWorksheet ws, CustomerBillingReportResponse report)
    {
        var row = AddPrefix(ws, report);
        row = AddFilters(ws, row, report);
        SetRow(ws, row++, DetailHeaders.Cast<object>().ToArray());
        ws.Row(row - 1).Style.Font.Bold = true;

        foreach (var bill in report.Data.SelectMany(customer => customer.Bills))
        {
            var audit = string.Join("; ", bill.Returns.Select(ret =>
                $"{ret.ReturnNo} | {ret.ReturnDate} | " +
                $"{(!string.IsNullOrWhiteSpace(ret.ReturnMode) ? ret.ReturnMode : ret.Kind)} | " +
                ret.Amount.ToString("0.00", CultureInfo.InvariantCulture)));
            SetRow(ws, row++, [
                bill.CustomerCode, bill.CustomerName, bill.CustomerPhone, bill.BillDate, bill.BillNo,
                bill.PosCounter, bill.Qty, bill.GrossAmount, bill.ReturnAmount, bill.NetAmount,
                bill.Payments.Cash, bill.Payments.Card, bill.Payments.Upi, bill.Payments.CreditNote,
                audit,
            ]);
        }

        Format(ws, row - 1, 7, 14);
    }

    private static void AddLineItems(IXLWorksheet ws, CustomerBillingReportResponse report)
    {
        var row = AddPrefix(ws, report);
        row = AddFilters(ws, row, report);
        SetRow(ws, row++, LineHeaders.Cast<object>().ToArray());
        ws.Row(row - 1).Style.Font.Bold = true;

        foreach (var bill in report.Data.SelectMany(customer => customer.Bills))
        {
            foreach (var line in bill.Lines)
            {
                SetRow(ws, row++, [
                    bill.CustomerCode, bill.CustomerName, bill.CustomerPhone, bill.BillDate, bill.BillNo,
                    line.LineNo, line.Sku, line.Description, line.Hsn, line.Qty, line.Rate,
                    line.DiscountAmount, line.TaxAmount, line.Amount,
                ]);
            }
        }

        Format(ws, row - 1, 10, 14);
    }

    private static int AddPrefix(IXLWorksheet ws, CustomerBillingReportResponse report)
    {
        SetRow(ws, 1, [""]);
        SetRow(ws, 2, [report.Period.StoreName.ToUpperInvariant()]);
        SetRow(ws, 3, ["Customer-Wise Billing Report"]);
        SetRow(ws, 4, [$"DATE : from {LegacyDate(report.Period.From)} {LegacyDate(report.Period.To)} ;"]);
        return 5;
    }

    private static int AddFilters(IXLWorksheet ws, int row, CustomerBillingReportResponse report)
    {
        var values = new[]
        {
            Value("POS Counter", report.Period.PosCounter),
            Value("Customer search", report.Filters.CustomerSearch),
            Value("Customer code", report.Filters.CustomerCode),
            Value("Customer phone", report.Filters.CustomerPhone),
            report.Truncated ? $"TRUNCATED: showing {report.Limit} of {report.Total} invoices" : null,
        }.Where(value => !string.IsNullOrWhiteSpace(value));

        var text = string.Join(" | ", values!);
        if (!string.IsNullOrWhiteSpace(text))
            SetRow(ws, row++, [text]);
        return row;
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

    private static void Format(IXLWorksheet ws, int lastRow, int firstMoneyColumn, int lastMoneyColumn)
    {
        if (lastRow > 0)
            ws.Range(1, firstMoneyColumn, lastRow, lastMoneyColumn).Style.NumberFormat.Format = "0.00";
        ws.Columns().AdjustToContents();
    }
}
