using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public sealed class BillPrintService
{
    /// <summary>~80mm at 96 DPI.</summary>
    private const double PageWidthPx = 80.0 / 25.4 * 96.0;

    public static FlowDocument CreateFlowDocument(string monospaceText, double fontSize = 10.0)
    {
        var doc = new FlowDocument
        {
            PageWidth = PageWidthPx,
            PagePadding = new Thickness(8, 8, 8, 8),
            FontFamily = new FontFamily("Consolas,Courier New"),
            FontSize = fontSize,
            Foreground = Brushes.Black,
            Background = Brushes.White,
        };

        foreach (var line in monospaceText.Replace("\r\n", "\n").Split('\n'))
        {
            var p = new Paragraph(new Run(line))
            {
                Margin = new Thickness(0),
                LineHeight = fontSize * 1.15,
            };
            doc.Blocks.Add(p);
        }

        return doc;
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
