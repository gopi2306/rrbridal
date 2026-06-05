using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

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

            var printSettings = services.ReceiptConfig.Current.Print;
            var printFormat = printSettings.PrintFormat;
            var isA5PrePrinted = printFormat == InvoicePrintFormat.A5 && printSettings.A5PrePrintedEnabled;
            var isOfficeFormat = printFormat is InvoicePrintFormat.A4 or InvoicePrintFormat.A5;
            var dualPrint = isOfficeFormat && printSettings.AlsoPrintThermalFirst;

            var assets = await ThermalReceiptDocumentBuilder.BuildAssetsAsync(
                services.ReceiptConfig.Current,
                input.BillNo,
                services.ReceiptLogoCache);
            var text = ThermalInvoiceTextBuilder.Build(input);

            FlowDocument doc;
            FlowDocument? thermalDoc = null;

            if (dualPrint)
            {
                var fontSize = input.CharWidth >= 48 ? 9.0 : 10.0;
                thermalDoc = BillPrintService.CreateReceiptDocument(text, assets, fontSize);
                doc = BuildInvoiceDocument(input, assets, printFormat, isA5PrePrinted, printSettings.A5PrePrintedLayout);
            }
            else if (isA5PrePrinted)
            {
                doc = A5PrePrintedInvoiceDocumentBuilder.Create(input, printSettings.A5PrePrintedLayout);
            }
            else if (isOfficeFormat)
            {
                doc = BuildInvoiceDocument(input, assets, printFormat, isA5PrePrinted, printSettings.A5PrePrintedLayout);
            }
            else
            {
                var fontSize = input.CharWidth >= 48 ? 9.0 : 10.0;
                doc = BillPrintService.CreateReceiptDocument(text, assets, fontSize);
            }

            var isA5 = printFormat == InvoicePrintFormat.A5;
            var isTaxInvoice = isOfficeFormat;
            var title = isA5PrePrinted
                ? "A5 pre-printed preview"
                : isA5
                    ? "A5 invoice preview"
                    : printFormat == InvoicePrintFormat.A4
                        ? "A4 invoice preview"
                        : "Invoice preview";
            var dlg = new Views.InvoicePrintPreviewWindow(
                services,
                doc,
                text,
                printInvoiceEnabled,
                thermalDoc,
                dualPrint)
            {
                Owner = Application.Current.MainWindow,
                Width = isA5 ? 508 : isTaxInvoice ? 720 : 420,
                Height = isA5 ? 720 : isTaxInvoice ? 820 : 560,
                Title = title,
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

    private static FlowDocument BuildInvoiceDocument(
        ThermalInvoiceInput input,
        ThermalReceiptAssets assets,
        InvoicePrintFormat printFormat,
        bool isA5PrePrinted,
        A5PrePrintedLayoutSettings? a5Layout)
    {
        if (isA5PrePrinted)
            return A5PrePrintedInvoiceDocumentBuilder.Create(input, a5Layout);

        var (pageW, pageH) = printFormat == InvoicePrintFormat.A5
            ? (148.0, 210.0)
            : (210.0, 297.0);
        return A4InvoiceDocumentBuilder.Create(input, assets, pageW, pageH);
    }
}
