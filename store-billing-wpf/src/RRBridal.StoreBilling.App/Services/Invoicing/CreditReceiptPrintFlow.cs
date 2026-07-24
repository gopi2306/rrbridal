using System.Threading.Tasks;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using System.Windows.Documents;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class CreditReceiptPrintFlow
{
    public static async Task<bool> ShowAsync(AppServices services, CreditReceiptPrintInput input)
    {
        try
        {
            services.CentralAuthSession.ApplyTo(services.CentralApi);
            var (profileOk, profileMsg) = await services.ReceiptConfigSync.EnsureProfileReadyForPrintAsync();
            if (!profileOk)
            {
                AppDialog.Show(profileMsg, "Receipt settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var print = services.ReceiptConfig.Current.Print;
            var format = print.CreditPrintFormat;
            FlowDocument document;
            var forceThermal = format == CreditPrintFormat.Thermal;
            var previewTitle = input.DocumentTitle;

            if (format == CreditPrintFormat.A4)
            {
                document = CreditA4DocumentBuilder.Create(input);
            }
            else
            {
                var text = CreditThermalTextBuilder.Build(input);
                document = BillPrintService.CreateReceiptDocument(text, assets: null);
            }

            var owner = Application.Current?.MainWindow;
            var printerKind = format == CreditPrintFormat.A4
                ? BillPrinterKind.OfficeInvoice
                : BillPrinterKind.Thermal;
            var win = new InvoicePrintPreviewWindow(
                services,
                document,
                invoiceText: previewTitle,
                printInvoiceEnabled: true,
                thermalDocument: null,
                dualPrint: false,
                forceThermalPrinter: forceThermal,
                printerKindOverride: printerKind)
            {
                Owner = owner,
                Title = format == CreditPrintFormat.A4 ? "Credit receipt (A4)" : "Credit receipt (thermal)",
                Width = format == CreditPrintFormat.A4 ? 720 : 420,
                Height = format == CreditPrintFormat.A4 ? 820 : 560,
            };

            win.ShowDialog();
            return win.PrintSucceeded;
        }
        catch (System.Exception ex)
        {
            AppDialog.Show(
                $"Could not open credit receipt print preview:\n{ex.Message}",
                "Credit receipt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }
}
