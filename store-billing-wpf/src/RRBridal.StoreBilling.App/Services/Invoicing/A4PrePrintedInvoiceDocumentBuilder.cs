using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Values-only A4 FlowDocument for Bilal Textiles pre-printed invoice stationery.</summary>
public static class A4PrePrintedInvoiceDocumentBuilder
{
    private static readonly CultureInfo In = CultureInfo.GetCultureInfo("en-IN");

    public static FlowDocument Create(ThermalInvoiceInput input, A4PrePrintedLayoutSettings? layoutSettings = null)
    {
        var settings = layoutSettings ?? A4PrePrintedLayoutSettings.CreateDefault();
        settings.EnsureAlignmentDefaults();
        var layout = A4PrePrintedInvoiceLayout.FromSettings(settings);
        var printFont = A4PrePrintedText.ResolvePrintFont(settings.PrintFontFamily);

        var pageWidth = InvoiceImageScaling.MmToPx(A4PrePrintedInvoiceLayout.PageWidthMm);
        var pageHeight = InvoiceImageScaling.MmToPx(A4PrePrintedInvoiceLayout.PageHeightMm);

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

        var activeLines = InvoiceLinePagination.ActiveLines(input);
        var chunks = InvoiceLinePagination.ChunkLines(activeLines, layout.MaxLineRows);

        for (var pageIndex = 0; pageIndex < chunks.Count; pageIndex++)
        {
            var isLastPage = pageIndex == chunks.Count - 1;
            var hasMorePages = !isLastPage;
            var canvas = BuildCanvas(
                input,
                chunks[pageIndex],
                layout,
                settings,
                printFont,
                pageWidth,
                pageHeight,
                isLastPage,
                hasMorePages,
                isFirstPage: pageIndex == 0);

            if (pageIndex == 0)
                doc.Blocks.Add(new BlockUIContainer(canvas));
            else
            {
                var section = new Section { BreakPageBefore = true };
                section.Blocks.Add(new BlockUIContainer(canvas));
                doc.Blocks.Add(section);
            }
        }

        return doc;
    }

    private static Canvas BuildCanvas(
        ThermalInvoiceInput input,
        IReadOnlyList<InvoiceLineSnap> pageLines,
        A4PrePrintedInvoiceLayout layout,
        A4PrePrintedLayoutSettings settings,
        FontFamily printFont,
        double pageWidth,
        double pageHeight,
        bool isLastPage,
        bool hasMorePages,
        bool isFirstPage)
    {
        var canvas = new Canvas
        {
            Width = pageWidth,
            Height = pageHeight,
            Background = Brushes.White,
        };

        var bodyFont = layout.BodyFontPt;
        var totalFont = layout.TotalFontPt;

        PlaceHeader(canvas, input, layout, settings, printFont, bodyFont, isFirstPage);

        for (var i = 0; i < pageLines.Count; i++)
        {
            var line = pageLines[i];
            var top = layout.TableTopMm + i * layout.LineRowHeightMm;

            PlaceText(canvas, $"{line.LineNo}", layout.ColSrLeftMm, top, layout.ColSrWidthMm,
                bodyFont, printFont, TextAlignment.Center, useTableOffset: true, layout: layout);
            PlaceText(canvas, line.Description, layout.ColParticularLeftMm, top, layout.ColParticularWidthMm,
                bodyFont, printFont, TextAlignment.Left, useTableOffset: true, layout: layout);
            PlaceText(canvas, line.Hsn, layout.ColHsnLeftMm, top, layout.ColHsnWidthMm,
                bodyFont, printFont, TextAlignment.Center, useTableOffset: true, layout: layout);
            PlaceText(canvas, $"{line.Qty:0.###}", layout.ColPcsLeftMm, top, layout.ColPcsWidthMm,
                bodyFont, printFont, TextAlignment.Center, useTableOffset: true, layout: layout);
            PlaceText(canvas, "", layout.ColMeterLeftMm, top, layout.ColMeterWidthMm,
                bodyFont, printFont, TextAlignment.Center, useTableOffset: true, layout: layout);
            PlaceText(canvas, Money(line.Rate), layout.ColBasicRateLeftMm, top, layout.ColBasicRateWidthMm,
                bodyFont, printFont, TextAlignment.Right, useTableOffset: true, layout: layout);
            PlaceText(canvas, A4PrePrintedText.FormatPercent(line.CashDiscountPercent()),
                layout.ColCdPercentLeftMm, top, layout.ColCdPercentWidthMm,
                bodyFont, printFont, TextAlignment.Center, useTableOffset: true, layout: layout);
            PlaceText(canvas, A4PrePrintedText.FormatPercent(line.ItemDiscountPercent()),
                layout.ColLessPercentLeftMm, top, layout.ColLessPercentWidthMm,
                bodyFont, printFont, TextAlignment.Center, useTableOffset: true, layout: layout);
            PlaceText(canvas, A4PrePrintedText.FormatPercent(line.SchemeDiscountPercent()),
                layout.ColTdPercentLeftMm, top, layout.ColTdPercentWidthMm,
                bodyFont, printFont, TextAlignment.Center, useTableOffset: true, layout: layout);
            PlaceText(canvas, Money(line.NetUnitRate()), layout.ColNetRateLeftMm, top, layout.ColNetRateWidthMm,
                bodyFont, printFont, TextAlignment.Right, useTableOffset: true, layout: layout);
            PlaceText(canvas, Money(line.PrePrintedLineAmount()), layout.ColAmountLeftMm, top, layout.ColAmountWidthMm,
                bodyFont, printFont, TextAlignment.Right, useTableOffset: true, layout: layout);
        }

        if (hasMorePages)
        {
            var continuedTop = layout.TableTopMm + pageLines.Count * layout.LineRowHeightMm;
            var continuedLeft = settings.ContinuedLeftMm > 0 ? settings.ContinuedLeftMm : layout.ColParticularLeftMm;
            var continuedWidth = settings.ContinuedWidthMm > 0 ? settings.ContinuedWidthMm : layout.ColParticularWidthMm;
            PlaceText(canvas, A4PrePrintedText.ResolveContinuedLabel(settings),
                continuedLeft, continuedTop, continuedWidth,
                bodyFont, printFont, TextAlignment.Left, useTableOffset: true, layout: layout);
        }

        if (isLastPage)
        {
            PlaceText(canvas, Money(input.Payable), layout.TotalAmountLeftMm, layout.TotalAmountTopMm,
                layout.TotalAmountWidthMm, totalFont, printFont, TextAlignment.Right, FontWeights.Bold,
                useTableOffset: true, layout: layout);
        }

        return canvas;
    }

