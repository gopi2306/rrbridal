using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Values-only A5 FlowDocument for pre-printed invoice stationery.</summary>
public static class A5PrePrintedInvoiceDocumentBuilder
{
    private static readonly CultureInfo In = CultureInfo.GetCultureInfo("en-IN");

    public static FlowDocument Create(ThermalInvoiceInput input)
    {
        var pageWidth = InvoiceImageScaling.MmToPx(A5PrePrintedInvoiceLayout.PageWidthMm);
        var pageHeight = InvoiceImageScaling.MmToPx(A5PrePrintedInvoiceLayout.PageHeightMm);

        var doc = new FlowDocument
        {
            PageWidth = pageWidth,
            PageHeight = pageHeight,
            PagePadding = new Thickness(0),
            ColumnWidth = pageWidth,
            IsColumnWidthFlexible = false,
            IsOptimalParagraphEnabled = false,
            Background = Brushes.White,
        };

        doc.Blocks.Add(new BlockUIContainer(BuildCanvas(input, pageWidth, pageHeight)));
        return doc;
    }

    private static Canvas BuildCanvas(ThermalInvoiceInput input, double pageWidth, double pageHeight)
    {
        var canvas = new Canvas
        {
            Width = pageWidth,
            Height = pageHeight,
            Background = Brushes.White,
        };

        var bodyFont = A5PrePrintedInvoiceLayout.BodyFontPt;
        var totalFont = A5PrePrintedInvoiceLayout.TotalFontPt;

        PlaceText(canvas, A5PrePrintedText.FormatBillTo(input.CustomerName), A5PrePrintedInvoiceLayout.BillToLeftMm, A5PrePrintedInvoiceLayout.BillToTopMm,
            A5PrePrintedInvoiceLayout.BillToWidthMm, bodyFont, TextAlignment.Left, singleLine: true);
        PlaceText(canvas, input.BillNo, A5PrePrintedInvoiceLayout.InvNoLeftMm, A5PrePrintedInvoiceLayout.InvNoTopMm,
            A5PrePrintedInvoiceLayout.InvNoWidthMm, bodyFont, TextAlignment.Left);
        PlaceText(canvas, input.BillDate, A5PrePrintedInvoiceLayout.DateLeftMm, A5PrePrintedInvoiceLayout.DateTopMm,
            A5PrePrintedInvoiceLayout.DateWidthMm, bodyFont, TextAlignment.Left);
        PlaceText(canvas, input.CustomerPhone, A5PrePrintedInvoiceLayout.ContactLeftMm, A5PrePrintedInvoiceLayout.MetaRow2TopMm,
            A5PrePrintedInvoiceLayout.ContactWidthMm, bodyFont, TextAlignment.Left);

        if (input.Stitching)
        {
            PlaceText(canvas, "✓", A5PrePrintedInvoiceLayout.StitchingTickLeftMm, A5PrePrintedInvoiceLayout.StitchingTickTopMm,
                A5PrePrintedInvoiceLayout.StitchingTickSizeMm, bodyFont, TextAlignment.Center, FontWeights.Bold);
            PlaceText(canvas, input.DeliveryDate, A5PrePrintedInvoiceLayout.DeliveryDateLeftMm, A5PrePrintedInvoiceLayout.DeliveryDateTopMm,
                A5PrePrintedInvoiceLayout.DeliveryDateWidthMm, bodyFont, TextAlignment.Left);
        }

        var lines = input.Lines.Where(l => l.Amount > 0 || l.TaxableAmount > 0).Take(A5PrePrintedInvoiceLayout.MaxLineRows).ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var top = A5PrePrintedInvoiceLayout.TableTopMm + i * A5PrePrintedInvoiceLayout.LineRowHeightMm;
            var amount = line.TaxableAmount > 0 ? line.TaxableAmount : line.Amount;

            PlaceText(canvas, line.Description, A5PrePrintedInvoiceLayout.ColDescLeftMm, top,
                A5PrePrintedInvoiceLayout.ColDescWidthMm, bodyFont, TextAlignment.Left, useTableOffset: true);
            PlaceText(canvas, $"{line.Qty:0.###}", A5PrePrintedInvoiceLayout.ColQtyLeftMm, top,
                A5PrePrintedInvoiceLayout.ColQtyWidthMm, bodyFont, TextAlignment.Center, useTableOffset: true);
            PlaceText(canvas, Money(line.Rate), A5PrePrintedInvoiceLayout.ColRateLeftMm, top,
                A5PrePrintedInvoiceLayout.ColRateWidthMm, bodyFont, TextAlignment.Right, useTableOffset: true);
            PlaceText(canvas, Money(amount), A5PrePrintedInvoiceLayout.ColAmountLeftMm, top,
                A5PrePrintedInvoiceLayout.ColAmountWidthMm, bodyFont, TextAlignment.Right, useTableOffset: true);
        }

        PlaceText(canvas, Money(input.Payable), A5PrePrintedInvoiceLayout.TotalAmountLeftMm, A5PrePrintedInvoiceLayout.TotalAmountTopMm,
            A5PrePrintedInvoiceLayout.TotalAmountWidthMm, totalFont, TextAlignment.Right, FontWeights.Bold, useTableOffset: true);

        return canvas;
    }

    private static void PlaceText(
        Canvas canvas,
        string text,
        double leftMm,
        double topMm,
        double widthMm,
        double fontPt,
        TextAlignment align,
        FontWeight? weight = null,
        bool useTableOffset = false,
        bool singleLine = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var tb = new TextBlock
        {
            Text = text,
            FontFamily = A5PrePrintedText.PrintFont,
            FontSize = fontPt,
            FontWeight = weight ?? FontWeights.Normal,
            Foreground = Brushes.Black,
            Width = InvoiceImageScaling.MmToPx(widthMm),
            TextWrapping = singleLine ? TextWrapping.NoWrap : TextWrapping.Wrap,
            TextTrimming = TextTrimming.None,
            TextAlignment = align,
            Padding = new Thickness(0, 0, 0, 0),
        };
        var yMm = topMm + A5PrePrintedInvoiceLayout.TextBaselineNudgeMm;
        Canvas.SetLeft(tb, InvoiceImageScaling.MmToPx(A5PrePrintedInvoiceLayout.X(leftMm)));
        Canvas.SetTop(tb, InvoiceImageScaling.MmToPx(
            useTableOffset ? A5PrePrintedInvoiceLayout.TableY(yMm) : A5PrePrintedInvoiceLayout.Y(yMm)));
        canvas.Children.Add(tb);
    }

    private static string Money(decimal value) => value.ToString("0.00", In);
}
