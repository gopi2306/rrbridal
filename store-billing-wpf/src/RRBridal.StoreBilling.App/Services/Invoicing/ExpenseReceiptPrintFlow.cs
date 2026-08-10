using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Expenses;
using RRBridal.StoreBilling.App.Services.Ui;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public static class ExpenseReceiptPrintFlow
{
    public static async Task<bool> ShowAsync(AppServices services, BsonDocument expense)
    {
        try
        {
            services.CentralAuthSession.ApplyTo(services.CentralApi);
            var (profileOk, profileMessage) = await services.ReceiptConfigSync.EnsureProfileReadyForPrintAsync();
            if (!profileOk)
            {
                AppDialog.Show(profileMessage, "Receipt settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var config = services.ReceiptConfig.Current;
            var width = config.Print.ReceiptCharWidth is >= 32 and <= 56
                ? config.Print.ReceiptCharWidth
                : 48;
            var input = new ExpenseThermalInput
            {
                Store = config.Store,
                CharWidth = width,
                ExpenseNo = Read(expense, "expenseNo"),
                BusinessDate = Read(expense, "businessDate"),
                SupplierName = Read(expense, "supplierName"),
                SupplierGstin = Read(expense, "supplierGstin"),
                SupplierInvoiceNo = Read(expense, "supplierInvoiceNo"),
                Category = Read(expense, "category"),
                Description = Read(expense, "description"),
                TaxableAmount = DailyExpenseDomain.ReadDecimal(expense, "taxableAmount"),
                CgstAmount = DailyExpenseDomain.ReadDecimal(expense, "cgstAmount"),
                SgstAmount = DailyExpenseDomain.ReadDecimal(expense, "sgstAmount"),
                IgstAmount = DailyExpenseDomain.ReadDecimal(expense, "igstAmount"),
                TotalAmount = DailyExpenseDomain.ReadDecimal(expense, "amount"),
                PaymentSummary = FormatPayments(expense),
                Status = Read(expense, "status", "posted"),
            };
            var text = ExpenseThermalTextBuilder.Build(input);
            var document = BillPrintService.CreateReceiptDocument(text, assets: null, width >= 48 ? 9.0 : 10.0);
            var preview = new InvoicePrintPreviewWindow(
                services,
                document,
                text,
                printInvoiceEnabled: true,
                forceThermalPrinter: true)
            {
                Owner = Application.Current?.MainWindow,
                Title = $"Expense voucher {input.ExpenseNo}",
                Width = 430,
                Height = 620,
            };
            preview.ShowDialog();
            return preview.PrintSucceeded;
        }
        catch (Exception ex)
        {
            AppDialog.Show($"Could not open expense voucher preview:\n{ex.Message}", "Expense voucher", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private static string Read(BsonDocument document, string field, string fallback = "") =>
        DailyExpenseDomain.ReadString(document, field) ?? fallback;

    private static string FormatPayments(BsonDocument expense)
    {
        if (!expense.TryGetValue("payments", out var value) || !value.IsBsonArray || value.AsBsonArray.Count == 0)
            return $"Cash {MoneyMath.FormatRupee(DailyExpenseDomain.ReadDecimal(expense, "amount"))}";
        return string.Join(", ", value.AsBsonArray
            .OfType<BsonDocument>()
            .Where(payment => DailyExpenseDomain.ReadDecimal(payment, "amount") > 0)
            .Select(payment =>
                $"{Read(payment, "mode", Read(payment, "provider", "Other"))} " +
                MoneyMath.FormatRupee(DailyExpenseDomain.ReadDecimal(payment, "amount")) +
                (string.IsNullOrWhiteSpace(Read(payment, "reference")) ? "" : $" ({Read(payment, "reference")})")));
    }
}
