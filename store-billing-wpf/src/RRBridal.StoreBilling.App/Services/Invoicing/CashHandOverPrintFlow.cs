using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using RRBridal.StoreBilling.App.Services.Store;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class CashHandOverPrintFlow
{
    public static async Task<bool> ShowAsync(
        AppServices services,
        CashHandOverThermalInput input,
        string windowTitle = "Cash hand over preview")
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
            input = new CashHandOverThermalInput
            {
                Store = input.Store,
                CharWidth = charWidth,
                BusinessDate = input.BusinessDate,
                Counter = input.Counter,
                UserName = input.UserName,
                Denominations = input.Denominations,
                CashInHand = input.CashInHand,
                MorningCash = input.MorningCash,
                ExpectedCash = input.ExpectedCash,
                Difference = input.Difference,
                StatusLabel = input.StatusLabel,
                CashTaken = input.CashTaken,
                PrintAllDenominations = input.PrintAllDenominations,
            };

            var text = CashHandOverThermalTextBuilder.Build(input);
            var fontSize = charWidth >= 48 ? 9.0 : 10.0;
            var doc = BillPrintService.CreateReceiptDocument(text, assets: null, fontSize);

            var dlg = new Views.InvoicePrintPreviewWindow(
                services,
                doc,
                text,
                printInvoiceEnabled: true,
                forceThermalPrinter: true)
            {
                Owner = Application.Current.MainWindow,
                Title = windowTitle,
            };
            dlg.ShowDialog();
            return dlg.PrintSucceeded;
        }
        catch (Exception ex)
        {
            AppDialog.Show($"Could not open cash hand over preview: {ex.Message}", "Print", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    public static string ResolveStatusLabel(decimal difference)
    {
        if (difference == 0m)
            return "BALANCED";
        return difference > 0 ? "EXCESS" : "SHORT";
    }

    public static CashHandOverThermalInput BuildInput(
        AppServices services,
        IReadOnlyList<CashDenominationLine> denominations,
        decimal cashInHand,
        decimal morningCash,
        decimal expectedCash,
        string businessDateDisplay,
        string? cashTaken = null)
    {
        var difference = cashInHand - expectedCash;
        return new CashHandOverThermalInput
        {
            Store = services.ReceiptConfig.Current.Store,
            CharWidth = services.ReceiptConfig.Current.Print.ReceiptCharWidth is >= 32 and <= 56
                ? services.ReceiptConfig.Current.Print.ReceiptCharWidth
                : 48,
            BusinessDate = businessDateDisplay,
            Counter = services.StoreContext.PosCounter,
            UserName = services.UserSession?.LoggedInUser.Name ?? "",
            Denominations = denominations,
            CashInHand = cashInHand,
            MorningCash = morningCash,
            ExpectedCash = expectedCash,
            Difference = difference,
            StatusLabel = ResolveStatusLabel(difference),
            CashTaken = cashTaken,
        };
    }
}
