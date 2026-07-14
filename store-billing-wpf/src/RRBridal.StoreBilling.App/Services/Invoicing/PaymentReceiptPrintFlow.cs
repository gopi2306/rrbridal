using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public sealed class PaymentReceiptPrintInput
{
    public required string ReceiptNo { get; init; }
    public required string BillNo { get; init; }
    public string CustomerName { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public decimal AmountPaid { get; init; }
    public decimal BalanceDue { get; init; }
    public string PaymentMode { get; init; } = "";
    public string Reference { get; init; } = "";
}

public static class PaymentReceiptPrintFlow
{
    public static Task<bool> ShowAsync(AppServices services, PaymentReceiptPrintInput input)
    {
        var doc = new FlowDocument
        {
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12,
            PagePadding = new Thickness(24),
        };

        doc.Blocks.Add(new Paragraph(new Run("PAYMENT RECEIPT")) { FontWeight = FontWeights.Bold, FontSize = 16 });
        doc.Blocks.Add(new Paragraph(new Run($"Receipt: {input.ReceiptNo}")));
        doc.Blocks.Add(new Paragraph(new Run($"Bill: {input.BillNo}")));
        doc.Blocks.Add(new Paragraph(new Run($"Customer: {input.CustomerName}")));
        if (!string.IsNullOrWhiteSpace(input.CustomerPhone))
            doc.Blocks.Add(new Paragraph(new Run($"Mobile: {input.CustomerPhone}")));
        doc.Blocks.Add(new Paragraph(new Run($"Paid: {MoneyMath.FormatRupee(input.AmountPaid)}")) { FontWeight = FontWeights.SemiBold });
        doc.Blocks.Add(new Paragraph(new Run($"Mode: {input.PaymentMode}")));
        if (!string.IsNullOrWhiteSpace(input.Reference))
            doc.Blocks.Add(new Paragraph(new Run($"Reference: {input.Reference}")));
        doc.Blocks.Add(new Paragraph(new Run($"Balance due: {MoneyMath.FormatRupee(input.BalanceDue)}")) { FontWeight = FontWeights.SemiBold });

        var viewer = new FlowDocumentScrollViewer { Document = doc };
        var win = new Window
        {
            Title = "Payment receipt",
            Width = 420,
            Height = 480,
            Content = viewer,
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var print = new Button { Content = "Print", Margin = new Thickness(8), Padding = new Thickness(12, 6, 12, 6) };
        print.Click += (_, _) =>
        {
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() == true)
                dlg.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Payment receipt");
        };
        var close = new Button { Content = "Close", Margin = new Thickness(8), Padding = new Thickness(12, 6, 12, 6) };
        close.Click += (_, _) => win.Close();

        var panel = new DockPanel();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(print);
        buttons.Children.Add(close);
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);
        panel.Children.Add(viewer);
        win.Content = panel;
        win.ShowDialog();
        return Task.FromResult(true);
    }
}
