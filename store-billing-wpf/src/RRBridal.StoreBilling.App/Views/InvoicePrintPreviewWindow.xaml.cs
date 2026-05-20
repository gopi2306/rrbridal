using System.Windows;
using System.Windows.Documents;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Invoicing;

namespace RRBridal.StoreBilling.App.Views;

public partial class InvoicePrintPreviewWindow : Window
{
    private readonly AppServices _services;
    private readonly FlowDocument _document;
    private readonly bool _printInvoiceEnabled;

    public bool PrintSucceeded { get; private set; }

    public InvoicePrintPreviewWindow(AppServices services, FlowDocument document, string invoiceText, bool printInvoiceEnabled)
    {
        InitializeComponent();
        _services = services;
        _document = document;
        _printInvoiceEnabled = printInvoiceEnabled;
        PreviewViewer.Document = document;
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

        var print = _services.ReceiptConfig.Current.Print;
        var printed = false;

        if (print.AlwaysUsePrintDialog || string.IsNullOrWhiteSpace(print.BillPrinterFullName))
        {
            printed = BillPrintService.ShowPrintDialog(this, _document, "RR Bridal bill");
        }
        else if (BillPrintService.TryPrintToQueue(_document, print.BillPrinterFullName!, "RR Bridal bill"))
        {
            printed = true;
        }
        else
        {
            var r = MessageBox.Show(
                $"Could not print to saved printer \"{print.BillPrinterFullName}\". Open the print dialog to pick a printer?",
                "RR Bridal Billing",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (r == MessageBoxResult.Yes)
                printed = BillPrintService.ShowPrintDialog(this, _document, "RR Bridal bill");
        }

        if (!printed)
            return;

        PrintSucceeded = true;
        try
        {
            DialogResult = true;
        }
        catch
        {
            /* non-modal owner */
        }

        Close();
    }
}
