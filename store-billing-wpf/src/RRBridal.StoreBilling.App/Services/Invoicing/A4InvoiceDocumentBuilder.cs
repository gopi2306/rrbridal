using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Branded retail invoice FlowDocument for A4/A5.</summary>
public static class A4InvoiceDocumentBuilder
{
    private static readonly CultureInfo In = CultureInfo.GetCultureInfo("en-IN");

    private const double A5PageWidthMm = 148;
    private const double A5PageHeightMm = 210;

    public static FlowDocument Create(
        ThermalInvoiceInput input,
        ThermalReceiptAssets? assets,
        double pageWidthMm = RetailInvoiceLayout.ReferencePageWidthMm,
        double pageHeightMm = RetailInvoiceLayout.ReferencePageHeightMm,
        int linesPerPage = 10,
        A5PrePrintedLayoutSettings? a5Layout = null)
    {
        var scale = RetailInvoiceLayout.Scale(pageWidthMm);
        var pageWidth = InvoiceImageScaling.MmToPx(pageWidthMm);
        var pageHeight = InvoiceImageScaling.MmToPx(pageHeightMm);

        var doc = new FlowDocument
        {
            PageWidth = pageWidth,
            PageHeight = pageHeight,
            PagePadding = new Thickness(0),
            ColumnWidth = pageWidth,
            IsColumnWidthFlexible = false,
            IsOptimalParagraphEnabled = false,
            FontFamily = RetailInvoiceVisuals.BodyFont,
            Background = RetailInvoiceVisuals.PageGreenBrush,
        };

        var isA5 = IsA5Page(pageWidthMm, pageHeightMm);
        if (isA5)
        {
            var activeLines = InvoiceLinePagination.ActiveLines(input);
            var chunks = InvoiceLinePagination.ChunkLines(activeLines, linesPerPage);
            for (var pageIndex = 0; pageIndex < chunks.Count; pageIndex++)
            {
                var isLastPage = pageIndex == chunks.Count - 1;
                var hasMorePages = !isLastPage;
                var pageVisual = BuildPageVisual(
                    input, assets, pageWidth, pageHeight, scale,
                    chunks[pageIndex], isLastPage, hasMorePages, a5Layout);

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

        var singlePageVisual = BuildPageVisual(
            input, assets, pageWidth, pageHeight, scale,
            InvoiceLinePagination.ActiveLines(input), isLastPage: true, hasMorePages: false);
        doc.Blocks.Add(new BlockUIContainer(singlePageVisual));
        return doc;
    }

    private static bool IsA5Page(double pageWidthMm, double pageHeightMm) =>
        Math.Abs(pageWidthMm - A5PageWidthMm) < 0.1 && Math.Abs(pageHeightMm - A5PageHeightMm) < 0.1;

    private static FrameworkElement BuildPageVisual(
        ThermalInvoiceInput input,
        ThermalReceiptAssets? assets,
        double pageWidth,
        double pageHeight,
        double scale,
        IReadOnlyList<InvoiceLineSnap> pageLines,
        bool isLastPage,
        bool hasMorePages,
        A5PrePrintedLayoutSettings? a5Layout = null)
    {
        var inset = InvoiceImageScaling.MmToPx(RetailInvoiceLayout.PageInsetMm * scale);
        var panelWidth = pageWidth - inset * 2;
        var archRadius = panelWidth / 2;
        var panelHeight = pageHeight - inset * 2;
        var padding = InvoiceImageScaling.MmToPx(RetailInvoiceLayout.PanelPaddingMm * scale);
        var contentWidth = panelWidth - padding * 2;

        var root = new Grid
        {
            Width = pageWidth,
            Height = pageHeight,
            Background = RetailInvoiceVisuals.PageGreenBrush,
        };

        root.Children.Add(new Border { Background = RetailInvoiceVisuals.CreatePatternBrush() });

        var panelHost = new Grid
        {
            Margin = new Thickness(inset),
            Width = panelWidth,
            Height = panelHeight,
        };
        root.Children.Add(panelHost);

        panelHost.Children.Add(new Path
        {
            Data = RetailInvoiceVisuals.BuildArchPanelGeometry(panelWidth, panelHeight),
            Fill = RetailInvoiceVisuals.PanelCreamBrush,
            Stroke = RetailInvoiceVisuals.BorderBrush,
            StrokeThickness = 1,
        });

        var content = new Grid
        {
            Margin = new Thickness(padding, archRadius * 0.15 + padding * 0.5, padding, padding),
        };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panelHost.Children.Add(content);

        var headerBlock = new StackPanel();
        headerBlock.Children.Add(BuildHeader(input, assets, contentWidth, scale));
        headerBlock.Children.Add(BuildMetaSection(input, contentWidth, scale));
        Grid.SetRow(headerBlock, 0);
        content.Children.Add(headerBlock);

        var table = BuildLineItemsTable(input, assets, contentWidth, scale, pageLines, isLastPage, hasMorePages, a5Layout);
        Grid.SetRow(table, 2);
        content.Children.Add(table);

        if (isLastPage)
        {
            var colWidths = ComputeColumnWidths(contentWidth);
            var footer = BuildFooter(input, contentWidth, colWidths, scale);
            Grid.SetRow(footer, 3);
            content.Children.Add(footer);
        }

        return root;
    }

    private static double[] ComputeColumnWidths(double contentWidth)
    {
        var weights = RetailInvoiceLayout.LineColumnWeights;
        var sum = weights.Sum();
        return weights.Select(w => contentWidth * w / sum).ToArray();
    }

    private static UIElement BuildHeader(
        ThermalInvoiceInput input,
        ThermalReceiptAssets? assets,
        double contentWidth,
        double scale)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, Px(6, scale)) };

