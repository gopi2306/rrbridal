using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class OutboundDispatchA4DocumentBuilder
{
    private static readonly CultureInfo In = CultureInfo.GetCultureInfo("en-IN");
    private static readonly Brush Border = Brushes.Black;

    public static FlowDocument CreateParcelNote(OutboundDispatchPrintInput input)
    {
        var document = CreateDocument(input.Store, "PARCEL NOTE");
        document.Blocks.Add(KeyValues(
            ("Dispatch no", input.DispatchNo), ("Batch no", input.BatchNo),
            ("Bill no", input.BillNo), ("Date", input.Date),
            ("Status", input.Status), ("Packages", input.PackageCount.ToString(In))));
        document.Blocks.Add(Section("RECIPIENT",
            $"{input.RecipientName}\n{input.RecipientAddress}\nPhone: {input.RecipientPhone}"));
        document.Blocks.Add(KeyValues(
            ("Carrier", Join(input.CarrierName, input.CarrierType)),
            ("Tracking no", input.TrackingNo),
            ("Fee payer", input.FeePayer),
            ("Dispatch fee", MoneyMath.FormatRupee(input.DispatchFee))));

        var rows = input.Lines.Select(line => new[]
        {
            line.Sku,
            line.Description,
            FormatQuantity(line.Quantity),
        }).ToList();
        document.Blocks.Add(DataTable(
            "ITEMS", new[] { "SKU", "Description", "Quantity" }, rows,
            new[] { 1.0, 3.0, 0.8 }));
        document.Blocks.Add(Signatures(
            $"Prepared by\n{input.PreparedBy}",
            "Prepared signature",
            "Handover to / signature",
            "Recipient signature"));
        return document;
    }

    public static FlowDocument CreateBatchSummary(OutboundDispatchBatchPrintInput input)
    {
        var document = CreateDocument(input.Store, "DISPATCH BATCH SUMMARY");
        document.Blocks.Add(KeyValues(
            ("Batch no", input.BatchNo),
            ("Carrier", Join(input.CarrierName, input.CarrierType)),
            ("Date", input.Date)));
        var rows = input.Rows.Select(row => new[]
        {
            row.DispatchNo,
            row.BillNo,
            $"{row.CustomerName}\n{row.CustomerPhone}",
            row.TrackingNo,
            row.PackageCount.ToString(In),
            row.FeePayer,
            MoneyMath.FormatRupee(row.DispatchFee),
        }).ToList();
        document.Blocks.Add(DataTable(
            "DISPATCHES",
            new[] { "Dispatch", "Bill", "Customer", "Tracking", "Parcels", "Payer", "Fee" },
            rows,
            new[] { 1.1, 1.0, 1.8, 1.2, 0.6, 0.7, 0.8 }));
        document.Blocks.Add(KeyValues(
            ("Total parcels", input.TotalParcels.ToString(In)),
            ("Customer-paid fee total", MoneyMath.FormatRupee(input.CustomerPaidFee)),
            ("Store-paid fee total", MoneyMath.FormatRupee(input.StorePaidFee))));
        document.Blocks.Add(Signatures(
            "Prepared by / signature",
            "Carrier received by",
            "Carrier signature",
            "Handover date / time"));
        return document;
    }

    public static FlowDocument CreateChargeReceipt(OutboundDispatchChargeReceiptPrintInput input)
    {
        var document = CreateDocument(input.Store, "DISPATCH CHARGE RECEIPT");
        document.Blocks.Add(new Paragraph(new Run("Separate dispatch charge"))
        {
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 14),
        });
        document.Blocks.Add(KeyValues(
            ("Receipt no", input.ReceiptNo),
            ("Bill no", input.BillNo),
            ("Dispatch no", input.DispatchNo),
            ("Date", input.Date)));
        document.Blocks.Add(Section("CUSTOMER", $"{input.CustomerName}\nPhone: {input.CustomerPhone}"));
        document.Blocks.Add(KeyValues(
            ("Dispatch charge amount", MoneyMath.FormatRupee(input.Amount)),
            ("Payment mode", input.PaymentMode),
            ("Reference", input.Reference),
            ("Received by", input.ReceivedBy)));
        document.Blocks.Add(Signatures("Received by", "Customer signature"));
        return document;
    }

    private static FlowDocument CreateDocument(StoreProfile store, string title)
    {
        var pageWidth = InvoiceImageScaling.MmToPx(210);
        var document = new FlowDocument
        {
            PageWidth = pageWidth,
            PageHeight = InvoiceImageScaling.MmToPx(297),
            PagePadding = new Thickness(InvoiceImageScaling.MmToPx(14)),
            ColumnWidth = pageWidth,
            IsColumnWidthFlexible = false,
            FontFamily = new FontFamily("Segoe UI, Arial"),
            FontSize = 11,
            Foreground = Brushes.Black,
            Background = Brushes.White,
        };
        document.Blocks.Add(new Paragraph
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12),
            Inlines =
            {
                new Run(store.StoreName) { FontSize = 19, FontWeight = FontWeights.Bold },
                new LineBreak(),
                new Run(store.Address),
                new LineBreak(),
                new Run(string.IsNullOrWhiteSpace(store.CustomerCarePhone)
                    ? ""
                    : $"Phone: {store.CustomerCarePhone}"),
                new LineBreak(),
                new Run(title) { FontSize = 16, FontWeight = FontWeights.Bold },
            },
        });
        return document;
    }

    private static Block Section(string title, string value) => new Section
    {
        BorderBrush = Border,
        BorderThickness = new Thickness(1),
        Padding = new Thickness(8),
        Margin = new Thickness(0, 0, 0, 10),
        Blocks =
        {
            new Paragraph(new Run(title)) { FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) },
            new Paragraph(new Run(value ?? "")) { Margin = new Thickness(0) },
        },
    };

    private static Table KeyValues(params (string Label, string Value)[] values)
    {
        var table = BaseTable();
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
        var group = new TableRowGroup();
        foreach (var (label, value) in values)
        {
            var row = new TableRow();
            row.Cells.Add(Cell(label, true));
            row.Cells.Add(Cell(value, false));
            group.Rows.Add(row);
        }
        table.RowGroups.Add(group);
        return table;
    }

    private static Section DataTable(
        string title,
        IReadOnlyList<string> headers,
        IReadOnlyList<string[]> rows,
        IReadOnlyList<double> widths)
    {
        var table = BaseTable();
        foreach (var width in widths)
            table.Columns.Add(new TableColumn { Width = new GridLength(width, GridUnitType.Star) });
        var group = new TableRowGroup();
        var header = new TableRow();
        foreach (var value in headers)
            header.Cells.Add(Cell(value, true));
        group.Rows.Add(header);
        foreach (var values in rows)
        {
            var row = new TableRow();
            foreach (var value in values)
                row.Cells.Add(Cell(value, false));
            group.Rows.Add(row);
        }
        if (rows.Count == 0)
        {
            var empty = new TableRow();
            var cell = Cell("(No rows)", false);
            cell.ColumnSpan = headers.Count;
            empty.Cells.Add(cell);
            group.Rows.Add(empty);
        }
        table.RowGroups.Add(group);
        return new Section
        {
            Margin = new Thickness(0, 0, 0, 10),
            Blocks =
            {
                new Paragraph(new Run(title)) { FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) },
                table,
            },
        };
    }

    private static Table Signatures(params string[] labels)
    {
        var table = BaseTable();
        foreach (var _ in labels)
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        var row = new TableRow();
        foreach (var label in labels)
            row.Cells.Add(new TableCell(new Paragraph(new Run($"\n\n{label}\n____________________")))
            {
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
                TextAlignment = TextAlignment.Center,
            });
        var group = new TableRowGroup();
        group.Rows.Add(row);
        table.RowGroups.Add(group);
        return table;
    }

    private static Table BaseTable() => new()
    {
        CellSpacing = 0,
        Margin = new Thickness(0, 0, 0, 10),
    };

    private static TableCell Cell(string value, bool bold) => new(new Paragraph(new Run(value ?? ""))
    {
        Margin = new Thickness(0),
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
    })
    {
        BorderBrush = Border,
        BorderThickness = new Thickness(1),
        Padding = new Thickness(5),
    };

    private static string Join(string first, string second) =>
        string.IsNullOrWhiteSpace(first) ? second
        : string.IsNullOrWhiteSpace(second) ? first
        : $"{first} ({second})";

    private static string FormatQuantity(decimal quantity) =>
        quantity == decimal.Truncate(quantity)
            ? quantity.ToString("0", In)
            : quantity.ToString("0.##", In);
}
