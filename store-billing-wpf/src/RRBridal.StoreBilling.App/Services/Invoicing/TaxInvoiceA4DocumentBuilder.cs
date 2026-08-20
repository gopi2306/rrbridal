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
    private const int LineColCount = 7;

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
        var chunks = InvoiceLinePagination.ChunkLines(activeLines, linesPerPage);

        for (var pageIndex = 0; pageIndex < chunks.Count; pageIndex++)
        {
            var isLastPage = pageIndex == chunks.Count - 1;
            var hasMorePages = !isLastPage;
            var pageVisual = BuildPageVisual(input, contentWidth, pageWidth, pageHeight, margin, chunks[pageIndex], isLastPage, hasMorePages);

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
        bool hasMorePages)
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
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        if (isLastPage)
        {
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // amount words
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // gst split
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // tax words
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // declaration/bank/sig
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer note
        }
        root.Children.Add(content);

        var title = BuildTitleRow(contentWidth, bodyPt);
        Grid.SetRow(title, 0);
        content.Children.Add(title);

        var meta = BuildMetaSection(input, contentWidth, bodyPt, smallPt);
        Grid.SetRow(meta, 1);
        content.Children.Add(meta);

        var table = BuildLineTable(input, contentWidth, pageLines, isLastPage, hasMorePages);
        Grid.SetRow(table, 2);
        content.Children.Add(table);

        if (isLastPage)
        {
            var taxTotals = TaxInvoiceA4GstBreakdown.ComputeTotals(input);

            var amountInWords = BuildAmountInWords(input, contentWidth, bodyPt);
            Grid.SetRow(amountInWords, 3);
            content.Children.Add(amountInWords);

            var gstSplit = BuildGstSplitTable(input, contentWidth, bodyPt, smallPt);
            Grid.SetRow(gstSplit, 4);
            content.Children.Add(gstSplit);

            var taxWords = BuildTaxAmountInWords(taxTotals.TotalTax, contentWidth, bodyPt);
            Grid.SetRow(taxWords, 5);
            content.Children.Add(taxWords);

            var declaration = BuildDeclarationFooter(input, contentWidth, bodyPt, smallPt);
            Grid.SetRow(declaration, 6);
            content.Children.Add(declaration);

            var footerNote = SectionBorder(
                TaxInvoiceA4Visuals.Text(
                    "This is a Computer Generated Invoice",
                    smallPt,
                    align: TextAlignment.Center),
                new Thickness(1, 0, 1, 1));
            Grid.SetRow(footerNote, 7);
            content.Children.Add(footerNote);
        }

        return root;
    }

    private static UIElement BuildTitleRow(double contentWidth, double bodyPt)
    {
        var grid = new Grid { Width = contentWidth };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var title = TaxInvoiceA4Visuals.Text("Tax Invoice", TaxInvoiceA4Layout.TitlePt, FontWeights.Bold, TextAlignment.Center);
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

        var stateLine = GstStateCodeResolver.FormatStateLine(input.Store.StateName, input.Store.Gstin);
        stack.Children.Add(TaxInvoiceA4Visuals.Text("GSTIN/UIN:", smallPt, verticalAlign: VerticalAlignment.Top));
        if (!string.IsNullOrWhiteSpace(stateLine))
            stack.Children.Add(TaxInvoiceA4Visuals.Text(stateLine, smallPt, verticalAlign: VerticalAlignment.Top));

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
        bool isLastPage,
        bool hasMorePages)
    {
        var colWidths = TaxInvoiceA4Layout.ComputeColumnWidths(contentWidth);
        var headerPt = TaxInvoiceA4Layout.TableHeaderPt;
        var rowPt = TaxInvoiceA4Layout.TableRowPt;
        var rowH = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.TableRowHeightMm);
        var cellPadH = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.TableCellPaddingHorizontalMm);
        var cellPadV = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.TableCellPaddingVerticalMm);
        var cellPadding = new Thickness(cellPadH, cellPadV, cellPadH, cellPadV);

        var showDiscount = isLastPage && input.ManualDiscountAmount > 0;
        var taxTotals = isLastPage ? TaxInvoiceA4GstBreakdown.ComputeTotals(input) : null;
        var taxFooterRows = isLastPage
            ? (input.IsInterState ? 1 : 2)
            : 0;
        var footerRows = isLastPage ? 1 + (showDiscount ? 1 : 0) + taxFooterRows + 1 : 0;
        var spacerRow = 1 + pageLines.Count;
        var footerStartRow = spacerRow + 1;

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
        for (var i = 0; i < footerRows; i++)
            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowH) });

        var headers = new[] { "SI No.", "Description of Goods", "HSN/SAC", "Quantity", "Rate", "per", "Amount" };
        for (var c = 0; c < LineColCount; c++)
            AddTableCell(table, 0, c, headers[c], headerPt, FontWeights.Bold, TableColumnAlign(c), TableCellBorder.Header, cellPadding: cellPadding);

        var rowIndex = 1;
        foreach (var line in pageLines)
        {
            AddTableCell(table, rowIndex, 0, line.LineNo.ToString(In), rowPt, FontWeights.Normal, TableColumnAlign(0), TableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 1, line.Description, rowPt, FontWeights.Normal, TableColumnAlign(1), TableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 2, line.Hsn, rowPt, FontWeights.Normal, TableColumnAlign(2), TableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 3, FormatQty(line.Qty), rowPt, FontWeights.Normal, TableColumnAlign(3), TableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 4, Money(TaxInvoiceA4GstBreakdown.LineExclusiveRate(line)), rowPt, FontWeights.Normal, TableColumnAlign(4), TableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 5, UnitPer, rowPt, FontWeights.Normal, TableColumnAlign(5), TableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 6, Money(TaxInvoiceA4GstBreakdown.LineTaxableAmount(line)), rowPt, FontWeights.Normal, TableColumnAlign(6), TableCellBorder.Body, cellPadding: cellPadding);
            rowIndex++;
        }

        AddSpacerRow(table, spacerRow, hasMorePages ? "Continued..." : "", rowPt, cellPadding);

        if (isLastPage && taxTotals != null)
        {
            var subtotal = ComputeTaxableSubtotal(input, taxTotals);
            var footerRow = footerStartRow;

            AddLabelAmountFooterRow(table, footerRow, "", "Subtotal", Money(subtotal), rowPt, TableCellBorder.Footer, cellPadding, labelBold: true);
            footerRow++;

            if (showDiscount)
            {
                AddLabelAmountFooterRow(
                    table,
                    footerRow,
                    "Less : DISCOUNT",
                    "",
                    $"(-) {Money(input.ManualDiscountAmount)}",
                    rowPt,
                    TableCellBorder.Footer,
                    cellPadding,
                    labelInDescription: true);
                footerRow++;
            }

            if (input.IsInterState)
            {
                AddLabelAmountFooterRow(table, footerRow, "IGST", "", Money(taxTotals.Igst), rowPt, TableCellBorder.Footer, cellPadding, labelInDescription: true);
                footerRow++;
            }
            else
            {
                AddLabelAmountFooterRow(table, footerRow, "CGST", "", Money(taxTotals.Cgst), rowPt, TableCellBorder.Footer, cellPadding, labelInDescription: true);
                footerRow++;
                AddLabelAmountFooterRow(table, footerRow, "SGST", "", Money(taxTotals.Sgst), rowPt, TableCellBorder.Footer, cellPadding, labelInDescription: true);
                footerRow++;
            }

            AddTableCell(table, footerRow, 0, "", rowPt, FontWeights.Normal, TableColumnAlign(0), TableCellBorder.FooterLast, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 1, "Total", rowPt, FontWeights.Bold, TextAlignment.Left, TableCellBorder.FooterLast, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 2, "", rowPt, FontWeights.Normal, TableColumnAlign(2), TableCellBorder.FooterLast, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 3, $"{FormatQty(input.TotalQty)} {UnitPer}", rowPt, FontWeights.Bold, TableColumnAlign(3), TableCellBorder.FooterLast, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 4, "", rowPt, FontWeights.Normal, TableColumnAlign(4), TableCellBorder.FooterLast, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 5, "", rowPt, FontWeights.Normal, TableColumnAlign(5), TableCellBorder.FooterLast, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 6, Money(input.Payable), rowPt, FontWeights.Bold, TableColumnAlign(6), TableCellBorder.FooterLast, cellPadding: cellPadding);
        }

        return SectionBorder(table, new Thickness(1, 0, 1, 0), new Thickness(0));
    }

    private static void AddLabelAmountFooterRow(
        Grid table,
        int row,
        string descriptionLabel,
        string amountLabel,
        string amountValue,
        double rowPt,
        TableCellBorder border,
        Thickness cellPadding,
        bool labelBold = false,
        bool labelInDescription = false)
    {
        AddTableCell(table, row, 0, "", rowPt, FontWeights.Normal, TableColumnAlign(0), border, cellPadding: cellPadding);
        AddTableCell(
            table,
            row,
            1,
            labelInDescription ? descriptionLabel : "",
            rowPt,
            labelInDescription && !string.IsNullOrEmpty(descriptionLabel) ? FontWeights.Normal : FontWeights.Normal,
            TextAlignment.Left,
            border,
            cellPadding: cellPadding);
        AddTableCell(table, row, 2, "", rowPt, FontWeights.Normal, TableColumnAlign(2), border, cellPadding: cellPadding);
        AddTableCell(table, row, 3, "", rowPt, FontWeights.Normal, TableColumnAlign(3), border, cellPadding: cellPadding);
        AddTableCell(table, row, 4, "", rowPt, FontWeights.Normal, TableColumnAlign(4), border, cellPadding: cellPadding);
        AddTableCell(
            table,
            row,
            5,
            amountLabel,
            rowPt,
            labelBold ? FontWeights.Bold : FontWeights.Normal,
            TextAlignment.Right,
            border,
            cellPadding: cellPadding);
        AddTableCell(table, row, 6, amountValue, rowPt, FontWeights.Normal, TableColumnAlign(6), border, cellPadding: cellPadding);
    }

    private static UIElement BuildAmountInWords(ThermalInvoiceInput input, double contentWidth, double bodyPt)
    {
        var stack = new StackPanel();
        stack.Children.Add(TaxInvoiceA4Visuals.Text("Amount Chargeable (in words)", bodyPt, FontWeights.Bold));
        stack.Children.Add(TaxInvoiceA4Visuals.Text(IndianAmountInWords.ForRupee(input.Payable), bodyPt));
        return SectionBorder(stack, new Thickness(1, 0, 1, 1));
    }

    private static UIElement BuildTaxAmountInWords(decimal totalTax, double contentWidth, double bodyPt)
    {
        var stack = new StackPanel();
        stack.Children.Add(TaxInvoiceA4Visuals.Text("Tax Amount (in words)", bodyPt, FontWeights.Bold));
        stack.Children.Add(TaxInvoiceA4Visuals.Text(IndianAmountInWords.ForRupee(totalTax), bodyPt));
        return SectionBorder(stack, new Thickness(1, 0, 1, 1));
    }

    private static UIElement BuildGstSplitTable(
        ThermalInvoiceInput input,
        double contentWidth,
        double bodyPt,
        double smallPt)
    {
        var rows = TaxInvoiceA4GstBreakdown.BuildHsnRows(input);
        var inter = input.IsInterState;
        var headerPt = TaxInvoiceA4Layout.TableHeaderPt;
        var rowPt = TaxInvoiceA4Layout.TableRowPt;
        var rowH = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.TableRowHeightMm);
        var cellPadH = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.TableCellPaddingHorizontalMm);
        var cellPadV = InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.TableCellPaddingVerticalMm);
        var cellPadding = new Thickness(cellPadH, cellPadV, cellPadH, cellPadV);

        var colCount = inter ? 5 : 7;
        double[] weights = inter
            ? new[] { 1.2, 1.4, 0.9, 1.2, 1.2 }
            : new[] { 1.1, 1.3, 0.7, 1.0, 0.7, 1.0, 1.1 };
        var widths = new double[colCount];
        var sumW = weights.Sum();
        for (var i = 0; i < colCount; i++)
            widths[i] = contentWidth * weights[i] / sumW;

        var grid = new Grid
        {
            Width = contentWidth,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
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
        return SectionBorder(stack, new Thickness(1, 0, 1, 1), new Thickness(
            InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.SectionPaddingMm),
            InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.SectionPaddingMm),
            InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.SectionPaddingMm),
            InvoiceImageScaling.MmToPx(TaxInvoiceA4Layout.SectionPaddingMm)));
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

        var cell = new Border
        {
            BorderBrush = TaxInvoiceA4Visuals.BorderBrush,
            BorderThickness = thickness,
            Padding = cellPadding,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SnapsToDevicePixels = true,
            Child = TaxInvoiceA4Visuals.Text(text, fontSize, weight, align),
        };
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, col);
        if (colSpan > 1)
            Grid.SetColumnSpan(cell, colSpan);
        if (rowSpan > 1)
            Grid.SetRowSpan(cell, rowSpan);
        table.Children.Add(cell);
    }

    private static UIElement BuildDeclarationFooter(ThermalInvoiceInput input, double contentWidth, double bodyPt, double smallPt)
    {
        var sectionPad = SectionPadding();
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

        foreach (var line in input.Store.PolicyLines ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            decl.Children.Add(TaxInvoiceA4Visuals.Text($"{idx}. {line}", smallPt, verticalAlign: VerticalAlignment.Top));
            idx++;
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
            Margin = new Thickness(0, 20, 0, 4),
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
        5 => TextAlignment.Center,
        6 => TextAlignment.Right,
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
            Child = TaxInvoiceA4Visuals.Text(text, fontSize, weight, align, verticalAlign: verticalAlign),
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