        stack.Children.Add(RetailInvoiceVisuals.Text(
            "INVOICE",
            Pt(RetailInvoiceLayout.HeaderTitlePt, scale),
            FontWeights.Bold,
            TextAlignment.Center));

        if (assets?.Logo != null)
        {
            var logoW = InvoiceImageScaling.MmToPx(RetailInvoiceLayout.LogoMaxWidthMm * scale);
            var logoH = InvoiceImageScaling.MmToPx(RetailInvoiceLayout.LogoMaxHeightMm * scale);
            var logo = InvoiceImageScaling.CreateWpfImage(assets.Logo, logoW, logoH);
            logo.HorizontalAlignment = HorizontalAlignment.Center;
            logo.Margin = new Thickness(0, Px(4, scale), 0, Px(2, scale));
            stack.Children.Add(logo);
        }

        var store = input.Store;
        stack.Children.Add(RetailInvoiceVisuals.Text(
            store.StoreName.ToUpperInvariant(),
            Pt(RetailInvoiceLayout.StoreNamePt, scale),
            FontWeights.Bold,
            TextAlignment.Center));

        if (!string.IsNullOrWhiteSpace(store.Address))
        {
            foreach (var line in store.Address.Split('\n', '\r').Select(l => l.Trim()).Where(l => l.Length > 0))
            {
                stack.Children.Add(RetailInvoiceVisuals.Text(
                    line.ToUpperInvariant(),
                    Pt(RetailInvoiceLayout.SubHeaderPt, scale),
                    align: TextAlignment.Center));
            }
        }

        if (!string.IsNullOrWhiteSpace(store.CustomerCarePhone))
        {
            stack.Children.Add(RetailInvoiceVisuals.Text(
                $"CONTACT: {store.CustomerCarePhone}",
                Pt(RetailInvoiceLayout.SubHeaderPt, scale),
                align: TextAlignment.Center));
        }

        if (input.IsDuplicateCopy)
        {
            stack.Children.Add(RetailInvoiceVisuals.Text(
                "*** DUPLICATE ***",
                Pt(RetailInvoiceLayout.SubHeaderPt, scale),
                FontWeights.Bold,
                TextAlignment.Center));
        }

