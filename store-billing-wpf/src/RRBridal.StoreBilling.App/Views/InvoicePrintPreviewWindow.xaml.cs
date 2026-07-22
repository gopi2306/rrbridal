using System.Windows;
using System.Windows.Documents;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Invoicing;

namespace RRBridal.StoreBilling.App.Views;

public partial class InvoicePrintPreviewWindow : Window
{
    private readonly AppServices _services;
    private readonly FlowDocument _document;
    private readonly FlowDocument? _thermalDocument;
    private readonly bool _dualPrint;
    private readonly bool _printInvoiceEnabled;
    private readonly bool _forceThermalPrinter;
    private readonly BillPrinterKind? _printerKindOverride;

    public bool PrintSucceeded { get; private set; }

    public InvoicePrintPreviewWindow(
        AppServices services,
        FlowDocument document,
        string invoiceText,
        bool printInvoiceEnabled,
        FlowDocument? thermalDocument = null,
        bool dualPrint = false,
        bool forceThermalPrinter = false,
        BillPrinterKind? printerKindOverride = null)
    {
        InitializeComponent();
        _services = services;
        _document = document;
        _thermalDocument = thermalDocument;
        _dualPrint = dualPrint && thermalDocument != null;
        _printInvoiceEnabled = printInvoiceEnabled;
        _forceThermalPrinter = forceThermalPrinter;
        _printerKindOverride = printerKindOverride;
        PreviewViewer.Document = document;

        if (_dualPrint)
            PrintButton.Content = "Print thermal + invoice";
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

        if (_dualPrint && _thermalDocument != null)
        {
            if (!BillPrintService.PrintDocument(this, _thermalDocument, print, "RR Bridal thermal receipt", BillPrinterKind.Thermal))
                return;

            if (!BillPrintService.PrintDocument(this, _document, print, "RR Bridal invoice", BillPrinterKind.OfficeInvoice))
                return;
        }
        else
        {
            var kind = _printerKindOverride
                ?? (_forceThermalPrinter || print.PrintFormat == InvoicePrintFormat.Thermal
                    ? BillPrinterKind.Thermal
                    : BillPrinterKind.OfficeInvoice);
            var jobName = _forceThermalPrinter || kind == BillPrinterKind.Thermal
                ? "RR Bridal receipt"
                : "RR Bridal bill";
            if (!BillPrintService.PrintDocument(this, _document, print, jobName, kind))
                return;
        }

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
