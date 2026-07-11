using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Plain bordered commercial A4 GST invoice FlowDocument.</summary>
public static class CommercialA4InvoiceDocumentBuilder
{
    private static readonly CultureInfo In = CultureInfo.GetCultureInfo("en-IN");
    private const string UnitPer = "NOS";

    public static FlowDocument Create(ThermalInvoiceInput input, int linesPerPage = CommercialA4InvoiceLayout.LinesPerPage)
    {
        var pageWidth = InvoiceImageScaling.MmToPx(CommercialA4InvoiceLayout.PageWidthMm);
        var pageHeight = InvoiceImageScaling.MmToPx(CommercialA4InvoiceLayout.PageHeightMm);
        var margin = InvoiceImageScaling.MmToPx(CommercialA4InvoiceLayout.PageMarginMm);
        var contentWidth = pageWidth - margin * 2;

        var doc = new FlowDocument
        {
            PageWidth = pageWidth,
            PageHeight = pageHeight,
            PagePadding = new Thickness(0),
            ColumnWidth = pageWidth,
            IsColumnWidthFlexible = false,
            IsOptimalParagraphEnabled = false,
            FontFamily = CommercialA4InvoiceVisuals.BodyFont,
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
        var bodyPt = CommercialA4InvoiceLayout.BodyPt;
        var smallPt = CommercialA4InvoiceLayout.SmallPt;

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
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
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
            var amountInWords = BuildAmountInWords(input, contentWidth, bodyPt);
            Grid.SetRow(amountInWords, 3);
            content.Children.Add(amountInWords);

            var declaration = BuildDeclarationFooter(input, contentWidth, bodyPt, smallPt);
            Grid.SetRow(declaration, 4);
            content.Children.Add(declaration);

            var footerNote = SectionBorder(
                CommercialA4InvoiceVisuals.Text(
                    "This is a Computer Generated Invoice",
                    smallPt,
                    align: TextAlignment.Center),
                new Thickness(1, 0, 1, 1));
            Grid.SetRow(footerNote, 5);
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

        var title = CommercialA4InvoiceVisuals.Text("INVOICE", CommercialA4InvoiceLayout.TitlePt, FontWeights.Bold, TextAlignment.Center);
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);

        var printed = CommercialA4InvoiceVisuals.Text(
            $"Printed on {DateTime.Now:dd-MMM-yy} at {DateTime.Now:HH:mm}",
            bodyPt,
            align: TextAlignment.Right);
        Grid.SetColumn(printed, 2);
        grid.Children.Add(printed);

        return SectionBorder(grid, new Thickness(1, 1, 1, 1));
    }

    private static Thickness SectionPadding()
    {
        var pad = InvoiceImageScaling.MmToPx(CommercialA4InvoiceLayout.SectionPaddingMm);
        return new Thickness(pad);
    }

    private static Border SectionBorder(UIElement child, Thickness border, Thickness? padding = null)
        => CommercialA4InvoiceVisuals.BorderedCell(child, border, padding ?? SectionPadding());

