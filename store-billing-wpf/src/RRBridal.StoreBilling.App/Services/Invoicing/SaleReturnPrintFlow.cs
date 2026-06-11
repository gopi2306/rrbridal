using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using RRBridal.StoreBilling.App.Models;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Thermal sales return / credit note preview and print.</summary>
public static class SaleReturnPrintFlow
{
    public static async Task<bool> ShowAsync(
        AppServices services,
        IReadOnlyList<SaleReturnLineItem> returnLines,
        string returnNo,
        string originalBillNo,
        string returnModeLabel,
        decimal grossAmount,
        decimal returnTotal,
        decimal cgstTotal,
        decimal sgstTotal,
        decimal igstTotal,
        bool isInterState,
        string? creditNoteNo = null,
        decimal cashRefunded = 0m)
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

            var config = services.ReceiptConfig.Current;
            var printSettings = config.Print;
            var charWidth = printSettings.ReceiptCharWidth is >= 32 and <= 56 ? printSettings.ReceiptCharWidth : 48;
            var now = DateTime.Now;

            var input = new ThermalSaleReturnInput
            {
                Store = config.Store,
                CharWidth = charWidth,
                ReturnNo = returnNo,
                OriginalBillNo = originalBillNo,
                UserName = services.UserSession?.LoggedInUser.Name ?? "",
                Counter = services.StoreContext.PosCounter,
                ReturnDate = now.ToString("dd/MM/yy", CultureInfo.GetCultureInfo("en-IN")),
                ReturnTime = now.ToString("h:mmtt", CultureInfo.GetCultureInfo("en-IN")).ToUpperInvariant(),
                ReturnModeLabel = returnModeLabel,
                CreditNoteNo = creditNoteNo,
                Lines = returnLines.Select(l => new SaleReturnLineSnap
                {
                    Description = string.IsNullOrWhiteSpace(l.Description) ? l.ProductCode : l.Description,
                    Qty = l.ReturnQty,
                    Rate = l.Rate,
                    Amount = l.LineReturnTotal,
                }).ToList(),
                TotalQty = returnLines.Sum(l => l.ReturnQty),
                ItemCount = returnLines.Count,
                GrossAmount = grossAmount,
                TaxTotal = cgstTotal + sgstTotal + igstTotal,
                ReturnAmount = returnTotal,
                IsInterState = isInterState,
                CgstTotal = cgstTotal,
                SgstTotal = sgstTotal,
                IgstTotal = igstTotal,
                CashRefunded = cashRefunded,
            };

            var text = ThermalSaleReturnTextBuilder.Build(input);
            var fullAssets = await ThermalReceiptDocumentBuilder.BuildAssetsAsync(
                config,
                returnNo,
                services.ReceiptLogoCache);
            var assets = new ThermalReceiptAssets
            {
                BillBarcode = fullAssets.BillBarcode,
                BillNoLabel = fullAssets.BillNoLabel,
            };
            var fontSize = charWidth >= 48 ? 9.0 : 10.0;
            var doc = BillPrintService.CreateReceiptDocument(text, assets, fontSize);

            var dlg = new Views.InvoicePrintPreviewWindow(
                services,
                doc,
                text,
                printInvoiceEnabled: true,
                forceThermalPrinter: true)
            {
                Owner = Application.Current.MainWindow,
                Title = string.Equals(returnModeLabel, "Credit Note", StringComparison.OrdinalIgnoreCase)
                    ? "Credit note preview"
                    : "Sales return preview",
            };
            dlg.ShowDialog();
            return dlg.PrintSucceeded;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open return receipt preview: {ex.Message}", "Print", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }
}