        return stack;
    }

    private static UIElement BuildMetaSection(ThermalInvoiceInput input, double contentWidth, double scale)
    {
        var font = Pt(RetailInvoiceLayout.BodyPt, scale);
        var labelW = Px(72, scale);
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, Px(8, scale)) };

        var billTo = string.IsNullOrWhiteSpace(input.CustomerName) ? "" : input.CustomerName;
        stack.Children.Add(RetailInvoiceVisuals.CreateUnderlineField("BILL TO:", billTo, labelW, font, contentWidth - labelW));

        var invDateRow = new Grid { Margin = new Thickness(0, Px(4, scale), 0, 0) };
        invDateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        invDateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Px(16, scale)) });
        invDateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var invField = RetailInvoiceVisuals.CreateUnderlineField("INV. NO.:", input.BillNo, Px(56, scale), font, 0);
        Grid.SetColumn(invField, 0);
        invDateRow.Children.Add(invField);

        var dateField = RetailInvoiceVisuals.CreateUnderlineField("DATE:", input.BillDate, Px(40, scale), font, 0);
        Grid.SetColumn(dateField, 2);
        invDateRow.Children.Add(dateField);
        stack.Children.Add(invDateRow);

        var contact = string.IsNullOrWhiteSpace(input.CustomerPhone) ? "" : input.CustomerPhone;
        stack.Children.Add(RetailInvoiceVisuals.CreateUnderlineField("CONTACT:", contact, labelW, font, contentWidth - labelW));

        var flagsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, Px(6, scale), 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var cbSize = Px(12, scale);
        flagsRow.Children.Add(RetailInvoiceVisuals.Text("STITCHING", font, FontWeights.Bold));
        flagsRow.Children.Add(RetailInvoiceVisuals.CreateCheckbox(input.Stitching, cbSize));
        flagsRow.Children.Add(RetailInvoiceVisuals.CreateUnderlineField("D/D:", input.DeliveryDate, Px(28, scale), font, Px(80, scale)));
        stack.Children.Add(flagsRow);

        return stack;
    }

    private static UIElement BuildLineItemsTable(
        ThermalInvoiceInput input,
        ThermalReceiptAssets? assets,
        double contentWidth,
        double scale,
        IReadOnlyList<InvoiceLineSnap> pageLines,
        bool isLastPage,
        bool hasMorePages,
        A5PrePrintedLayoutSettings? a5Layout = null)
    {
        var colWidths = ComputeColumnWidths(contentWidth);
        var headerH = InvoiceImageScaling.MmToPx(RetailInvoiceLayout.TableRowHeightMm * scale);
        var totalH = headerH;
        var font = Pt(RetailInvoiceLayout.TableHeaderPt, scale);
        var line = Math.Max(1, Px(1, scale));

        var outer = new Grid
        {
            Margin = new Thickness(0, 0, 0, Px(8, scale)),
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var tableHost = new Grid { Background = RetailInvoiceVisuals.PanelCreamBrush };
        var showDiscPct = isLastPage && input.ItemDiscountPercent > 0;
        var showDiscAmt = isLastPage && input.ManualDiscountAmount > 0;
        var showAlteration = isLastPage && input.AlterationTotal > 0;
        var footerRowCount = isLastPage ? 1 + (showDiscPct ? 1 : 0) + (showDiscAmt ? 1 : 0) + (showAlteration ? 1 : 0) : 0;

        tableHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(headerH) });
        tableHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < footerRowCount; i++)
            tableHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(totalH) });
        for (var i = 0; i < 4; i++)
            tableHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(colWidths[i]) });

        var headers = new[] { "DESCRIPTION", "QTY", "RATE", "AMOUNT" };
        for (var c = 0; c < 4; c++)
        {
            AddTableCell(tableHost, 0, c, headers[c], font, FontWeights.Bold,
                c == 0 ? TextAlignment.Left : TextAlignment.Center, TableCellBorder.Header);
        }

        var lineSpacing = Px(3, scale);
        var descriptions = pageLines.Select(l => l.Description).ToList();
        var qtys = pageLines.Select(l => $"{l.Qty:0.###}").ToList();
        var rates = pageLines.Select(l => Money(l.Rate)).ToList();
        var amounts = pageLines.Select(l => Money(l.PrePrintedLineAmount())).ToList();

        if (hasMorePages)
        {
            descriptions.Add("");
            qtys.Add(A5PrePrintedText.FormatTotalQty(input.TotalQty));
            rates.Add("");
            amounts.Add("");

            descriptions.Add(A5PrePrintedText.ResolveContinuedLabel(a5Layout));
            qtys.Add("");
            rates.Add("");
            amounts.Add("");
        }

        AddBodyColumn(tableHost, 1, 0, descriptions, font, TextAlignment.Left, lineSpacing);
        AddBodyColumn(tableHost, 1, 1, qtys, font, TextAlignment.Center, lineSpacing);
        AddBodyColumn(tableHost, 1, 2, rates, font, TextAlignment.Center, lineSpacing);
        AddBodyColumn(tableHost, 1, 3, amounts, font, TextAlignment.Center, lineSpacing);

        if (isLastPage)
        {
            var footerRow = 2;
            if (showDiscPct)
            {
                AddTableCell(tableHost, footerRow, 0, "", font, FontWeights.Normal, TextAlignment.Left, TableCellBorder.Total);
                AddTableCell(tableHost, footerRow, 1, "", font, FontWeights.Normal, TextAlignment.Center, TableCellBorder.Total);
                AddTableCell(tableHost, footerRow, 2, "DISC %", font, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Total);
                AddTableCell(tableHost, footerRow, 3, input.ItemDiscountPercent.ToString("0.##", In), font, FontWeights.Bold,
                    TextAlignment.Center, TableCellBorder.Total);
                footerRow++;
            }

            if (showDiscAmt)
            {
                AddTableCell(tableHost, footerRow, 0, "", font, FontWeights.Normal, TextAlignment.Left, TableCellBorder.Total);
                AddTableCell(tableHost, footerRow, 1, "", font, FontWeights.Normal, TextAlignment.Center, TableCellBorder.Total);
                AddTableCell(tableHost, footerRow, 2, "DISCOUNT", font, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Total);
                AddTableCell(tableHost, footerRow, 3, Money(input.ManualDiscountAmount), font, FontWeights.Bold,
                    TextAlignment.Center, TableCellBorder.Total);
                footerRow++;
            }

            if (showAlteration)
            {
                AddTableCell(tableHost, footerRow, 0, "", font, FontWeights.Normal, TextAlignment.Left, TableCellBorder.Total);
                AddTableCell(tableHost, footerRow, 1, "", font, FontWeights.Normal, TextAlignment.Center, TableCellBorder.Total);
                AddTableCell(tableHost, footerRow, 2, "ALTERATION", font, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Total);
                AddTableCell(tableHost, footerRow, 3, Money(input.AlterationTotal), font, FontWeights.Bold,
                    TextAlignment.Center, TableCellBorder.Total);
                footerRow++;
            }

            AddTableCell(tableHost, footerRow, 0, "", font, FontWeights.Normal, TextAlignment.Left, TableCellBorder.Total);
            AddTableCell(tableHost, footerRow, 1, "", font, FontWeights.Normal, TextAlignment.Center, TableCellBorder.Total);
            AddTableCell(tableHost, footerRow, 2, "TOTAL", font, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Total);
            AddTableCell(tableHost, footerRow, 3, Money(input.Payable), font, FontWeights.Bold, TextAlignment.Center, TableCellBorder.Total);
        }

        var tableBorder = new Border
        {
            BorderBrush = RetailInvoiceVisuals.BorderBrush,
            BorderThickness = new Thickness(line),
            Child = tableHost,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        outer.Children.Add(tableBorder);

        if (assets?.Logo != null)
        {
            var watermarkW = colWidths[0] * 0.5;
            var watermark = RetailInvoiceVisuals.CreateWatermarkImage(assets.Logo, watermarkW);
            watermark.IsHitTestVisible = false;
            watermark.VerticalAlignment = VerticalAlignment.Center;
            watermark.HorizontalAlignment = HorizontalAlignment.Center;
            var overlay = new Grid
            {
                IsHitTestVisible = false,
                Margin = new Thickness(line, headerH + line, contentWidth - colWidths[0], footerRowCount * totalH + line),
            };
            overlay.Children.Add(watermark);
            outer.Children.Add(overlay);
        }

        return outer;
    }

    private static void AddBodyColumn(
        Grid table,
        int row,
        int col,
        IEnumerable<string> values,
        double fontSize,
        TextAlignment align,
        double lineSpacing)
    {
        var stack = new StackPanel { Margin = new Thickness(4, 4, 4, 4) };
        foreach (var value in values)
        {
            var tb = RetailInvoiceVisuals.Text(value, fontSize, align: align);
            tb.Margin = new Thickness(0, 0, 0, lineSpacing);
            stack.Children.Add(tb);
        }

        var cell = new Border
        {
            BorderBrush = RetailInvoiceVisuals.BorderBrush,
            BorderThickness = ColumnLeftBorder(col),
            Background = Brushes.Transparent,
            Child = stack,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, col);
        table.Children.Add(cell);
    }

    private enum TableCellBorder
    {
        Header,
        Body,
        Total,
    }

    private static Thickness ColumnLeftBorder(int col) =>
        col > 0 ? new Thickness(1, 0, 0, 0) : new Thickness(0);

    private static Thickness CellBorderThickness(int col, TableCellBorder borderKind)
    {
        var left = col > 0 ? 1 : 0;
        return borderKind switch
        {
            TableCellBorder.Header => new Thickness(left, 0, 0, 1),
            TableCellBorder.Total => new Thickness(left, 1, 0, 0),
            _ => new Thickness(left, 0, 0, 0),
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
        int colSpan = 1)
    {
        var thickness = CellBorderThickness(col, borderKind);

        var cell = new Border
        {
            BorderBrush = RetailInvoiceVisuals.BorderBrush,
            BorderThickness = thickness,
            Background = Brushes.Transparent,
            Padding = new Thickness(4, 2, 4, 2),
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = RetailInvoiceVisuals.Text(text, fontSize, weight, align),
        };
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, col);
        if (colSpan > 1)
            Grid.SetColumnSpan(cell, colSpan);
        table.Children.Add(cell);
    }

    private static UIElement BuildFooter(
        ThermalInvoiceInput input,
        double contentWidth,
        double[] colWidths,
        double scale)
    {
        var font = Pt(RetailInvoiceLayout.TermsPt, scale);
        var grid = new Grid
        {
            Width = contentWidth,
            Margin = new Thickness(0, Px(4, scale), 0, 0),
            VerticalAlignment = VerticalAlignment.Bottom,
        };

        for (var i = 0; i < 4; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(colWidths[i]) });

        var terms = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, Px(8, scale), 0),
        };
        terms.Children.Add(RetailInvoiceVisuals.Text("TERMS & CONDITIONS", font, FontWeights.Bold));

        var idx = 1;
        if (!string.IsNullOrWhiteSpace(input.Store.TermsAndConditions))
        {
            terms.Children.Add(RetailInvoiceVisuals.Text($"{idx}. {input.Store.TermsAndConditions}", font));
            idx++;
        }

        foreach (var line in input.Store.PolicyLines ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            terms.Children.Add(RetailInvoiceVisuals.Text($"{idx}. {line}", font));
            idx++;
        }

        Grid.SetColumn(terms, 0);
        Grid.SetColumnSpan(terms, 2);
        grid.Children.Add(terms);

        var sigWidth = colWidths[2] + colWidths[3];
        var sig = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Right,
            Width = sigWidth,
        };
        sig.Children.Add(new Border
        {
            BorderBrush = RetailInvoiceVisuals.BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Width = colWidths[3],
            Height = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, Px(20, scale), 0, Px(2, scale)),
        });
        sig.Children.Add(RetailInvoiceVisuals.Text(
            "SIGNATURE",
            font,
            FontWeights.Bold,
            TextAlignment.Center));
        Grid.SetColumn(sig, 2);
        Grid.SetColumnSpan(sig, 2);
        grid.Children.Add(sig);

        return grid;
    }

    private static double Pt(double pt, double scale) => pt * scale;
    private static double Px(double px, double scale) => px * scale;
    private static string Money(decimal value) => value.ToString("0.00", In);
}
