using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public sealed class BillPrintService
{
    /// <summary>~80mm at 96 DPI.</summary>
    private const double DefaultPageWidthPx = 80.0 / 25.4 * 96.0;

    private const double PagePaddingPx = 8.0;

    private const double PageTopPaddingWithLogoPx = 2.0;

    private const double LogoBlockMarginBottomPx = 2.0;

    /// <summary>Cap logo height so tall assets do not dominate the receipt.</summary>
    private const double LogoMaxHeightPx = 28.0 / 25.4 * 96.0;

    private const double QrSlotWidthPx = 22.0 / 25.4 * 96.0;

    public static FlowDocument CreateFlowDocument(string monospaceText, double fontSize = 10.0)
        => CreateReceiptDocument(monospaceText, null, fontSize);

    public static FlowDocument CreateReceiptDocument(
        string monospaceText,
        ThermalReceiptAssets? assets,
        double fontSize = 10.0,
        double? pageWidthPx = null)
    {
        var pageWidth = pageWidthPx ?? DefaultPageWidthPx;
        var contentWidth = pageWidth - PagePaddingPx * 2;

        var hasLogo = assets?.Logo != null;
        var doc = new FlowDocument
        {
            PageWidth = pageWidth,
            PagePadding = hasLogo
                ? new Thickness(PagePaddingPx, PageTopPaddingWithLogoPx, PagePaddingPx, PagePaddingPx)
                : new Thickness(PagePaddingPx),
            FontFamily = new FontFamily("Consolas,Courier New"),
            FontSize = fontSize,
            Foreground = Brushes.Black,
            Background = Brushes.White,
        };

        if (hasLogo)
        {
            var logoImg = CreateScaledImage(assets!.Logo!, contentWidth, LogoMaxHeightPx);
            var logoPara = new Paragraph(new InlineUIContainer(logoImg))
            {
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, LogoBlockMarginBottomPx),
                Padding = new Thickness(0),
            };
            doc.Blocks.Add(logoPara);
        }

        foreach (var line in monospaceText.Replace("\r\n", "\n").Split('\n'))
        {
            var p = new Paragraph(new Run(line))
            {
                Margin = new Thickness(0),
                LineHeight = fontSize * 1.15,
            };
            doc.Blocks.Add(p);
        }

        if (assets == null)
            return doc;

        if (assets.QrCodes.Count > 0)
        {
            var qrTable = new Table
            {
                CellSpacing = 0,
                Margin = new Thickness(0, 8, 0, 4),
            };
            foreach (var _ in assets.QrCodes)
                qrTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

            var rowGroup = new TableRowGroup();
            var row = new TableRow();
            foreach (var qr in assets.QrCodes)
            {
                var cell = new TableCell
                {
                    TextAlignment = TextAlignment.Center,
                    Padding = new Thickness(2),
                };
                cell.Blocks.Add(new BlockUIContainer(CreateScaledImage(qr.Image, QrSlotWidthPx, null)));
                if (!string.IsNullOrWhiteSpace(qr.Label))
                {
                    cell.Blocks.Add(new Paragraph(new Run(qr.Label))
                    {
                        FontSize = fontSize - 1,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 2, 0, 0),
                    });
                }
                row.Cells.Add(cell);
            }
            rowGroup.Rows.Add(row);
            qrTable.RowGroups.Add(rowGroup);
            doc.Blocks.Add(qrTable);
        }

        if (assets.BillBarcode != null)
        {
            var bcImg = CreateScaledImage(assets.BillBarcode, contentWidth, null);
            var bcPara = new Paragraph(new InlineUIContainer(bcImg))
            {
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
            };
            doc.Blocks.Add(bcPara);
            if (!string.IsNullOrWhiteSpace(assets.BillNoLabel))
            {
                doc.Blocks.Add(new Paragraph(new Run(assets.BillNoLabel))
                {
                    TextAlignment = TextAlignment.Center,
                    FontSize = fontSize,
                    Margin = new Thickness(0, 2, 0, 0),
                });
            }
        }

        return doc;
    }

    /// <summary>
    /// Sizes the image to fill <paramref name="targetWidth"/> (FlowDocument ignores MaxWidth-only upscaling).
    /// </summary>
    private static Image CreateScaledImage(ImageSource source, double targetWidth, double? maxHeightPx)
    {
        var (width, height) = ComputeScaledSize(source, targetWidth, maxHeightPx);
        return new Image
        {
            Source = source,
            Stretch = Stretch.Uniform,
            Width = width,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
        };
    }

    private static (double Width, double Height) ComputeScaledSize(
        ImageSource source,
        double targetWidth,
        double? maxHeightPx)
    {
        if (source.Width <= 0 || source.Height <= 0)
            return (targetWidth, double.NaN);

        var scale = targetWidth / source.Width;
        var height = source.Height * scale;
        if (maxHeightPx.HasValue && height > maxHeightPx.Value)
        {
            scale = maxHeightPx.Value / source.Height;
            return (source.Width * scale, maxHeightPx.Value);
        }

        return (targetWidth, height);
    }

    /// <summary>Print to a specific queue, or return false if queue unavailable.</summary>
    public static bool TryPrintToQueue(FlowDocument document, string printQueueFullName, string jobName)
    {
        try
        {
            using var server = new LocalPrintServer();
            PrintQueue? queue = null;
            foreach (PrintQueue pq in server.GetPrintQueues())
            {
                if (string.Equals(pq.FullName, printQueueFullName, StringComparison.OrdinalIgnoreCase))
                {
                    queue = pq;
                    break;
                }
            }

            if (queue == null)
                return false;

            var writer = PrintQueue.CreateXpsDocumentWriter(queue);
            writer.Write(((IDocumentPaginatorSource)document).DocumentPaginator);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool ShowPrintDialog(Window? owner, FlowDocument document, string jobName)
    {
        var dlg = new PrintDialog();
        dlg.PrintTicket = new PrintTicket
        {
            PageOrientation = PageOrientation.Portrait,
        };
        if (dlg.ShowDialog() != true)
            return false;
        dlg.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, jobName);
        return true;
    }
}
