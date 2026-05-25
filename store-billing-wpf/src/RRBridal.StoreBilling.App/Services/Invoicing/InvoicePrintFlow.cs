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

            var text = ThermalInvoiceTextBuilder.Build(input);
            var assets = await ThermalReceiptDocumentBuilder.BuildAssetsAsync(
                services.ReceiptConfig.Current,
                input.BillNo,
                services.ReceiptLogoCache);
            var fontSize = input.CharWidth >= 48 ? 9.0 : 10.0;
            var doc = BillPrintService.CreateReceiptDocument(text, assets, fontSize);
            var dlg = new Views.InvoicePrintPreviewWindow(services, doc, text, printInvoiceEnabled)
            {
                Owner = Application.Current.MainWindow,
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
