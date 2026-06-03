using System;
using System.Threading.Tasks;
using System.Windows;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Shared thermal invoice preview/print for billing, duplicate reprint, and ledger.</summary>
public static class InvoicePrintFlow
{
    public static async Task<bool> ShowAsync(
        AppServices services,
        ThermalInvoiceInput input,
        bool printInvoiceEnabled = true)
    {
        try
        {
            services.CentralAuthSession.ApplyTo(services.CentralApi);
            var (profileOk, profileMsg) = await services.ReceiptConfigSync.EnsureProfileReadyForPrintAsync();
            if (!profileOk)
            {
                MessageBox.Show(profileMsg, "Receipt settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var printFormat = services.ReceiptConfig.Current.Print.PrintFormat;
            var assets = await ThermalReceiptDocumentBuilder.BuildAssetsAsync(
                services.ReceiptConfig.Current,
                input.BillNo,
                services.ReceiptLogoCache);

            string text;
            System.Windows.Documents.FlowDocument doc;
            if (printFormat is InvoicePrintFormat.A4 or InvoicePrintFormat.A5)
            {
                text = ThermalInvoiceTextBuilder.Build(input);
                var (pageW, pageH) = printFormat == InvoicePrintFormat.A5
                    ? (148.0, 210.0)
                    : (210.0, 297.0);
                doc = A4InvoiceDocumentBuilder.Create(input, assets, pageW, pageH);
            }
            else
            {
                text = ThermalInvoiceTextBuilder.Build(input);
                var fontSize = input.CharWidth >= 48 ? 9.0 : 10.0;
                doc = BillPrintService.CreateReceiptDocument(text, assets, fontSize);
            }

            var isA5 = printFormat == InvoicePrintFormat.A5;
            var isTaxInvoice = printFormat is InvoicePrintFormat.A4 or InvoicePrintFormat.A5;
            var dlg = new Views.InvoicePrintPreviewWindow(services, doc, text, printInvoiceEnabled)
            {
                Owner = Application.Current.MainWindow,
                Width = isA5 ? 508 : isTaxInvoice ? 720 : 420,
                Height = isA5 ? 720 : isTaxInvoice ? 820 : 560,
                Title = isA5 ? "A5 invoice preview" : printFormat == InvoicePrintFormat.A4 ? "A4 invoice preview" : "Invoice preview",
            };
            dlg.ShowDialog();
            return dlg.PrintSucceeded;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open invoice preview: {ex.Message}", "Print", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }
}