    private static void PlaceHeader(
        Canvas canvas,
        ThermalInvoiceInput input,
        A4PrePrintedInvoiceLayout layout,
        A4PrePrintedLayoutSettings settings,
        FontFamily printFont,
        double bodyFont,
        bool isFirstPage)
    {
        if (isFirstPage && input.IsDuplicateCopy)
        {
            var duplicateLabel = string.IsNullOrWhiteSpace(settings.DuplicateCopyLabel)
                ? "DUPLICATE COPY"
                : settings.DuplicateCopyLabel.Trim();
            var duplicateFont = settings.DuplicateCopyFontPt > 0 ? settings.DuplicateCopyFontPt : bodyFont;
            PlaceText(canvas, duplicateLabel,
                settings.DuplicateCopyLeftMm, settings.DuplicateCopyTopMm, settings.DuplicateCopyWidthMm,
                duplicateFont, printFont, TextAlignment.Right, FontWeights.Bold, layout: layout);
        }

        PlaceText(canvas,
            A4PrePrintedText.FormatBillingAddress(input.CustomerName, input.CustomerPhone, settings.BillToMaxChars),
            layout.BillToLeftMm, layout.BillToTopMm, layout.BillToWidthMm, bodyFont, printFont,
            TextAlignment.Left, layout: layout);
        PlaceText(canvas, input.BillNo, layout.InvNoLeftMm, layout.InvNoTopMm, layout.InvNoWidthMm,
            bodyFont, printFont, TextAlignment.Left, layout: layout);
        PlaceText(canvas, input.BillDate, layout.DateLeftMm, layout.DateTopMm, layout.DateWidthMm,
            bodyFont, printFont, TextAlignment.Left, layout: layout);
        PlaceText(canvas, input.OrderNo, layout.OrderNoLeftMm, layout.OrderNoTopMm, layout.OrderNoWidthMm,
            bodyFont, printFont, TextAlignment.Left, layout: layout);
    }

    private static void PlaceText(
        Canvas canvas,
        string text,
        double leftMm,
        double topMm,
        double widthMm,
        double fontPt,
        FontFamily printFont,
        TextAlignment align,
        FontWeight? weight = null,
        bool useTableOffset = false,
        A4PrePrintedInvoiceLayout? layout = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        layout ??= A4PrePrintedInvoiceLayout.FromSettings(A4PrePrintedLayoutSettings.CreateDefault());

        var tb = new TextBlock
        {
            Text = text,
            FontFamily = printFont,
            FontSize = fontPt,
            FontWeight = weight ?? FontWeights.Normal,
            Foreground = Brushes.Black,
            Width = InvoiceImageScaling.MmToPx(widthMm),
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None,
            TextAlignment = align,
            Padding = new Thickness(0),
        };
        var yMm = topMm + layout.TextBaselineNudgeMm;
        Canvas.SetLeft(tb, InvoiceImageScaling.MmToPx(layout.X(leftMm)));
        Canvas.SetTop(tb, InvoiceImageScaling.MmToPx(
            useTableOffset ? layout.TableY(yMm) : layout.Y(yMm)));
        canvas.Children.Add(tb);
    }

    private static string Money(decimal value) => value.ToString("0.00", In);
}
