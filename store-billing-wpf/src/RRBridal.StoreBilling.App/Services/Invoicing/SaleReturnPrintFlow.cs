using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using MongoDB.Bson;
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
        decimal cashRefunded = 0m,
        bool isDuplicate = false,
        DateTime? returnPostedUtc = null)
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

            var config = services.ReceiptConfig.Current;
            var printSettings = config.Print;
            var charWidth = printSettings.ReceiptCharWidth is >= 32 and <= 56 ? printSettings.ReceiptCharWidth : 48;
            var localPosted = returnPostedUtc?.ToLocalTime() ?? DateTime.Now;

            var input = new ThermalSaleReturnInput
            {
                Store = config.Store,
                CharWidth = charWidth,
                ReturnNo = returnNo,
                OriginalBillNo = originalBillNo,
                UserName = services.UserSession?.LoggedInUser.Name ?? "",
                Counter = services.StoreContext.PosCounter,
                ReturnDate = localPosted.ToString("dd/MM/yy", CultureInfo.GetCultureInfo("en-IN")),
                ReturnTime = localPosted.ToString("h:mmtt", CultureInfo.GetCultureInfo("en-IN")).ToUpperInvariant(),
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
                IsDuplicateCopy = isDuplicate,
            };

            return await ShowFromThermalInputAsync(services, input, returnNo, returnModeLabel);
        }
        catch (Exception ex)
        {
            AppDialog.Show($"Could not open return receipt preview: {ex.Message}", "Print", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    public static async Task<bool> ShowLegacyAsync(
        AppServices services,
        IReadOnlyList<LegacyReturnLineItem> returnLines,
        string returnNo,
        string originalBillNo,
        string originalBillDateDisplay,
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
                AppDialog.Show(profileMsg, "Receipt settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var config = services.ReceiptConfig.Current;
            var printSettings = config.Print;
            var charWidth = printSettings.ReceiptCharWidth is >= 32 and <= 56 ? printSettings.ReceiptCharWidth : 48;
            var localPosted = DateTime.Now;

            var input = new ThermalSaleReturnInput
            {
                Store = config.Store,
                CharWidth = charWidth,
                ReturnNo = returnNo,
                OriginalBillNo = originalBillNo,
                UserName = services.UserSession?.LoggedInUser.Name ?? "",
                Counter = services.StoreContext.PosCounter,
                ReturnDate = localPosted.ToString("dd/MM/yy", CultureInfo.GetCultureInfo("en-IN")),
                ReturnTime = localPosted.ToString("h:mmtt", CultureInfo.GetCultureInfo("en-IN")).ToUpperInvariant(),
                ReturnModeLabel = returnModeLabel,
                CreditNoteNo = creditNoteNo,
                Lines = returnLines.Select(l => new SaleReturnLineSnap
                {
                    Description = string.IsNullOrWhiteSpace(l.Description) ? l.ProductCode : l.Description,
                    Qty = l.Qty,
                    Rate = l.Rate,
                    Amount = l.LineReturnTotal,
                }).ToList(),
                TotalQty = returnLines.Sum(l => l.Qty),
                ItemCount = returnLines.Count,
                GrossAmount = grossAmount,
                TaxTotal = cgstTotal + sgstTotal + igstTotal,
                ReturnAmount = returnTotal,
                IsInterState = isInterState,
                CgstTotal = cgstTotal,
                SgstTotal = sgstTotal,
                IgstTotal = igstTotal,
                CashRefunded = cashRefunded,
                IsLegacy = true,
                OriginalBillDateDisplay = originalBillDateDisplay,
            };

            return await ShowFromThermalInputAsync(services, input, returnNo, returnModeLabel);
        }
        catch (Exception ex)
        {
            AppDialog.Show($"Could not open return receipt preview: {ex.Message}", "Print", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    public static async Task<bool> ShowFromReturnDocumentAsync(
        AppServices services,
        BsonDocument returnDoc,
        string? creditNoteNo,
        bool isDuplicate = false)
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

            var config = services.ReceiptConfig.Current;
            var printSettings = config.Print;
            var charWidth = printSettings.ReceiptCharWidth is >= 32 and <= 56 ? printSettings.ReceiptCharWidth : 48;
            var returnNo = returnDoc.GetValue("returnNo", "").AsString;
            var returnMode = returnDoc.GetValue("returnMode", "").AsString;
            var returnModeLabel = string.Equals(returnMode, "credit_note", StringComparison.OrdinalIgnoreCase)
                ? "Credit Note"
                : "Cash";

            DateTime? postedUtc = null;
            if (returnDoc.TryGetValue("createdAtUtc", out var cu) && cu.IsString
                && DateTime.TryParse(cu.AsString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                postedUtc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();

            var input = SaleReturnDocumentMapper.MapFromReturnDocument(
                returnDoc,
                config.Store,
                charWidth,
                services.UserSession?.LoggedInUser.Name ?? "",
                services.StoreContext.PosCounter,
                creditNoteNo,
                isDuplicate,
                postedUtc);

            return await ShowFromThermalInputAsync(services, input, returnNo, returnModeLabel);
        }
        catch (Exception ex)
        {
            AppDialog.Show($"Could not open return receipt preview: {ex.Message}", "Print", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private static async Task<bool> ShowFromThermalInputAsync(
        AppServices services,
        ThermalSaleReturnInput input,
        string returnNo,
        string returnModeLabel)
    {
        var config = services.ReceiptConfig.Current;
        var printSettings = config.Print;
        var charWidth = printSettings.ReceiptCharWidth is >= 32 and <= 56 ? printSettings.ReceiptCharWidth : 48;

        var text = ThermalSaleReturnTextBuilder.Build(input);
        var barcodeNo = !string.IsNullOrWhiteSpace(input.CreditNoteNo)
            ? input.CreditNoteNo.Trim()
            : returnNo;
        var fullAssets = await ThermalReceiptDocumentBuilder.BuildAssetsAsync(
            config,
            barcodeNo,
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
                ? input.IsDuplicateCopy ? "Credit note duplicate preview" : "Credit note preview"
                : input.IsDuplicateCopy ? "Sales return duplicate preview" : "Sales return preview",
        };
        dlg.ShowDialog();
        return dlg.PrintSucceeded;
    }
}
