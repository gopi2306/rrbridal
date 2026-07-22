using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>A4 credit invoice / balance payment receipt (separate from retail invoice).</summary>
public static class CreditA4DocumentBuilder
{
    private static readonly CultureInfo In = CultureInfo.GetCultureInfo("en-IN");
    private static readonly Brush BorderBrush = Brushes.Black;
    private const double PageWidthMm = 210;
    private const double PageHeightMm = 297;
    private const double MarginMm = 14;

    public static FlowDocument Create(CreditReceiptPrintInput input)
    {
        var pageWidth = InvoiceImageScaling.MmToPx(PageWidthMm);
        var pageHeight = InvoiceImageScaling.MmToPx(PageHeightMm);
        var margin = InvoiceImageScaling.MmToPx(MarginMm);
        var contentWidth = pageWidth - margin * 2;

        var doc = new FlowDocument
        {
            PageWidth = pageWidth,
            PageHeight = pageHeight,
            PagePadding = new Thickness(0),
            ColumnWidth = pageWidth,
            IsColumnWidthFlexible = false,
            FontFamily = new FontFamily("Segoe UI, Arial"),
            Background = Brushes.White,
        };

        doc.Blocks.Add(new BlockUIContainer(BuildPage(input, pageWidth, pageHeight, margin, contentWidth)));
        return doc;
    }

    private static FrameworkElement BuildPage(
        CreditReceiptPrintInput input,
        double pageWidth,
        double pageHeight,
        double margin,
        double contentWidth)
    {
        var root = new Border
        {
            Width = pageWidth,
            Height = pageHeight,
            Background = Brushes.White,
            Padding = new Thickness(margin),
            Child = new StackPanel { Width = contentWidth },
        };

        var stack = (StackPanel)root.Child;

        stack.Children.Add(Header(input, contentWidth));
        stack.Children.Add(MetaSection(input, contentWidth));
        stack.Children.Add(LinesTable(input, contentWidth));
        stack.Children.Add(TotalsBlock(input, contentWidth));
        stack.Children.Add(PaymentHistoryTable(input, contentWidth));

        if (!string.IsNullOrWhiteSpace(input.Store.ThankYouLine))
        {
            stack.Children.Add(new TextBlock
            {
                Text = input.Store.ThankYouLine,
                FontSize = 11,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 16, 0, 0),
                TextAlignment = TextAlignment.Center,
            });
        }