    private static UIElement BuildMetaSection(ThermalInvoiceInput input, double contentWidth, double bodyPt, double smallPt)
    {
        const int metaRows = 7;
        var rowMinH = InvoiceImageScaling.MmToPx(CommercialA4InvoiceLayout.MetaRowHeightMm);
        var sectionPad = SectionPadding();
        var metaPt = CommercialA4InvoiceLayout.MetaLabelPt;

        var outer = new Grid
        {
            Width = contentWidth,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
        };
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CommercialA4InvoiceLayout.MetaLeftColumnWeight, GridUnitType.Star) });
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CommercialA4InvoiceLayout.MetaRightColumnWeight, GridUnitType.Star) });
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
            Background = CommercialA4InvoiceVisuals.BorderBrush,
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
            var cell = CommercialA4InvoiceVisuals.MetaSplitRowCell(
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
        stack.Children.Add(CommercialA4InvoiceVisuals.Text(store.StoreName.ToUpperInvariant(), bodyPt, FontWeights.Bold, verticalAlign: VerticalAlignment.Top));
        if (!string.IsNullOrWhiteSpace(store.Address))
        {
            foreach (var line in store.Address.Split('\n', '\r').Select(l => l.Trim()).Where(l => l.Length > 0))
                stack.Children.Add(CommercialA4InvoiceVisuals.Text(line, smallPt, verticalAlign: VerticalAlignment.Top));
        }

        var stateLine = GstStateCodeResolver.FormatStateLine(store.StateName, store.Gstin);
        if (!string.IsNullOrWhiteSpace(stateLine))
            stack.Children.Add(CommercialA4InvoiceVisuals.Text(stateLine, smallPt, verticalAlign: VerticalAlignment.Top));

        if (!string.IsNullOrWhiteSpace(store.Gstin))
            stack.Children.Add(CommercialA4InvoiceVisuals.Text($"GSTIN/UIN: {store.Gstin}", smallPt, verticalAlign: VerticalAlignment.Top));

        return CommercialA4InvoiceVisuals.MetaBlockCell(stack, sectionPad, drawBottomBorder: true);
    }

    private static UIElement BuildPartyBlock(string label, ThermalInvoiceInput input, double bodyPt, double smallPt, Thickness sectionPad, bool drawBottomBorder)
    {
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        stack.Children.Add(CommercialA4InvoiceVisuals.Text(label, bodyPt, FontWeights.Bold, verticalAlign: VerticalAlignment.Top));
        stack.Children.Add(CommercialA4InvoiceVisuals.Text(FormatPartyNameLine(input), bodyPt, verticalAlign: VerticalAlignment.Top));

        var stateLine = GstStateCodeResolver.FormatStateLine(input.Store.StateName, input.Store.Gstin);
        stack.Children.Add(CommercialA4InvoiceVisuals.Text("GSTIN/UIN:", smallPt, verticalAlign: VerticalAlignment.Top));
        if (!string.IsNullOrWhiteSpace(stateLine))
            stack.Children.Add(CommercialA4InvoiceVisuals.Text(stateLine, smallPt, verticalAlign: VerticalAlignment.Top));

        return CommercialA4InvoiceVisuals.MetaBlockCell(stack, sectionPad, drawBottomBorder);
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
        var colWidths = CommercialA4InvoiceLayout.ComputeColumnWidths(contentWidth);
        var headerPt = CommercialA4InvoiceLayout.TableHeaderPt;
        var rowPt = CommercialA4InvoiceLayout.TableRowPt;
        var rowH = InvoiceImageScaling.MmToPx(CommercialA4InvoiceLayout.TableRowHeightMm);
        var cellPadH = InvoiceImageScaling.MmToPx(CommercialA4InvoiceLayout.TableCellPaddingHorizontalMm);
        var cellPadV = InvoiceImageScaling.MmToPx(CommercialA4InvoiceLayout.TableCellPaddingVerticalMm);
        var cellPadding = new Thickness(cellPadH, cellPadV, cellPadH, cellPadV);

        var showDiscount = isLastPage && input.ManualDiscountAmount > 0;
        var footerRows = isLastPage ? 1 + (showDiscount ? 1 : 0) + 1 : 0;
        var spacerRow = 1 + pageLines.Count;
        var footerStartRow = spacerRow + 1;

        var table = new Grid
        {
            Width = contentWidth,
            VerticalAlignment = VerticalAlignment.Stretch,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
        };
        for (var c = 0; c < 8; c++)
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(colWidths[c]) });

        table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowH) });
        for (var i = 0; i < pageLines.Count; i++)
            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowH) });
        table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < footerRows; i++)
            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowH) });

        var headers = new[] { "SI No.", "Description of Goods", "HSN/SAC", "Quantity", "Rate", "per", "Disc. %", "Amount" };
        for (var c = 0; c < 8; c++)
            AddTableCell(table, 0, c, headers[c], headerPt, FontWeights.Bold, TableColumnAlign(c), CommercialTableCellBorder.Header, cellPadding: cellPadding);

        var rowIndex = 1;
        foreach (var line in pageLines)
        {
            AddTableCell(table, rowIndex, 0, line.LineNo.ToString(In), rowPt, FontWeights.Normal, TableColumnAlign(0), CommercialTableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 1, line.Description, rowPt, FontWeights.Normal, TableColumnAlign(1), CommercialTableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 2, line.Hsn, rowPt, FontWeights.Normal, TableColumnAlign(2), CommercialTableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 3, FormatQty(line.Qty), rowPt, FontWeights.Normal, TableColumnAlign(3), CommercialTableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 4, Money(line.Rate), rowPt, FontWeights.Normal, TableColumnAlign(4), CommercialTableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 5, UnitPer, rowPt, FontWeights.Normal, TableColumnAlign(5), CommercialTableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 6, "", rowPt, FontWeights.Normal, TableColumnAlign(6), CommercialTableCellBorder.Body, cellPadding: cellPadding);
            AddTableCell(table, rowIndex, 7, Money(line.PrePrintedLineAmount()), rowPt, FontWeights.Normal, TableColumnAlign(7), CommercialTableCellBorder.Body, cellPadding: cellPadding);
            rowIndex++;
        }

        AddSpacerRow(table, spacerRow, hasMorePages ? "Continued..." : "", rowPt, cellPadding);

        if (isLastPage)
        {
            var subtotal = ComputeSubtotal(input);
            var footerRow = footerStartRow;

            AddTableCell(table, footerRow, 0, "", rowPt, FontWeights.Normal, TableColumnAlign(0), CommercialTableCellBorder.Footer, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 1, "", rowPt, FontWeights.Normal, TableColumnAlign(1), CommercialTableCellBorder.Footer, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 2, "", rowPt, FontWeights.Normal, TableColumnAlign(2), CommercialTableCellBorder.Footer, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 3, "", rowPt, FontWeights.Normal, TableColumnAlign(3), CommercialTableCellBorder.Footer, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 4, "", rowPt, FontWeights.Normal, TableColumnAlign(4), CommercialTableCellBorder.Footer, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 5, "", rowPt, FontWeights.Normal, TableColumnAlign(5), CommercialTableCellBorder.Footer, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 6, "Subtotal", rowPt, FontWeights.Bold, TextAlignment.Right, CommercialTableCellBorder.Footer, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 7, Money(subtotal), rowPt, FontWeights.Normal, TableColumnAlign(7), CommercialTableCellBorder.Footer, cellPadding: cellPadding);
            footerRow++;

            if (showDiscount)
            {
                AddTableCell(table, footerRow, 0, "", rowPt, FontWeights.Normal, TableColumnAlign(0), CommercialTableCellBorder.Footer, cellPadding: cellPadding);
                AddTableCell(table, footerRow, 1, "Less : DISCOUNT", rowPt, FontWeights.Normal, TextAlignment.Left, CommercialTableCellBorder.Footer, cellPadding: cellPadding);
                AddTableCell(table, footerRow, 2, "", rowPt, FontWeights.Normal, TableColumnAlign(2), CommercialTableCellBorder.Footer, cellPadding: cellPadding);
                AddTableCell(table, footerRow, 3, "", rowPt, FontWeights.Normal, TableColumnAlign(3), CommercialTableCellBorder.Footer, cellPadding: cellPadding);
                AddTableCell(table, footerRow, 4, "", rowPt, FontWeights.Normal, TableColumnAlign(4), CommercialTableCellBorder.Footer, cellPadding: cellPadding);
                AddTableCell(table, footerRow, 5, "", rowPt, FontWeights.Normal, TableColumnAlign(5), CommercialTableCellBorder.Footer, cellPadding: cellPadding);
                AddTableCell(table, footerRow, 6, "", rowPt, FontWeights.Normal, TableColumnAlign(6), CommercialTableCellBorder.Footer, cellPadding: cellPadding);
                AddTableCell(table, footerRow, 7, $"(-) {Money(input.ManualDiscountAmount)}", rowPt, FontWeights.Normal, TableColumnAlign(7), CommercialTableCellBorder.Footer, cellPadding: cellPadding);
                footerRow++;
            }

            AddTableCell(table, footerRow, 0, "", rowPt, FontWeights.Normal, TableColumnAlign(0), CommercialTableCellBorder.FooterLast, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 1, "Total", rowPt, FontWeights.Bold, TextAlignment.Left, CommercialTableCellBorder.FooterLast, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 2, "", rowPt, FontWeights.Normal, TableColumnAlign(2), CommercialTableCellBorder.FooterLast, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 3, $"{FormatQty(input.TotalQty)} {UnitPer}", rowPt, FontWeights.Bold, TableColumnAlign(3), CommercialTableCellBorder.FooterLast, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 4, "", rowPt, FontWeights.Normal, TableColumnAlign(4), CommercialTableCellBorder.FooterLast, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 5, "", rowPt, FontWeights.Normal, TableColumnAlign(5), CommercialTableCellBorder.FooterLast, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 6, "", rowPt, FontWeights.Normal, TableColumnAlign(6), CommercialTableCellBorder.FooterLast, cellPadding: cellPadding);
            AddTableCell(table, footerRow, 7, Money(input.Payable), rowPt, FontWeights.Bold, TableColumnAlign(7), CommercialTableCellBorder.FooterLast, cellPadding: cellPadding);
        }

        return SectionBorder(table, new Thickness(1, 0, 1, 0), new Thickness(0));
    }

    private static UIElement BuildAmountInWords(ThermalInvoiceInput input, double contentWidth, double bodyPt)
    {
        var stack = new StackPanel();
        stack.Children.Add(CommercialA4InvoiceVisuals.Text("Amount Chargeable (in words)", bodyPt, FontWeights.Bold));
        stack.Children.Add(CommercialA4InvoiceVisuals.Text(IndianAmountInWords.ForRupee(input.Payable), bodyPt));
        return SectionBorder(stack, new Thickness(1, 0, 1, 1));
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
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.62, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.38, GridUnitType.Star) });

        var decl = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        decl.Children.Add(CommercialA4InvoiceVisuals.Text("Declaration", bodyPt, FontWeights.Bold, verticalAlign: VerticalAlignment.Top));
        decl.Children.Add(CommercialA4InvoiceVisuals.Text(
            "We declare that this invoice shows the actual price of the goods described and that all particulars are true and correct.",
            smallPt,
            verticalAlign: VerticalAlignment.Top));

        var idx = 1;
        if (!string.IsNullOrWhiteSpace(input.Store.TermsAndConditions))
        {
            decl.Children.Add(CommercialA4InvoiceVisuals.Text($"{idx}. {input.Store.TermsAndConditions}", smallPt, verticalAlign: VerticalAlignment.Top));
            idx++;
        }

        foreach (var line in input.Store.PolicyLines ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            decl.Children.Add(CommercialA4InvoiceVisuals.Text($"{idx}. {line}", smallPt, verticalAlign: VerticalAlignment.Top));
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

        var divider = new Border
        {
            Background = CommercialA4InvoiceVisuals.BorderBrush,
            Width = 1,
            SnapsToDevicePixels = true,
        };
        Grid.SetColumn(divider, 1);
        grid.Children.Add(divider);

        var sig = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        sig.Children.Add(new Border
        {
            BorderBrush = CommercialA4InvoiceVisuals.BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 1,
            Margin = new Thickness(0, 20, 0, 4),
            SnapsToDevicePixels = true,
        });
        sig.Children.Add(CommercialA4InvoiceVisuals.Text($"for {input.Store.StoreName}", bodyPt, align: TextAlignment.Right));
        sig.Children.Add(CommercialA4InvoiceVisuals.Text("Authorised Signatory", smallPt, align: TextAlignment.Right));

        var sigHost = new Border
        {
            Padding = sectionPad,
            SnapsToDevicePixels = true,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = sig,
        };
        Grid.SetColumn(sigHost, 2);
        grid.Children.Add(sigHost);

        return SectionBorder(grid, new Thickness(1, 0, 1, 1), new Thickness(0));
    }

    private static decimal ComputeSubtotal(ThermalInvoiceInput input)
    {
        var fromLines = InvoiceLinePagination.ActiveLines(input).Sum(l => l.PrePrintedLineAmount());
        if (fromLines > 0)
            return fromLines;
        return input.SubTotal > 0 ? input.SubTotal : input.Payable + input.ManualDiscountAmount;
    }

    private static TextAlignment TableColumnAlign(int column) => column switch
    {
        0 => TextAlignment.Center,  // SI No.
        1 => TextAlignment.Left,    // Description
        2 => TextAlignment.Center,  // HSN/SAC
        3 => TextAlignment.Center,  // Quantity
        4 => TextAlignment.Right,   // Rate
        5 => TextAlignment.Center,  // per
        6 => TextAlignment.Center,  // Disc. %
        7 => TextAlignment.Right,   // Amount
        _ => TextAlignment.Left,
    };

    private enum CommercialTableCellBorder
    {
        Header,
        Body,
        Spacer,
        Footer,
        FooterLast,
    }

    private static void AddSpacerRow(Grid table, int row, string continuedLabel, double rowPt, Thickness cellPadding)
    {
        for (var c = 0; c < 8; c++)
        {
            var label = c == 1 ? continuedLabel : "";
            var verticalAlign = c == 1 && !string.IsNullOrEmpty(continuedLabel)
                ? VerticalAlignment.Bottom
                : VerticalAlignment.Top;
            AddTableCell(table, row, c, label, rowPt, FontWeights.Normal, TableColumnAlign(c), CommercialTableCellBorder.Spacer,
                cellPadding: cellPadding, verticalAlign: verticalAlign);
        }
    }

    private static Thickness TableCellBorderThickness(int col, int colSpan, CommercialTableCellBorder borderKind)
    {
        const int lastCol = 7;
        var left = col > 0 ? 1 : 0;
        var right = col + colSpan - 1 >= lastCol ? 1 : 0;

        return borderKind switch
        {
            CommercialTableCellBorder.Header => new Thickness(left, 0, right, 1),
            CommercialTableCellBorder.Footer => new Thickness(left, 1, right, 0),
            CommercialTableCellBorder.FooterLast => new Thickness(left, 1, right, 1),
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
        CommercialTableCellBorder borderKind,
        int colSpan = 1,
        Thickness? cellPadding = null,
        VerticalAlignment verticalAlign = VerticalAlignment.Center)
    {
        var padding = cellPadding ?? new Thickness(2, 1, 2, 1);

        var cell = new Border
        {
            BorderBrush = CommercialA4InvoiceVisuals.BorderBrush,
            BorderThickness = TableCellBorderThickness(col, colSpan, borderKind),
            Padding = padding,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SnapsToDevicePixels = true,
            Child = CommercialA4InvoiceVisuals.Text(text, fontSize, weight, align, verticalAlign: verticalAlign),
        };
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, col);
        if (colSpan > 1)
            Grid.SetColumnSpan(cell, colSpan);
        table.Children.Add(cell);
    }

    private static string FormatQty(decimal qty) => qty.ToString("0.##", In);
    private static string Money(decimal value) => value.ToString("N2", In);
}
