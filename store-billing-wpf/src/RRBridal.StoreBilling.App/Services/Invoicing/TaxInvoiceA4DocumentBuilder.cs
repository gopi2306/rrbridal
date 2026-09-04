using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>A4 Tax Invoice FlowDocument with CGST/SGST (or IGST), HSN GST split, and bank details.</summary>
public static class TaxInvoiceA4DocumentBuilder
{
    private static readonly CultureInfo In = CultureInfo.GetCultureInfo("en-IN");
    private const string UnitPer = "NOS";
    private const int LineColCount = 8;
    private const int AmountColIndex = LineColCount - 1;

    public static FlowDocument Create(ThermalInvoiceInput input, int linesPerPage = TaxInvoiceA4Layout.LinesPerPage)
    {
        var pageWidth = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.PageWidthMm);
        var pageHeight = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.PageHeightMm);
        var margin = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.PageMarginMm);
        var contentWidth = pageWidth - margin * 2;

        var doc = new FlowDocument
        {
            PageWidth = pageWidth,
            PageHeight = pageHeight,
            PagePadding = new Thickness(0),
            ColumnWidth = pageWidth,
            IsColumnWidthFlexible = false,
            IsOptimalParagraphEnabled = false,
            FontFamily = TaxInvoiceA4Visuals.BodyFont,
            Background = Brushes.White,
        };

        var activeLines = InvoiceLinePagination.ActiveLines(input);
        var lastPageMax = Math.Min(linesPerPage, TaxInvoiceA4Layout.LastPageLinesPerPage);
        var chunks = InvoiceLinePagination.ChunkLinesFillThenFooterPage(activeLines, linesPerPage, lastPageMax);

        for (var pageIndex = 0; pageIndex < chunks.Count; pageIndex++)
        {
            var isLastPage = pageIndex == chunks.Count - 1;
            var hasMoreLinePages = !isLastPage && chunks[pageIndex + 1].Count > 0;
            var showHeader = pageIndex == 0;
            var pageVisual = BuildPageVisual(
                input, contentWidth, pageWidth, pageHeight, margin,
                chunks[pageIndex], isLastPage, hasMoreLinePages, showHeader);

            if (pageIndex == 0)
                doc.Blocks.Add(new BlockUIContainer(pageVisual));
            else
            {
                var section = new Section { BreakPageBefore = true };
                section.Blocks.Add(new BlockUIContainer(pageVisual));
                doc.Blocks.Add(section);
            }
        }

        return doc;
    }

    private static FrameworkElement BuildPageVisual(
        ThermalInvoiceInput input,
        double contentWidth,
        double pageWidth,
        double pageHeight,
        double margin,
        IReadOnlyList<InvoiceLineSnap> pageLines,
        bool isLastPage,
        bool hasMoreLinePages,
        bool showHeader)
    {
        var bodyPt = TaxInvoiceA4Layout.BodyPt;
        var smallPt = TaxInvoiceA4Layout.SmallPt;

        var root = new Grid
        {
            Width = pageWidth,
            Height = pageHeight,
            Background = Brushes.White,
        };

        var content = new Grid
        {
            Margin = new Thickness(margin),
            Width = contentWidth,
            Height = pageHeight - margin * 2,
        };

        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // title
        if (showHeader)
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // meta
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // table
        var tableRow = showHeader ? 2 : 1;
        var footerStartRow = tableRow + 1;
        if (isLastPage)
        {
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // subtotal / tax / total
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // amount words
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // gst split
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // tax words
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // declaration/bank/sig
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer note
        }
        root.Children.Add(content);

        var row = 0;
        var title = BuildTitleRow(contentWidth, bodyPt, showHeader, input.BillNo);
        Grid.SetRow(title, row++);
        content.Children.Add(title);

        if (showHeader)
        {
            var meta = BuildMetaSection(input, contentWidth, bodyPt, smallPt);
            Grid.SetRow(meta, row++);
            content.Children.Add(meta);
        }

        var table = BuildLineTable(input, contentWidth, pageLines, hasMoreLinePages);
        Grid.SetRow(table, tableRow);
        content.Children.Add(table);

        if (isLastPage)
        {
            var taxTotals = TaxInvoiceA4GstBreakdown.ComputeTotals(input);
            var footerPt = TaxInvoiceA4Layout.FooterBodyPt;
            var footerSmallPt = TaxInvoiceA4Layout.FooterSmallPt;

            var totalsBlock = BuildTotalsBlock(input, taxTotals, contentWidth);
            Grid.SetRow(totalsBlock, footerStartRow);
            content.Children.Add(totalsBlock);

            var amountInWords = BuildAmountInWords(input, contentWidth, footerPt);
            Grid.SetRow(amountInWords, footerStartRow + 1);
            content.Children.Add(amountInWords);

            var gstSplit = BuildGstSplitTable(input, contentWidth, footerPt, footerSmallPt);
            Grid.SetRow(gstSplit, footerStartRow + 2);
            content.Children.Add(gstSplit);

            var taxWords = BuildTaxAmountInWords(taxTotals.TotalTax, contentWidth, footerPt);
            Grid.SetRow(taxWords, footerStartRow + 3);
            content.Children.Add(taxWords);

            var declaration = BuildDeclarationFooter(input, contentWidth, footerPt, footerSmallPt);
            Grid.SetRow(declaration, footerStartRow + 4);
            content.Children.Add(declaration);

            var footerNote = SectionBorder(
                TaxInvoiceA4Visuals.Text(
                    "This is a Computer Generated Invoice",
                    footerSmallPt,
                    align: TextAlignment.Center),
                new Thickness(1, 0, 1, 1),
                FooterSectionPadding());
            Grid.SetRow(footerNote, footerStartRow + 5);
            content.Children.Add(footerNote);
        }

        return root;
    }

    private static UIElement BuildTitleRow(double contentWidth, double bodyPt, bool showHeader, string? billNo)
    {
        var grid = new Grid { Width = contentWidth };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var titleText = showHeader
            ? "Tax Invoice"
            : $"Tax Invoice (Continued){(string.IsNullOrWhiteSpace(billNo) ? "" : $" — {billNo}")}";
        var title = TaxInvoiceA4Visuals.Text(
            titleText,
            showHeader ? TaxInvoiceA4Layout.TitlePt : TaxInvoiceA4Layout.BodyPt,
            FontWeights.Bold,
            TextAlignment.Center);
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);

        var printed = TaxInvoiceA4Visuals.Text(
            $"Printed on {DateTime.Now:dd-MMM-yy} at {DateTime.Now:HH:mm}",
            bodyPt,
            align: TextAlignment.Right);
        Grid.SetColumn(printed, 2);
        grid.Children.Add(printed);

        return SectionBorder(grid, new Thickness(1, 1, 1, 1));
    }

    private static Thickness SectionPadding()
    {
        var pad = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.SectionPaddingMm);
        return new Thickness(pad);
    }

    private static Thickness FooterSectionPadding()
    {
        var pad = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.FooterSectionPaddingMm);
        return new Thickness(pad, pad * 0.6, pad, pad * 0.6);
    }

    private static Border SectionBorder(UIElement child, Thickness border, Thickness? padding = null)
        => TaxInvoiceA4Visuals.BorderedCell(child, border, padding ?? SectionPadding());

    private static UIElement BuildMetaSection(ThermalInvoiceInput input, double contentWidth, double bodyPt, double smallPt)
    {
        const int metaRows = 7;
        var rowMinH = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.MetaRowHeightMm);
        var sectionPad = SectionPadding();
        var metaPt = TaxInvoiceA4Layout.MetaLabelPt;

        var outer = new Grid
        {
            Width = contentWidth,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
        };
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TaxInvoiceA4Layout.MetaLeftColumnWeight, GridUnitType.Star) });
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TaxInvoiceA4Layout.MetaRightColumnWeight, GridUnitType.Star) });
        for (var r = 0; r < metaRows; r++)
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = rowMinH });

        var seller = BuildSellerBlock(input, bodyPt, smallPt, sectionPad);
        Grid.SetRow(seller, 0);
        Grid.SetRowSpan(seller, 3);
        Grid.SetColumn(seller, 0);
        outer.Children.Add(seller);

        var consignee = BuildPartyBlock("Consignee (Ship to)", input, bodyPt, smallPt, sectionPad, drawBottomBorder: true);
        Grid.SetRow(consignee, 3);
        Grid.SetRowSpan(consignee, 2);
        Grid.SetColumn(consignee, 0);
        outer.Children.Add(consignee);

        var buyer = BuildPartyBlock("Buyer (Bill to)", input, bodyPt, smallPt, sectionPad, drawBottomBorder: false);
        Grid.SetRow(buyer, 5);
        Grid.SetRowSpan(buyer, 2);
        Grid.SetColumn(buyer, 0);
        outer.Children.Add(buyer);

        var divider = new Border
        {
            Background = TaxInvoiceA4Visuals.BorderBrush,
            Width = 1,
            SnapsToDevicePixels = true,
        };
        Grid.SetRow(divider, 0);
        Grid.SetRowSpan(divider, metaRows);
        Grid.SetColumn(divider, 1);
        outer.Children.Add(divider);

        var metaRowsData = new (string LeftLabel, string LeftValue, string RightLabel, string RightValue)[]
        {
            ("Invoice No.", input.BillNo, "Dated", input.BillDate),
            ("Delivery Note", "", "Mode/Terms of Payment", ""),
            ("Reference No. & Date.", "", "Other References", ""),
            ("Buyer's Order No.", "", "Dated", ""),
            ("Dispatch Doc No.", "", "Delivery Note Date", ""),
            ("Dispatched through", "", "Destination", ""),
            ("Salesman", input.UserName, "Terms of Delivery", ""),
        };

        for (var r = 0; r < metaRows; r++)
        {
            var row = metaRowsData[r];
            var cell = TaxInvoiceA4Visuals.MetaSplitRowCell(
                row.LeftLabel, row.LeftValue, row.RightLabel, row.RightValue, metaPt,
                sectionPad,
                drawBottomBorder: r < metaRows - 1);
            Grid.SetRow(cell, r);
            Grid.SetColumn(cell, 2);
            outer.Children.Add(cell);
        }

        return SectionBorder(outer, new Thickness(1, 0, 1, 1));
    }

    private static UIElement BuildSellerBlock(ThermalInvoiceInput input, double bodyPt, double smallPt, Thickness sectionPad)
    {
        var store = input.Store;
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        stack.Children.Add(TaxInvoiceA4Visuals.Text(store.StoreName.ToUpperInvariant(), bodyPt, FontWeights.Bold, verticalAlign: VerticalAlignment.Top));
        if (!string.IsNullOrWhiteSpace(store.Address))
        {
            foreach (var line in store.Address.Split('\n', '\r').Select(l => l.Trim()).Where(l => l.Length > 0))
                stack.Children.Add(TaxInvoiceA4Visuals.Text(line, smallPt, verticalAlign: VerticalAlignment.Top));
        }

        var stateLine = GstStateCodeResolver.FormatStateLine(store.StateName, store.Gstin);
        if (!string.IsNullOrWhiteSpace(stateLine))
            stack.Children.Add(TaxInvoiceA4Visuals.Text(stateLine, smallPt, verticalAlign: VerticalAlignment.Top));

        if (!string.IsNullOrWhiteSpace(store.Gstin))
            stack.Children.Add(TaxInvoiceA4Visuals.Text($"GSTIN/UIN: {store.Gstin}", smallPt, verticalAlign: VerticalAlignment.Top));

        return TaxInvoiceA4Visuals.MetaBlockCell(stack, sectionPad, drawBottomBorder: true);
    }

    private static UIElement BuildPartyBlock(string label, ThermalInvoiceInput input, double bodyPt, double smallPt, Thickness sectionPad, bool drawBottomBorder)
    {
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        stack.Children.Add(TaxInvoiceA4Visuals.Text(label, bodyPt, FontWeights.Bold, verticalAlign: VerticalAlignment.Top));
        stack.Children.Add(TaxInvoiceA4Visuals.Text(FormatPartyNameLine(input), bodyPt, verticalAlign: VerticalAlignment.Top));

        var buyerGstin = (input.CustomerGstin ?? "").Trim();
        stack.Children.Add(TaxInvoiceA4Visuals.Text(
            string.IsNullOrEmpty(buyerGstin) ? "GSTIN/UIN:" : $"GSTIN/UIN: {buyerGstin}",
            smallPt,
            verticalAlign: VerticalAlignment.Top));

        return TaxInvoiceA4Visuals.MetaBlockCell(stack, sectionPad, drawBottomBorder);
    }

    private static string FormatPartyNameLine(ThermalInvoiceInput input)
    {
        var name = input.CustomerName?.Trim() ?? "";
        var phone = input.CustomerPhone?.Trim() ?? "";
        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(phone))
            return "";
        if (string.IsNullOrEmpty(phone))
            return name;
        if (string.IsNullOrEmpty(name))
            return phone;
        return $"{name} {phone}";
    }

    private static UIElement BuildLineTable(
        ThermalInvoiceInput input,
        double contentWidth,
        IReadOnlyList<InvoiceLineSnap> pageLines,
        bool hasMorePages)
    {
        var colWidths = TaxInvoiceA4Layout.ComputeColumnWidths(contentWidth);
        var headerPt = TaxInvoiceA4Layout.TableHeaderPt;
        var rowPt = TaxInvoiceA4Layout.TableRowPt;
        var rowH = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.TableRowHeightMm);
        var cellPadH = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.TableCellPaddingHorizontalMm);
        var cellPadV = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.TableCellPaddingVerticalMm);
        var cellPadding = new Thickness(cellPadH, cellPadV, cellPadH, cellPadV);

        var spacerRow = 1 + pageLines.Count;

        var table = new Grid
        {
            Width = contentWidth,
            VerticalAlignment = VerticalAlignment.Stretch,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
        };
        for (var c = 0; c < LineColCount; c++)
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(colWidths[c]) });

        table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowH) });
        for (var i = 0; i < pageLines.Count; i++)
            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowH) });
        table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var headers = new[] { "SI No.", "Description of Goods", "HSN/SAC", "Quantity", "MRP", "Rate", "per", "Amount" };
        for (var c = 0; c < LineColCount; c++)
            AddTableCell(table, 0, c, headers[c], headerPt, FontWeights.Bold, TableColumnAlign(c), TableCellBorder.Header, cellPadding: cellPadding);

        var rowIndex = 1;
        foreach (var line in pageLines)
        {
            AddTableCell(table, rowIndex, 0, line.LineNo.ToString(In), rowPt, FontWeights.Normal, TableColumnAlign(0), TableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 1, line.Description, rowPt, FontWeights.Normal, TableColumnAlign(1), TableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 2, line.Hsn, rowPt, FontWeights.Normal, TableColumnAlign(2), TableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 3, FormatQty(line.Qty), rowPt, FontWeights.Normal, TableColumnAlign(3), TableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 4, line.Mrp > 0 ? Money(line.Mrp) : "", rowPt, FontWeights.Normal, TableColumnAlign(4), TableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 5, Money(line.Rate), rowPt, FontWeights.Normal, TableColumnAlign(5), TableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 6, UnitPer, rowPt, FontWeights.Normal, TableColumnAlign(6), TableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 7, Money(TaxInvoiceA4GstBreakdown.LineProductAmount(line)), rowPt, FontWeights.Normal, TableColumnAlign(7), TableCellBorder.Body, cellPadding: cellPadding);
            rowIndex++;
        }

        AddSpacerRow(table, spacerRow, hasMorePages ? "Continued..." : "", rowPt, cellPadding);

        return SectionBorder(table, new Thickness(1, 0, 1, 0), new Thickness(0));
    }

    /// <summary>
    /// Subtotal / CGST / SGST / Total as a dedicated Auto block so it cannot be clipped
    /// when the line-item Star row shrinks under GST + declaration content.
    /// </summary>
    private static UIElement BuildTotalsBlock(
        ThermalInvoiceInput input,
        TaxInvoiceA4GstBreakdown.BillTaxTotals taxTotals,
        double contentWidth)
    {
        var colWidths = TaxInvoiceA4Layout.ComputeColumnWidths(contentWidth);
        var rowPt = TaxInvoiceA4Layout.FooterBodyPt;
        var rowH = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.FooterTableRowHeightMm);
        var cellPadH = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.TableCellPaddingHorizontalMm);
        var cellPadV = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.TableCellPaddingVerticalMm);
        var cellPadding = new Thickness(cellPadH, cellPadV, cellPadH, cellPadV);
        var showDiscount = input.ManualDiscountAmount > 0;
        var taxFooterRows = input.IsInterState ? 1 : 2;
        var footerRows = 1 + (showDiscount ? 2 : 0) + taxFooterRows + 1;

        var table = new Grid
        {
            Width = contentWidth,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
        };
        for (var c = 0; c < LineColCount; c++)
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(colWidths[c]) });
        for (var i = 0; i < footerRows; i++)
            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowH) });

        var footerRow = 0;
        var subtotal = ComputeDisplayedSubtotal(input, taxTotals);
        AddLabelAmountFooterRow(table, footerRow, "Subtotal", Money(subtotal), rowPt, TableCellBorder.Footer, cellPadding, labelBold: true, labelAlign: TextAlignment.Right);
        footerRow++;

        if (showDiscount)
        {
            AddLabelAmountFooterRow(
                table,
                footerRow,
                "Less : DISCOUNT",
                $"(-) {Money(input.ManualDiscountAmount)}",
                rowPt,
                TableCellBorder.Footer,
                cellPadding);
            footerRow++;

            var discountedSubtotal = ComputeDiscountedSubtotal(subtotal, input, taxTotals);
            AddLabelAmountFooterRow(
                table,
                footerRow,
                "Discounted Subtotal",
                Money(discountedSubtotal),
                rowPt,
                TableCellBorder.Footer,
                cellPadding,
                labelBold: true,
                labelAlign: TextAlignment.Right);
            footerRow++;
        }

        if (input.IsInterState)
        {
            AddLabelAmountFooterRow(table, footerRow, "IGST", Money(taxTotals.Igst), rowPt, TableCellBorder.Footer, cellPadding);
            footerRow++;
        }
        else
        {
            AddLabelAmountFooterRow(table, footerRow, "CGST", Money(taxTotals.Cgst), rowPt, TableCellBorder.Footer, cellPadding);
            footerRow++;
            AddLabelAmountFooterRow(table, footerRow, "SGST", Money(taxTotals.Sgst), rowPt, TableCellBorder.Footer, cellPadding);
            footerRow++;
        }

        AddTableCell(table, footerRow, 0, "", rowPt, FontWeights.Normal, TableColumnAlign(0), TableCellBorder.FooterLast, cellPadding: cellPadding);
        AddTableCell(table, footerRow, 1, "Total", rowPt, FontWeights.Bold, TextAlignment.Left, TableCellBorder.FooterLast, cellPadding: cellPadding);
        AddTableCell(table, footerRow, 2, "", rowPt, FontWeights.Normal, TableColumnAlign(2), TableCellBorder.FooterLast, cellPadding: cellPadding);
        AddTableCell(table, footerRow, 3, $"{FormatQty(input.TotalQty)} {UnitPer}", rowPt, FontWeights.Bold, TableColumnAlign(3), TableCellBorder.FooterLast, cellPadding: cellPadding);
        AddTableCell(table, footerRow, 4, "", rowPt, FontWeights.Normal, TableColumnAlign(4), TableCellBorder.FooterLast, cellPadding: cellPadding);
        AddTableCell(table, footerRow, 5, "", rowPt, FontWeights.Normal, TableColumnAlign(5), TableCellBorder.FooterLast, cellPadding: cellPadding);
        AddTableCell(table, footerRow, 6, "", rowPt, FontWeights.Normal, TableColumnAlign(6), TableCellBorder.FooterLast, cellPadding: cellPadding);
        AddTableCell(table, footerRow, AmountColIndex, Money(input.Payable), rowPt, FontWeights.Bold, TableColumnAlign(AmountColIndex), TableCellBorder.FooterLast, cellPadding: cellPadding);

        return SectionBorder(table, new Thickness(1, 0, 1, 0), new Thickness(0));
    }

    private static void AddLabelAmountFooterRow(
        Grid table,
        int row,
        string label,
        string amountValue,
        double rowPt,
        TableCellBorder border,
        Thickness cellPadding,
        bool labelBold = false,
        TextAlignment labelAlign = TextAlignment.Left)
    {
        AddTableCell(table, row, 0, "", rowPt, FontWeights.Normal, TableColumnAlign(0), border, cellPadding: cellPadding);
        AddTableCell(
            table,
            row,
            1,
            label,
            rowPt,
            labelBold ? FontWeights.Bold : FontWeights.Normal,
            labelAlign,
            border,
            cellPadding: cellPadding);
        AddTableCell(table, row, 2, "", rowPt, FontWeights.Normal, TableColumnAlign(2), border, cellPadding: cellPadding);
        AddTableCell(table, row, 3, "", rowPt, FontWeights.Normal, TableColumnAlign(3), border, cellPadding: cellPadding);
        AddTableCell(table, row, 4, "", rowPt, FontWeights.Normal, TableColumnAlign(4), border, cellPadding: cellPadding);
        AddTableCell(table, row, 5, "", rowPt, FontWeights.Normal, TableColumnAlign(5), border, cellPadding: cellPadding);
        AddTableCell(table, row, 6, "", rowPt, FontWeights.Normal, TableColumnAlign(6), border, cellPadding: cellPadding);
        AddTableCell(table, row, AmountColIndex, amountValue, rowPt, FontWeights.Normal, TableColumnAlign(AmountColIndex), border, cellPadding: cellPadding);
    }

    private static UIElement BuildAmountInWords(ThermalInvoiceInput input, double contentWidth, double bodyPt)
    {
        var stack = new StackPanel();
        stack.Children.Add(TaxInvoiceA4Visuals.Text(
            $"Amount Chargeable (in words): {IndianAmountInWords.ForRupee(input.Payable)}",
            bodyPt,
            FontWeights.SemiBold));
        return SectionBorder(stack, new Thickness(1, 0, 1, 1), FooterSectionPadding());
    }

    private static UIElement BuildTaxAmountInWords(decimal totalTax, double contentWidth, double bodyPt)
    {
        var stack = new StackPanel();
        stack.Children.Add(TaxInvoiceA4Visuals.Text(
            $"Tax Amount (in words): {IndianAmountInWords.ForRupee(totalTax)}",
            bodyPt,
            FontWeights.SemiBold));
        return SectionBorder(stack, new Thickness(1, 0, 1, 1), FooterSectionPadding());
    }

    private static UIElement BuildGstSplitTable(
        ThermalInvoiceInput input,
        double contentWidth,
        double bodyPt,
        double smallPt)
    {
        var rows = TaxInvoiceA4GstBreakdown.BuildHsnRows(input);
        var inter = input.IsInterState;
        var headerPt = TaxInvoiceA4Layout.FooterSmallPt;
        var rowPt = TaxInvoiceA4Layout.FooterSmallPt;
        var rowH = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.FooterTableRowHeightMm);
        var cellPadH = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.TableCellPaddingHorizontalMm);
        var cellPadV = InvoiceImageScaling.MmToPx(0.25);
        var cellPadding = new Thickness(cellPadH, cellPadV, cellPadH, cellPadV);

        var sectionPadPx = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.FooterSectionPaddingMm);
        var innerWidth = Math.Max(1, contentWidth - sectionPadPx * 2);

        var colCount = inter ? 5 : 7;
        // Widen amount / Total Tax Amount columns so values like 1,530.76 are not clipped.
        double[] weights = inter
            ? new[] { 1.0, 1.35, 0.7, 1.25, 1.5 }
            : new[] { 0.95, 1.25, 0.55, 1.1, 0.55, 1.1, 1.5 };
        var widths = new double[colCount];
        var sumW = weights.Sum();
        for (var i = 0; i < colCount; i++)
            widths[i] = innerWidth * weights[i] / sumW;

        var grid = new Grid
        {
            Width = innerWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            ClipToBounds = false,
        };
        for (var c = 0; c < colCount; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widths[c]) });

        // Header uses 2 rows for CGST/SGST rate+amount groups
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowH) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowH) });
        foreach (var _ in rows)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowH) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowH) }); // total

        if (inter)
        {
            AddGstCell(grid, 0, 0, "HSN/SAC", headerPt, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Header, cellPadding, rowSpan: 2);
            AddGstCell(grid, 0, 1, "Taxable Value", headerPt, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Header, cellPadding, rowSpan: 2);
            AddGstCell(grid, 0, 2, "IGST", headerPt, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Header, cellPadding, colSpan: 2);
            AddGstCell(grid, 0, 4, "Total Tax Amount", headerPt, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Header, cellPadding, rowSpan: 2);
            AddGstCell(grid, 1, 2, "Rate", headerPt, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Header, cellPadding);
            AddGstCell(grid, 1, 3, "Amount", headerPt, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Header, cellPadding);
        }
        else
        {
            AddGstCell(grid, 0, 0, "HSN/SAC", headerPt, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Header, cellPadding, rowSpan: 2);
            AddGstCell(grid, 0, 1, "Taxable Value", headerPt, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Header, cellPadding, rowSpan: 2);
            AddGstCell(grid, 0, 2, "Central Tax", headerPt, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Header, cellPadding, colSpan: 2);
            AddGstCell(grid, 0, 4, "State Tax", headerPt, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Header, cellPadding, colSpan: 2);
            AddGstCell(grid, 0, 6, "Total Tax Amount", headerPt, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Header, cellPadding, rowSpan: 2);
            AddGstCell(grid, 1, 2, "Rate", headerPt, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Header, cellPadding);
            AddGstCell(grid, 1, 3, "Amount", headerPt, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Header, cellPadding);
            AddGstCell(grid, 1, 4, "Rate", headerPt, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Header, cellPadding);
            AddGstCell(grid, 1, 5, "Amount", headerPt, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Header, cellPadding);
        }

        var dataRow = 2;
        decimal sumTaxable = 0, sumCgst = 0, sumSgst = 0, sumIgst = 0, sumTax = 0;
        foreach (var r in rows)
        {
            sumTaxable += r.TaxableValue;
            sumCgst += r.CgstAmount;
            sumSgst += r.SgstAmount;
            sumIgst += r.IgstAmount;
            sumTax += r.TotalTax;

            if (inter)
            {
                AddGstCell(grid, dataRow, 0, r.Hsn, rowPt, FontWeights.Normal, TextAlignment.Center, TableCellBorder.Body, cellPadding);
                AddGstCell(grid, dataRow, 1, Money(r.TaxableValue), rowPt, FontWeights.Normal, TextAlignment.Right, TableCellBorder.Body, cellPadding);
                AddGstCell(grid, dataRow, 2, FormatPercent(r.IgstRate), rowPt, FontWeights.Normal, TextAlignment.Right, TableCellBorder.Body, cellPadding);
                AddGstCell(grid, dataRow, 3, Money(r.IgstAmount), rowPt, FontWeights.Normal, TextAlignment.Right, TableCellBorder.Body, cellPadding);
                AddGstCell(grid, dataRow, 4, Money(r.TotalTax), rowPt, FontWeights.Normal, TextAlignment.Right, TableCellBorder.Body, cellPadding);
            }
            else
            {
                AddGstCell(grid, dataRow, 0, r.Hsn, rowPt, FontWeights.Normal, TextAlignment.Center, TableCellBorder.Body, cellPadding);
                AddGstCell(grid, dataRow, 1, Money(r.TaxableValue), rowPt, FontWeights.Normal, TextAlignment.Right, TableCellBorder.Body, cellPadding);
                AddGstCell(grid, dataRow, 2, FormatPercent(r.CgstRate), rowPt, FontWeights.Normal, TextAlignment.Right, TableCellBorder.Body, cellPadding);
                AddGstCell(grid, dataRow, 3, Money(r.CgstAmount), rowPt, FontWeights.Normal, TextAlignment.Right, TableCellBorder.Body, cellPadding);
                AddGstCell(grid, dataRow, 4, FormatPercent(r.SgstRate), rowPt, FontWeights.Normal, TextAlignment.Right, TableCellBorder.Body, cellPadding);
                AddGstCell(grid, dataRow, 5, Money(r.SgstAmount), rowPt, FontWeights.Normal, TextAlignment.Right, TableCellBorder.Body, cellPadding);
                AddGstCell(grid, dataRow, 6, Money(r.TotalTax), rowPt, FontWeights.Normal, TextAlignment.Right, TableCellBorder.Body, cellPadding);
            }

            dataRow++;
        }

        var totalBorder = TableCellBorder.FooterLast;
        if (inter)
        {
            AddGstCell(grid, dataRow, 0, "Total", rowPt, FontWeights.Bold, TextAlignment.Left, totalBorder, cellPadding);
            AddGstCell(grid, dataRow, 1, Money(sumTaxable), rowPt, FontWeights.Bold, TextAlignment.Right, totalBorder, cellPadding);
            AddGstCell(grid, dataRow, 2, "", rowPt, FontWeights.Normal, TextAlignment.Right, totalBorder, cellPadding);
            AddGstCell(grid, dataRow, 3, Money(sumIgst), rowPt, FontWeights.Bold, TextAlignment.Right, totalBorder, cellPadding);
            AddGstCell(grid, dataRow, 4, Money(sumTax), rowPt, FontWeights.Bold, TextAlignment.Right, totalBorder, cellPadding);
        }
        else
        {
            AddGstCell(grid, dataRow, 0, "Total", rowPt, FontWeights.Bold, TextAlignment.Left, totalBorder, cellPadding);
            AddGstCell(grid, dataRow, 1, Money(sumTaxable), rowPt, FontWeights.Bold, TextAlignment.Right, totalBorder, cellPadding);
            AddGstCell(grid, dataRow, 2, "", rowPt, FontWeights.Normal, TextAlignment.Right, totalBorder, cellPadding);
            AddGstCell(grid, dataRow, 3, Money(sumCgst), rowPt, FontWeights.Bold, TextAlignment.Right, totalBorder, cellPadding);
            AddGstCell(grid, dataRow, 4, "", rowPt, FontWeights.Normal, TextAlignment.Right, totalBorder, cellPadding);
            AddGstCell(grid, dataRow, 5, Money(sumSgst), rowPt, FontWeights.Bold, TextAlignment.Right, totalBorder, cellPadding);
            AddGstCell(grid, dataRow, 6, Money(sumTax), rowPt, FontWeights.Bold, TextAlignment.Right, totalBorder, cellPadding);
        }

        var stack = new StackPanel();
        stack.Children.Add(TaxInvoiceA4Visuals.Text("GST breakup", bodyPt, FontWeights.Bold));
        stack.Children.Add(grid);
        return SectionBorder(stack, new Thickness(1, 0, 1, 1), FooterSectionPadding());
    }

    private static void AddGstCell(
        Grid table,
        int row,
        int col,
        string text,
        double fontSize,
        FontWeight weight,
        TextAlignment align,
        TableCellBorder borderKind,
        Thickness cellPadding,
        int colSpan = 1,
        int rowSpan = 1)
    {
        var lastCol = table.ColumnDefinitions.Count - 1;
        var left = col > 0 ? 1 : 0;
        var right = col + colSpan - 1 >= lastCol ? 1 : 0;
        var thickness = borderKind switch
        {
            TableCellBorder.Header => new Thickness(left, 0, right, 1),
            TableCellBorder.FooterLast => new Thickness(left, 1, right, 1),
            _ => new Thickness(left, 0, right, 0),
        };

        // Slightly tighter padding on amount columns so N2 values are not clipped by the border.
        var isAmountCol = IsGstAmountColumn(table.ColumnDefinitions.Count, col);
        var pad = isAmountCol
            ? new Thickness(
                Math.Max(1, cellPadding.Left * 0.5),
                cellPadding.Top,
                Math.Max(1, cellPadding.Right * 0.5),
                cellPadding.Bottom)
            : cellPadding;

        var cell = new Border
        {
            BorderBrush = TaxInvoiceA4Visuals.BorderBrush,
            BorderThickness = thickness,
            Padding = pad,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SnapsToDevicePixels = true,
            ClipToBounds = false,
            Child = TaxInvoiceA4Visuals.Text(
                text,
                fontSize,
                weight,
                align,
                wrap: borderKind == TableCellBorder.Header),
        };
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, col);
        if (colSpan > 1)
            Grid.SetColumnSpan(cell, colSpan);
        if (rowSpan > 1)
            Grid.SetRowSpan(cell, rowSpan);
        table.Children.Add(cell);
    }

    private static bool IsGstAmountColumn(int colCount, int col) =>
        colCount == 5
            ? col is 1 or 3 or 4
            : col is 1 or 3 or 5 or 6;

    private static UIElement BuildDeclarationFooter(ThermalInvoiceInput input, double contentWidth, double bodyPt, double smallPt)
    {
        var sectionPad = FooterSectionPadding();
        var grid = new Grid
        {
            Width = contentWidth,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.42, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.30, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.28, GridUnitType.Star) });

        var decl = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        decl.Children.Add(TaxInvoiceA4Visuals.Text("Declaration", bodyPt, FontWeights.Bold, verticalAlign: VerticalAlignment.Top));
        decl.Children.Add(TaxInvoiceA4Visuals.Text(
            "We declare that this invoice shows the actual price of the goods described and that all particulars are true and correct.",
            smallPt,
            verticalAlign: VerticalAlignment.Top));

        var idx = 1;
        if (!string.IsNullOrWhiteSpace(input.Store.TermsAndConditions))
        {
            decl.Children.Add(TaxInvoiceA4Visuals.Text($"{idx}. {input.Store.TermsAndConditions}", smallPt, verticalAlign: VerticalAlignment.Top));
            idx++;
        }

        // Cap policy lines so the footer stays compact enough for 10–15 items on page 1.
        const int maxPolicyLines = 3;
        var policyShown = 0;
        foreach (var line in input.Store.PolicyLines ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (policyShown >= maxPolicyLines)
                break;
            decl.Children.Add(TaxInvoiceA4Visuals.Text($"{idx}. {line}", smallPt, verticalAlign: VerticalAlignment.Top));
            idx++;
            policyShown++;
        }

        var declHost = new Border
        {
            Padding = sectionPad,
            SnapsToDevicePixels = true,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = decl,
        };
        Grid.SetColumn(declHost, 0);
        grid.Children.Add(declHost);

        var divider1 = new Border { Background = TaxInvoiceA4Visuals.BorderBrush, Width = 1, SnapsToDevicePixels = true };
        Grid.SetColumn(divider1, 1);
        grid.Children.Add(divider1);

        var bank = BuildBankDetailsPanel(input.Store, bodyPt, smallPt);
        var bankHost = new Border
        {
            Padding = sectionPad,
            SnapsToDevicePixels = true,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = bank,
        };
        Grid.SetColumn(bankHost, 2);
        grid.Children.Add(bankHost);

        var divider2 = new Border { Background = TaxInvoiceA4Visuals.BorderBrush, Width = 1, SnapsToDevicePixels = true };
        Grid.SetColumn(divider2, 3);
        grid.Children.Add(divider2);

        var sig = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        sig.Children.Add(new Border
        {
            BorderBrush = TaxInvoiceA4Visuals.BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 1,
            Margin = new Thickness(0, 8, 0, 2),
            SnapsToDevicePixels = true,
        });
        sig.Children.Add(TaxInvoiceA4Visuals.Text($"for {input.Store.StoreName}", bodyPt, align: TextAlignment.Right));
        sig.Children.Add(TaxInvoiceA4Visuals.Text("Authorised Signatory", smallPt, align: TextAlignment.Right));

        var sigHost = new Border
        {
            Padding = sectionPad,
            SnapsToDevicePixels = true,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = sig,
        };
        Grid.SetColumn(sigHost, 4);
        grid.Children.Add(sigHost);

        return SectionBorder(grid, new Thickness(1, 0, 1, 1), new Thickness(0));
    }

    private static StackPanel BuildBankDetailsPanel(StoreProfile store, double bodyPt, double smallPt)
    {
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        stack.Children.Add(TaxInvoiceA4Visuals.Text("Bank Details", bodyPt, FontWeights.Bold, verticalAlign: VerticalAlignment.Top));
        stack.Children.Add(TaxInvoiceA4Visuals.Text(
            $"Account Holder Name: {store.BankAccountHolderName ?? ""}",
            smallPt,
            verticalAlign: VerticalAlignment.Top));
        stack.Children.Add(TaxInvoiceA4Visuals.Text(
            $"Bank Account Number: {store.BankAccountNumber ?? ""}",
            smallPt,
            verticalAlign: VerticalAlignment.Top));
        stack.Children.Add(TaxInvoiceA4Visuals.Text(
            $"IFSC Code: {store.BankIfsc ?? ""}",
            smallPt,
            verticalAlign: VerticalAlignment.Top));
        stack.Children.Add(TaxInvoiceA4Visuals.Text(
            $"Branch Name: {store.BankBranchName ?? ""}",
            smallPt,
            verticalAlign: VerticalAlignment.Top));
        return stack;
    }

    private static decimal ComputeDisplayedSubtotal(ThermalInvoiceInput input, TaxInvoiceA4GstBreakdown.BillTaxTotals taxTotals)
    {
        var fromProductLines = TaxInvoiceA4GstBreakdown.SumLineProductAmounts(InvoiceLinePagination.ActiveLines(input));
        if (fromProductLines > 0)
            return fromProductLines;
        return ComputeTaxableSubtotal(input, taxTotals);
    }

    private static decimal ComputeDiscountedSubtotal(
        decimal productSubtotal,
        ThermalInvoiceInput input,
        TaxInvoiceA4GstBreakdown.BillTaxTotals taxTotals)
    {
        if (input.RevisedSubTotal > 0)
            return input.RevisedSubTotal;
        if (input.TotalTaxableAmount > 0)
            return input.TotalTaxableAmount;
        if (taxTotals.Taxable > 0)
            return taxTotals.Taxable;
        return MoneyMath.RoundAmount(productSubtotal - input.ManualDiscountAmount);
    }

    private static decimal ComputeTaxableSubtotal(ThermalInvoiceInput input, TaxInvoiceA4GstBreakdown.BillTaxTotals taxTotals)
    {
        if (taxTotals.Taxable > 0)
            return taxTotals.Taxable;
        var fromLines = InvoiceLinePagination.ActiveLines(input).Sum(TaxInvoiceA4GstBreakdown.LineTaxableAmount);
        if (fromLines > 0)
            return fromLines;
        return input.TotalTaxableAmount > 0
            ? input.TotalTaxableAmount
            : MoneyMath.RoundAmount(input.Payable + input.ManualDiscountAmount - taxTotals.TotalTax);
    }

    private static TextAlignment TableColumnAlign(int column) => column switch
    {
        0 => TextAlignment.Center,
        1 => TextAlignment.Left,
        2 => TextAlignment.Center,
        3 => TextAlignment.Center,
        4 => TextAlignment.Right,
        5 => TextAlignment.Right,
        6 => TextAlignment.Center,
        7 => TextAlignment.Right,
        _ => TextAlignment.Left,
    };

    private enum TableCellBorder
    {
        Header,
        Body,
        Spacer,
        Footer,
        FooterLast,
    }

    private static void AddSpacerRow(Grid table, int row, string continuedLabel, double rowPt, Thickness cellPadding)
    {
        for (var c = 0; c < LineColCount; c++)
        {
            var label = c == 1 ? continuedLabel : "";
            var verticalAlign = c == 1 && !string.IsNullOrEmpty(continuedLabel)
                ? VerticalAlignment.Bottom
                : VerticalAlignment.Top;
            AddTableCell(table, row, c, label, rowPt, FontWeights.Normal, TableColumnAlign(c), TableCellBorder.Spacer,
                cellPadding: cellPadding, verticalAlign: verticalAlign);
        }
    }

    private static Thickness TableCellBorderThickness(int col, int colSpan, TableCellBorder borderKind)
    {
        var lastCol = LineColCount - 1;
        var left = col > 0 ? 1 : 0;
        var right = col + colSpan - 1 >= lastCol ? 1 : 0;

        return borderKind switch
        {
            TableCellBorder.Header => new Thickness(left, 0, right, 1),
            TableCellBorder.Footer => new Thickness(left, 1, right, 0),
            TableCellBorder.FooterLast => new Thickness(left, 1, right, 1),
            _ => new Thickness(left, 0, right, 0),
        };
    }

    private static void AddTableCell(
        Grid table,
        int row,
        int col,
        string text,
        double fontSize,
        FontWeight weight,
        TextAlignment align,
        TableCellBorder borderKind,
        int colSpan = 1,
        Thickness? cellPadding = null,
        VerticalAlignment verticalAlign = VerticalAlignment.Center)
    {
        var padding = cellPadding ?? new Thickness(2, 1, 2, 1);

        var cell = new Border
        {
            BorderBrush = TaxInvoiceA4Visuals.BorderBrush,
            BorderThickness = TableCellBorderThickness(col, colSpan, borderKind),
            Padding = padding,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SnapsToDevicePixels = true,
            Child = TaxInvoiceA4Visuals.Text(
                text,
                fontSize,
                weight,
                align,
                wrap: col == 1,
                verticalAlign: verticalAlign),
        };
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, col);
        if (colSpan > 1)
            Grid.SetColumnSpan(cell, colSpan);
        table.Children.Add(cell);
    }

    private static string FormatQty(decimal qty) => qty.ToString("0.##", In);
    private static string Money(decimal value) => value.ToString("N2", In);
    private static string FormatPercent(decimal value) => value.ToString("0.##", In) + "%";
}
