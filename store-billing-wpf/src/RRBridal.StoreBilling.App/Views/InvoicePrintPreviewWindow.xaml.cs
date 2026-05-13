using System.Windows;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Invoicing;

namespace RRBridal.StoreBilling.App.Views;

public partial class InvoicePrintPreviewWindow : Window
{
    private readonly AppServices _services;
    private readonly string _invoiceText;
    private readonly bool _printInvoiceEnabled;

    public InvoicePrintPreviewWindow(AppServices services, string invoiceText, bool printInvoiceEnabled)
    {
        InitializeComponent();
        _services = services;
        _invoiceText = invoiceText;
        _printInvoiceEnabled = printInvoiceEnabled;
        BodyText.Text = invoiceText;
    }

    private void Print_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_printInvoiceEnabled)
        {
            MessageBox.Show(
                "Turn on \"Print invoice\" on the billing screen to allow printing.",
                "RR Bridal Billing",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var doc = BillPrintService.CreateFlowDocument(_invoiceText);
        var print = _services.ReceiptConfig.Current.Print;

        if (print.AlwaysUsePrintDialog || string.IsNullOrWhiteSpace(print.BillPrinterFullName))
        {
            BillPrintService.ShowPrintDialog(this, doc, "RR Bridal bill");
            return;
        }

        if (BillPrintService.TryPrintToQueue(doc, print.BillPrinterFullName!, "RR Bridal bill"))
            return;

        var r = MessageBox.Show(
            $"Could not print to saved printer \"{print.BillPrinterFullName}\". Open the print dialog to pick a printer?",
            "RR Bridal Billing",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (r == MessageBoxResult.Yes)
            BillPrintService.ShowPrintDialog(this, doc, "RR Bridal bill");
    }
}
