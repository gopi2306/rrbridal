using System.Collections.Generic;
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

    public static FlowDocument Create(ThermalInvoiceInput input, A5PrePrintedLayoutSettings? layoutSettings = null)
    {
        var settings = layoutSettings ?? A5PrePrintedLayoutSettings.CreateDefault();
        settings.EnsureAlignmentDefaults();
        var layout = A5PrePrintedInvoiceLayout.FromSettings(settings);
        var printFont = A5PrePrintedText.ResolvePrintFont(settings.PrintFontFamily);

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

        var activeLines = InvoiceLinePagination.ActiveLines(input);
        var chunks = InvoiceLinePagination.ChunkLines(activeLines, layout.MaxLineRows);
        var pageCount = chunks.Count;

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var isLastPage = pageIndex == pageCount - 1;
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
        A5PrePrintedInvoiceLayout layout,
        A5PrePrintedLayoutSettings settings,
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
            var amount = line.PrePrintedLineAmount();

            PlaceText(canvas, line.Description, layout.ColDescLeftMm, top, layout.ColDescWidthMm,
                bodyFont, printFont, TextAlignment.Left, useTableOffset: true, layout: layout);
            PlaceText(canvas, $"{line.Qty:0.###}", layout.ColQtyLeftMm, top, layout.ColQtyWidthMm,
                bodyFont, printFont, TextAlignment.Center, useTableOffset: true, layout: layout);
            PlaceText(canvas, Money(line.Rate), layout.ColRateLeftMm, top, layout.ColRateWidthMm,
                bodyFont, printFont, TextAlignment.Right, useTableOffset: true, layout: layout);
            PlaceText(canvas, Money(amount), layout.ColAmountLeftMm, top, layout.ColAmountWidthMm,
                bodyFont, printFont, TextAlignment.Right, useTableOffset: true, layout: layout);
        }

        if (hasMorePages)
        {
            var totalQtyTop = layout.TableTopMm + pageLines.Count * layout.LineRowHeightMm;
            PlaceText(canvas, A5PrePrintedText.FormatTotalQty(input.TotalQty),
                layout.TotalQtyLeftMm, totalQtyTop, layout.TotalQtyWidthMm,
                bodyFont, printFont, TextAlignment.Center, useTableOffset: true, layout: layout);

            var continuedTop = totalQtyTop + layout.LineRowHeightMm;
            var continuedLeft = settings.ContinuedLeftMm > 0 ? settings.ContinuedLeftMm : layout.ColDescLeftMm;
            var continuedWidth = settings.ContinuedWidthMm > 0 ? settings.ContinuedWidthMm : layout.ColDescWidthMm;
            PlaceText(canvas, A5PrePrintedText.ResolveContinuedLabel(settings),
                continuedLeft, continuedTop, continuedWidth,
                bodyFont, printFont, TextAlignment.Left, useTableOffset: true, layout: layout);
        }

        if (isLastPage)
        {
            if (input.ItemDiscountPercent > 0 || input.ManualDiscountAmount > 0)
            {
                PlaceText(canvas, A5PrePrintedText.FormatDiscountName(input.ItemDiscountPercent),
                    layout.DiscountPercentLeftMm, layout.DiscountPercentTopMm, layout.DiscountPercentWidthMm,
                    bodyFont, printFont, TextAlignment.Left, useTableOffset: true, layout: layout);

                var discountAmountText = A5PrePrintedText.FormatDiscountAmount(input.ManualDiscountAmount);
                if (!string.IsNullOrEmpty(discountAmountText))
                {
                    PlaceText(canvas, discountAmountText,
                        layout.DiscountAmountLeftMm, layout.DiscountAmountTopMm, layout.DiscountAmountWidthMm,
                        bodyFont, printFont, TextAlignment.Right, useTableOffset: true, layout: layout);
                }
            }

            PlaceText(canvas, Money(input.Payable), layout.TotalAmountLeftMm, layout.TotalAmountTopMm,
                layout.TotalAmountWidthMm, totalFont, printFont, TextAlignment.Right, FontWeights.Bold,
                useTableOffset: true, layout: layout);
        }

        return canvas;
    }

    private static void PlaceHeader(
        Canvas canvas,
        ThermalInvoiceInput input,
        A5PrePrintedInvoiceLayout layout,
        A5PrePrintedLayoutSettings settings,
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

        PlaceText(canvas, A5PrePrintedText.FormatBillTo(input.CustomerName, settings.BillToMaxChars),
            layout.BillToLeftMm, layout.BillToTopMm, layout.BillToWidthMm, bodyFont, printFont,
            TextAlignment.Left, singleLine: true, layout: layout);
        PlaceText(canvas, input.BillNo, layout.InvNoLeftMm, layout.InvNoTopMm, layout.InvNoWidthMm,
            bodyFont, printFont, TextAlignment.Left, layout: layout);
        PlaceText(canvas, input.BillDate, layout.DateLeftMm, layout.DateTopMm, layout.DateWidthMm,
            bodyFont, printFont, TextAlignment.Left, layout: layout);
        PlaceText(canvas, input.CustomerPhone, layout.ContactLeftMm, layout.MetaRow2TopMm, layout.ContactWidthMm,
            bodyFont, printFont, TextAlignment.Left, layout: layout);

        if (input.Stitching)
        {
            PlaceText(canvas, "✓", layout.StitchingTickLeftMm, layout.StitchingTickTopMm, layout.StitchingTickSizeMm,
                bodyFont, printFont, TextAlignment.Center, FontWeights.Bold, layout: layout);
            PlaceText(canvas, input.DeliveryDate, layout.DeliveryDateLeftMm, layout.DeliveryDateTopMm,
                layout.DeliveryDateWidthMm, bodyFont, printFont, TextAlignment.Left, layout: layout);
        }
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
        bool singleLine = false,
        A5PrePrintedInvoiceLayout? layout = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        layout ??= A5PrePrintedInvoiceLayout.FromSettings(A5PrePrintedLayoutSettings.CreateDefault());

        var tb = new TextBlock
        {
            Text = text,
            FontFamily = printFont,
            FontSize = fontPt,
            FontWeight = weight ?? FontWeights.Normal,
            Foreground = Brushes.Black,
            Width = InvoiceImageScaling.MmToPx(widthMm),
            TextWrapping = singleLine ? TextWrapping.NoWrap : TextWrapping.Wrap,
            TextTrimming = TextTrimming.None,
            TextAlignment = align,
            Padding = new Thickness(0, 0, 0, 0),
        };
        var yMm = topMm + layout.TextBaselineNudgeMm;
        Canvas.SetLeft(tb, InvoiceImageScaling.MmToPx(layout.X(leftMm)));
        Canvas.SetTop(tb, InvoiceImageScaling.MmToPx(
            useTableOffset ? layout.TableY(yMm) : layout.Y(yMm)));
        canvas.Children.Add(tb);
    }

    private static string Money(decimal value) => value.ToString("0.00", In);
}