        return root;
    }

    private static FrameworkElement Header(CreditReceiptPrintInput input, double width)
    {
        var grid = new Grid { Width = width, Margin = new Thickness(0, 0, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel();
        left.Children.Add(new TextBlock
        {
            Text = input.Store.StoreName,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
        });
        if (!string.IsNullOrWhiteSpace(input.Store.Address))
            left.Children.Add(new TextBlock { Text = input.Store.Address, FontSize = 11, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(input.Store.CustomerCarePhone))
            left.Children.Add(new TextBlock { Text = $"Ph: {input.Store.CustomerCarePhone}", FontSize = 11 });
        if (!string.IsNullOrWhiteSpace(input.Store.Gstin))
            left.Children.Add(new TextBlock { Text = $"GSTIN: {input.Store.Gstin}", FontSize = 11 });
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        var right = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                new TextBlock
                {
                    Text = input.DocumentTitle,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    TextAlignment = TextAlignment.Right,
                },
                new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(input.Status) ? "" : $"Status: {input.Status}",
                    FontSize = 11,
                    TextAlignment = TextAlignment.Right,
                    Margin = new Thickness(0, 4, 0, 0),
                },
            },
        };
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }

    private static FrameworkElement MetaSection(CreditReceiptPrintInput input, double width)
    {
        var grid = new Grid { Width = width };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var billTo = new StackPanel
        {
            Children =
            {
                BoldLabel("BILL TO"),
                new TextBlock { Text = input.CustomerName, FontSize = 12, FontWeight = FontWeights.SemiBold },
                new TextBlock { Text = input.CustomerPhone, FontSize = 11 },
                new TextBlock { Text = input.CustomerCode, FontSize = 11, Foreground = Brushes.DimGray },
            },
        };
        var billToCell = CellBorder(billTo, new Thickness(0, 0, 1, 0), new Thickness(8, 8, 8, 8));
        Grid.SetColumn(billToCell, 0);
        grid.Children.Add(billToCell);

        var meta = new StackPanel
        {
            Children =
            {
                MetaRow("Invoice / Bill no", input.BillNo),
                MetaRow("Date", input.BillDate),
                MetaRow("Receipt no", input.ReceiptNo ?? "—"),
                MetaRow("Salesman", string.IsNullOrWhiteSpace(input.Salesman) ? "—" : input.Salesman),
                MetaRow("Counter", string.IsNullOrWhiteSpace(input.Counter) ? "—" : input.Counter),
            },
        };
        var metaCell = CellBorder(meta, new Thickness(0), new Thickness(8, 8, 8, 8));
        Grid.SetColumn(metaCell, 1);
        grid.Children.Add(metaCell);

        return SectionBorder(grid, new Thickness(0, 0, 0, 12));
    }

    private static FrameworkElement LinesTable(CreditReceiptPrintInput input, double width)
    {
        var panel = new StackPanel { Width = width, Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(BoldLabel("ITEMS"));

        var table = new StackPanel();
        table.Children.Add(Row("Item", "Qty", "Rate", "Amount", isHeader: true));

        if (input.Lines.Count == 0)
        {
            table.Children.Add(EmptyTableRow("(No line items)", 4));
        }
        else
        {
            foreach (var line in input.Lines)
            {
                table.Children.Add(Row(
                    string.IsNullOrWhiteSpace(line.Description) ? $"#{line.LineNo}" : line.Description,
                    line.Qty.ToString("0.##", In),
                    line.Rate.ToString("N2", In),
                    line.Amount.ToString("N2", In),
                    isHeader: false));
            }
        }

        panel.Children.Add(SectionBorder(table, margin: new Thickness(0)));
        return panel;
    }

    private static FrameworkElement TotalsBlock(CreditReceiptPrintInput input, double width)
    {
        var stack = new StackPanel
        {
            Children =
            {
                TotalRow("Total", MoneyMath.FormatRupee(input.TotalPayable), false),
                TotalRow("Advance at post", MoneyMath.FormatRupee(input.AdvanceAtPost), false),
                TotalRow("Amount paid (this)", MoneyMath.FormatRupee(input.AmountPaidThisTime), false),
                TotalRow("Cumulative paid", MoneyMath.FormatRupee(input.CumulativeAmountPaid), false),
                TotalRow("Balance due", MoneyMath.FormatRupee(input.BalanceDue), true),
                new TextBlock
                {
                    Text = $"Mode: {input.PaymentMode}"
                        + (string.IsNullOrWhiteSpace(input.Reference) ? "" : $"  |  Ref: {input.Reference}"),
                    FontSize = 11,
                    Margin = new Thickness(0, 8, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

        var bordered = SectionBorder(stack, new Thickness(0, 0, 0, 16), new Thickness(8, 8, 8, 8));
        bordered.Width = width * 0.45;
        bordered.HorizontalAlignment = HorizontalAlignment.Right;
        return bordered;
    }

    private static FrameworkElement PaymentHistoryTable(CreditReceiptPrintInput input, double width)
    {
        var panel = new StackPanel { Width = width };
        panel.Children.Add(BoldLabel("PREVIOUS PAYMENT DETAIL"));

        var table = new StackPanel();
        table.Children.Add(HistoryRow(
            "Date of payment", "Amount", "Payment method", "Additional information", isHeader: true));

        if (input.PaymentHistory.Count == 0)
        {
            table.Children.Add(EmptyTableRow("No payments recorded yet.", 4));
        }
        else
        {
            foreach (var row in input.PaymentHistory)
            {
                var extra = string.Join(" · ", new[]
                {
                    string.IsNullOrWhiteSpace(row.Reference) ? null : row.Reference,
                    string.IsNullOrWhiteSpace(row.ReceiptNo) ? null : row.ReceiptNo,
                }.Where(x => x != null)!);
                table.Children.Add(HistoryRow(
                    row.ReceivedAtDisplay,
                    MoneyMath.FormatRupee(row.Amount),
                    string.IsNullOrWhiteSpace(row.Mode) ? row.Kind : row.Mode,
                    extra,
                    isHeader: false));
            }
        }

        panel.Children.Add(SectionBorder(table, margin: new Thickness(0)));
        return panel;
    }

    private static FrameworkElement Row(string item, string qty, string rate, string amount, bool isHeader)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.2, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.6, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });

        AddTableCell(g, 0, item, TextAlignment.Left, isHeader, drawRightBorder: true);
        AddTableCell(g, 1, qty, TextAlignment.Right, isHeader, drawRightBorder: true);
        AddTableCell(g, 2, rate, TextAlignment.Right, isHeader, drawRightBorder: true);
        AddTableCell(g, 3, amount, TextAlignment.Right, isHeader, drawRightBorder: false);

        return g;
    }

    private static FrameworkElement HistoryRow(
        string date, string amount, string method, string extra, bool isHeader)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });

        AddTableCell(g, 0, date, TextAlignment.Left, isHeader, drawRightBorder: true);
        AddTableCell(g, 1, amount, TextAlignment.Left, isHeader, drawRightBorder: true);
        AddTableCell(g, 2, method, TextAlignment.Left, isHeader, drawRightBorder: true);
        AddTableCell(g, 3, extra, TextAlignment.Left, isHeader, drawRightBorder: false);

        return g;
    }

    private static FrameworkElement EmptyTableRow(string message, int colSpan)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition());
        var cell = new Border
        {
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 6, 6, 6),
            Child = new TextBlock
            {
                Text = message,
                FontSize = 11,
                Foreground = Brushes.Gray,
            },
        };
        Grid.SetColumn(cell, 0);
        Grid.SetColumnSpan(cell, colSpan);
        g.Children.Add(cell);
        return g;
    }

    private static void AddTableCell(
        Grid g,
        int col,
        string text,
        TextAlignment align,
        bool isHeader,
        bool drawRightBorder)
    {
        var cell = new Border
        {
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, drawRightBorder ? 1 : 0, isHeader ? 1 : 0),
            Padding = new Thickness(6, 4, 6, 4),
            SnapsToDevicePixels = true,
            Child = new TextBlock
            {
                Text = text ?? "",
                FontSize = 11,
                FontWeight = isHeader ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = Brushes.Black,
                TextAlignment = align,
                TextWrapping = TextWrapping.Wrap,
            },
        };
        Grid.SetColumn(cell, col);
        g.Children.Add(cell);
    }

    private static Border SectionBorder(UIElement child, Thickness? margin = null, Thickness? padding = null)
    {
        return new Border
        {
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            Padding = padding ?? new Thickness(0),
            Margin = margin ?? new Thickness(0),
            SnapsToDevicePixels = true,
            Child = child,
        };
    }

    private static Border CellBorder(UIElement child, Thickness border, Thickness padding)
    {
        return new Border
        {
            BorderBrush = BorderBrush,
            BorderThickness = border,
            Padding = padding,
            SnapsToDevicePixels = true,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = child,
        };
    }

    private static TextBlock BoldLabel(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeights.Bold,
        Margin = new Thickness(0, 0, 0, 4),
    };

    private static FrameworkElement MetaRow(string label, string value)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        var l = new TextBlock { Text = label, FontSize = 11, Foreground = Brushes.DimGray };
        var v = new TextBlock { Text = value, FontSize = 11, FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(l, 0);
        Grid.SetColumn(v, 1);
        g.Children.Add(l);
        g.Children.Add(v);
        return g;
    }

    private static FrameworkElement TotalRow(string label, string value, bool emphasis)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition());

        var labelCell = new Border
        {
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(0, 2, 8, 2),
            SnapsToDevicePixels = true,
            Child = new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = emphasis ? FontWeights.Bold : FontWeights.Normal,
            },
        };
        var valueCell = new Border
        {
            Padding = new Thickness(8, 2, 0, 2),
            SnapsToDevicePixels = true,
            Child = new TextBlock
            {
                Text = value,
                FontSize = emphasis ? 13 : 11,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Right,
                Foreground = Brushes.Black,
            },
        };
        Grid.SetColumn(labelCell, 0);
        Grid.SetColumn(valueCell, 1);
        g.Children.Add(labelCell);
        g.Children.Add(valueCell);
        return g;
    }
}
