using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Products;
using RRBridal.StoreBilling.App.Views;

namespace RRBridal.StoreBilling.App.ViewModels;

public enum ReturnMode
{
    CreditNote,
    CashRefund,
}

public partial class SaleReturnViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly AppServices _services;

    [ObservableProperty] private string _originalBillNo = "";
    [ObservableProperty] private bool _billLoaded;
    [ObservableProperty] private string _reason = "";
    [ObservableProperty] private ReturnMode _returnMode = ReturnMode.CreditNote;

    [ObservableProperty] private string _returnNo = "";
    [ObservableProperty] private bool _isInterState;

    [ObservableProperty] private string _subTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _cgstTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _sgstTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _igstTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _returnTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _replacementTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _amountToCollectFormatted = "₹ 0.00";
    [ObservableProperty] private string _creditBalanceFormatted = "₹ 0.00";

    public ObservableCollection<SaleReturnLineItem> ReturnLines { get; } = new();
    public ObservableCollection<SaleExchangeLineItem> ExchangeLines { get; } = new();

    private BsonDocument? _originalBillDoc;

    public SaleReturnViewModel(AppServices services)
    {
        _services = services;
        AssignReturnNo();
        ExchangeLines.CollectionChanged += (_, e) =>
        {
            if (e.OldItems != null)
            {
                foreach (SaleExchangeLineItem line in e.OldItems)
                    line.PropertyChanged -= OnExchangeLinePropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (SaleExchangeLineItem line in e.NewItems)
                    line.PropertyChanged += OnExchangeLinePropertyChanged;
            }

            RecalculateTotals();
        };
    }

    private void AssignReturnNo()
    {
        ReturnNo = $"RET-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(100, 999)}";
    }

    [RelayCommand]
    private async Task LookupBill()
    {
        var billNo = (OriginalBillNo ?? "").Trim();
        if (string.IsNullOrEmpty(billNo))
        {
            MessageBox.Show("Enter a bill number to look up.", "Sale Return", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var coll = _services.LocalDb.GetCollection<BsonDocument>("store_bills");
        var doc = await coll.Find(new BsonDocument("billNo", billNo)).FirstOrDefaultAsync();
        if (doc == null)
        {
            MessageBox.Show($"Bill '{billNo}' not found.", "Sale Return", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _originalBillDoc = doc;
        IsInterState = doc.Contains("isInterState") && doc["isInterState"].AsBoolean;
        ReturnLines.Clear();

        if (doc.Contains("lines") && doc["lines"].IsBsonArray)
        {
            foreach (BsonDocument lineBson in doc["lines"].AsBsonArray.OfType<BsonDocument>())
            {
                var item = new SaleReturnLineItem
                {
                    LineNo = lineBson.GetValue("lineNo", 0).ToInt32(),
                    ProductCode = lineBson.GetValue("sku", "").AsString,
                    Description = lineBson.GetValue("description", "").AsString,
                    OriginalQty = (decimal)lineBson.GetValue("qty", 0).ToDouble(),
                    Rate = (decimal)lineBson.GetValue("rate", 0).ToDouble(),
                    TaxPercent = (decimal)lineBson.GetValue("taxPercent", 0).ToDouble(),
                    IsIgst = IsInterState,
                };
                item.PropertyChanged += OnReturnLinePropertyChanged;
                ReturnLines.Add(item);
            }
        }

        BillLoaded = true;
        RecalculateTotals();
    }

    private void OnReturnLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SaleReturnLineItem.ReturnQty)
            or nameof(SaleReturnLineItem.IsSelected)
            or nameof(SaleReturnLineItem.ReturnAmount)
            or nameof(SaleReturnLineItem.CgstAmount)
            or nameof(SaleReturnLineItem.SgstAmount)
            or nameof(SaleReturnLineItem.IgstAmount)
            or nameof(SaleReturnLineItem.TaxAmount))
        {
            RecalculateTotals();
        }
    }

    private void OnExchangeLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SaleExchangeLineItem.Qty)
            or nameof(SaleExchangeLineItem.Amount)
            or nameof(SaleExchangeLineItem.CgstAmount)
            or nameof(SaleExchangeLineItem.SgstAmount)
            or nameof(SaleExchangeLineItem.IgstAmount)
            or nameof(SaleExchangeLineItem.TaxAmount)
            or nameof(SaleExchangeLineItem.Total))
        {
            RecalculateTotals();
        }
    }

    private void RecalculateTotals()
    {
        var selected = ReturnLines.Where(l => l.IsSelected && l.ReturnQty > 0).ToList();
        var sub = selected.Sum(l => l.ReturnAmount);
        var cgst = selected.Sum(l => l.CgstAmount);
        var sgst = selected.Sum(l => l.SgstAmount);
        var igst = selected.Sum(l => l.IgstAmount);
        var total = sub + cgst + sgst + igst;

        SubTotalFormatted = FormatRupee(sub);
        CgstTotalFormatted = FormatRupee(cgst);
        SgstTotalFormatted = FormatRupee(sgst);
        IgstTotalFormatted = FormatRupee(igst);
        ReturnTotalFormatted = FormatRupee(total);

        var replacementTotal = ExchangeLines.Sum(l => l.Total);
        var amountToCollect = Math.Max(0, replacementTotal - total);
        var creditBalance = Math.Max(0, total - replacementTotal);
        ReplacementTotalFormatted = FormatRupee(replacementTotal);
        AmountToCollectFormatted = FormatRupee(amountToCollect);
        CreditBalanceFormatted = FormatRupee(creditBalance);
    }

    public async Task AddExchangeProductFromSearchAsync(string query)
    {
        var q = (query ?? "").Trim();
        if (!BillLoaded)
        {
            MessageBox.Show("Load the original bill before adding an exchange product.", "Sale Exchange", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (q.Length < 1)
        {
            MessageBox.Show("Enter a SKU, barcode, or product name to add a replacement product.", "Sale Exchange", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var items = await _services.ProductCatalog.SearchAsync(q);
        if (items.Count == 1)
        {
            AddExchangeLineFromCatalog(items[0]);
            return;
        }

        var existing = await _services.ProductCatalog.FindBySkuOrBarcodeAsync(q);
        if (existing != null && existing.StockQty > 0)
        {
            AddExchangeLineFromCatalog(existing);
            return;
        }

        if (existing != null && existing.StockQty <= 0)
        {
            MessageBox.Show($"Product \"{existing.Name}\" (SKU: {existing.Sku}) is out of local stock.", "Sale Exchange", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new ProductSearchDialog(q, _services) { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() == true && dlg.SelectedProduct != null)
            AddExchangeLineFromCatalog(dlg.SelectedProduct);
    }

    private void AddExchangeLineFromCatalog(CatalogProduct product)
    {
        if (product.StockQty <= 0)
        {
            MessageBox.Show($"Product \"{product.Name}\" (SKU: {product.Sku}) is out of local stock.", "Sale Exchange", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var existing = ExchangeLines.FirstOrDefault(l => string.Equals(l.ProductCode, product.Sku, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            if (existing.Qty + 1 > existing.AvailableQty)
            {
                MessageBox.Show($"Only {existing.AvailableQty:N2} available locally for SKU {existing.ProductCode}.", "Sale Exchange", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            existing.Qty += 1;
            return;
        }

        ExchangeLines.Add(new SaleExchangeLineItem
        {
            CentralProductId = product.CentralId,
            ProductCode = product.Sku,
            Description = product.Name,
            AvailableQty = product.StockQty,
            Qty = 1,
            Rate = product.SuggestedRate,
            Mrp = product.Mrp ?? 0m,
            TaxPercent = product.SuggestedTaxPercent,
            IsIgst = IsInterState,
        });
    }

    [RelayCommand]
    private void RemoveExchangeLine(SaleExchangeLineItem? line)
    {
        if (line != null)
            ExchangeLines.Remove(line);
    }

    [RelayCommand]
    private async Task PostReturn()
    {
        var selected = ReturnLines.Where(l => l.IsSelected && l.ReturnQty > 0).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Select at least one item with a return quantity.", "Sale Return", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var storeId = _services.StoreContext.StoreId;
            var deviceId = _services.StoreContext.DeviceId;
            var createdAt = DateTime.UtcNow.ToString("O");
            var eventId = Guid.NewGuid().ToString();

            var linesArr = new BsonArray();
            foreach (var l in selected)
            {
                linesArr.Add(new BsonDocument
                {
                    { "lineNo", l.LineNo },
                    { "sku", l.ProductCode },
                    { "description", l.Description },
                    { "returnQty", (double)l.ReturnQty },
                    { "rate", (double)l.Rate },
                    { "amount", (double)l.ReturnAmount },
                    { "taxPercent", (double)l.TaxPercent },
                    { "cgstAmt", (double)l.CgstAmount },
                    { "sgstAmt", (double)l.SgstAmount },
                    { "igstAmt", (double)l.IgstAmount },
                });
            }

            var sub = selected.Sum(l => l.ReturnAmount);
            var cgst = selected.Sum(l => l.CgstAmount);
            var sgst = selected.Sum(l => l.SgstAmount);
            var igst = selected.Sum(l => l.IgstAmount);
            var total = sub + cgst + sgst + igst;
            var exchangeLines = ExchangeLines.Where(l => l.Qty > 0).ToList();
            var exchangeLinesArr = new BsonArray();
            foreach (var l in exchangeLines)
            {
                exchangeLinesArr.Add(new BsonDocument
                {
                    { "centralProductId", l.CentralProductId ?? "" },
                    { "sku", l.ProductCode },
                    { "description", l.Description },
                    { "qty", (double)l.Qty },
                    { "rate", (double)l.Rate },
                    { "amount", (double)l.Amount },
                    { "mrp", (double)l.Mrp },
                    { "taxPercent", (double)l.TaxPercent },
                    { "cgstAmt", (double)l.CgstAmount },
                    { "sgstAmt", (double)l.SgstAmount },
                    { "igstAmt", (double)l.IgstAmount },
                    { "taxAmt", (double)l.TaxAmount },
                    { "lineTotal", (double)l.Total },
                });
            }

            var replacementTotal = exchangeLines.Sum(l => l.Total);
            var amountToCollect = Math.Max(0, replacementTotal - total);
            var creditBalance = Math.Max(0, total - replacementTotal);
            PaymentOutcome? paymentOutcome = null;
            if (amountToCollect > 0)
            {
                var paymentVm = new PaymentDialogViewModel(_services.PaymentRouter, ReturnNo, amountToCollect);
                var paymentDlg = new PaymentDialog(paymentVm) { Owner = Application.Current.MainWindow };
                var paymentResult = paymentDlg.ShowDialog();
                if (paymentResult != true || !paymentVm.Outcome.Confirmed)
                    return;
                paymentOutcome = paymentVm.Outcome;
            }

            var paymentsArr = new BsonArray();
            if (paymentOutcome != null)
            {
                foreach (var leg in paymentOutcome.Legs)
                {
                    paymentsArr.Add(new BsonDocument
                    {
                        { "provider", leg.Provider.ToString() },
                        { "amount", (double)leg.Amount },
                        { "reference", leg.Reference },
                        { "status", leg.Status },
                    });
                }
            }

            var returnDoc = new BsonDocument
            {
                { "returnNo", ReturnNo },
                { "originalBillNo", OriginalBillNo.Trim() },
                { "storeId", storeId },
                { "transactionType", exchangeLines.Count > 0 ? "exchange" : "return" },
                { "returnMode", ReturnMode == ReturnMode.CreditNote ? "credit_note" : "cash_refund" },
                { "reason", Reason },
                { "isInterState", IsInterState },
                { "lines", linesArr },
                { "returnLines", linesArr },
                { "exchangeLines", exchangeLinesArr },
                { "subTotal", (double)sub },
                { "cgstTotal", (double)cgst },
                { "sgstTotal", (double)sgst },
                { "igstTotal", (double)igst },
                { "returnTotal", (double)total },
                { "replacementTotal", (double)replacementTotal },
                { "amountCollected", (double)amountToCollect },
                { "creditBalance", (double)creditBalance },
                { "payments", paymentsArr },
                { "status", "posted" },
                { "createdAtUtc", createdAt },
            };

            var returnsColl = _services.LocalDb.GetCollection<BsonDocument>("store_sale_returns");
            await returnsColl.InsertOneAsync(returnDoc);

            var payload = new BsonDocument
            {
                { "returnNo", ReturnNo },
                { "originalBillNo", OriginalBillNo.Trim() },
                { "transactionType", exchangeLines.Count > 0 ? "exchange" : "return" },
                { "returnMode", ReturnMode == ReturnMode.CreditNote ? "credit_note" : "cash_refund" },
                { "reason", Reason },
                { "isInterState", IsInterState },
                { "lines", linesArr },
                { "returnLines", linesArr },
                { "exchangeLines", exchangeLinesArr },
                { "subTotal", (double)sub },
                { "cgstTotal", (double)cgst },
                { "sgstTotal", (double)sgst },
                { "igstTotal", (double)igst },
                { "returnTotal", (double)total },
                { "replacementTotal", (double)replacementTotal },
                { "amountCollected", (double)amountToCollect },
                { "creditBalance", (double)creditBalance },
                { "payments", paymentsArr },
            };

            var hash = JsonSerializer.Serialize(new
            {
                returnNo = ReturnNo,
                originalBillNo = OriginalBillNo.Trim(),
                returnMode = ReturnMode == ReturnMode.CreditNote ? "credit_note" : "cash_refund",
                lines = selected.Select(l => new { sku = l.ProductCode, returnQty = l.ReturnQty, rate = l.Rate }),
                exchangeLines = exchangeLines.Select(l => new { sku = l.ProductCode, qty = l.Qty, rate = l.Rate }),
                total,
                replacementTotal,
                amountToCollect,
                creditBalance,
            });

            var outboxEvent = new BsonDocument
            {
                { "eventId", eventId },
                { "storeId", storeId },
                { "deviceId", deviceId },
                { "type", exchangeLines.Count > 0 ? "SaleExchangeCreated" : "SaleReturnCreated" },
                { "createdAt", createdAt },
                { "payload", payload },
                { "hash", hash },
                { "status", "pending" },
            };

            var outbox = _services.LocalDb.GetCollection<BsonDocument>("outbox_events");
            await outbox.InsertOneAsync(outboxEvent);

            foreach (var line in selected.Where(l => !string.IsNullOrWhiteSpace(l.ProductCode)))
                await _services.ProductCatalog.IncrementStockBySkuAsync(line.ProductCode, line.ReturnQty, line.Description);

            foreach (var line in exchangeLines.Where(l => !string.IsNullOrWhiteSpace(l.ProductCode)))
                await _services.ProductCatalog.DecrementStockBySkuAsync(line.ProductCode, line.Qty);

            if (exchangeLines.Count > 0)
                ShowExchangeReceipt(selected, exchangeLines, total, replacementTotal, amountToCollect, creditBalance);

            MessageBox.Show(
                exchangeLines.Count > 0
                    ? $"Exchange {ReturnNo} posted."
                    : $"Return {ReturnNo} posted. Mode: {(ReturnMode == ReturnMode.CreditNote ? "Credit Note" : "Cash Refund")}.",
                "Sale Return", MessageBoxButton.OK, MessageBoxImage.Information);

            ClearForm();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not post return: {ex.Message}", "Sale Return", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ClearForm()
    {
        foreach (var line in ReturnLines)
            line.PropertyChanged -= OnReturnLinePropertyChanged;
        foreach (var line in ExchangeLines)
            line.PropertyChanged -= OnExchangeLinePropertyChanged;

        OriginalBillNo = "";
        Reason = "";
        ReturnMode = ReturnMode.CreditNote;
        IsInterState = false;
        ReturnLines.Clear();
        ExchangeLines.Clear();
        BillLoaded = false;
        _originalBillDoc = null;
        AssignReturnNo();
        RecalculateTotals();
    }

    private void ShowExchangeReceipt(
        System.Collections.Generic.IReadOnlyList<SaleReturnLineItem> returnLines,
        System.Collections.Generic.IReadOnlyList<SaleExchangeLineItem> exchangeLines,
        decimal returnTotal,
        decimal replacementTotal,
        decimal amountCollected,
        decimal creditBalance)
    {
        try
        {
            _services.ReceiptConfig.Reload();
            var text = BuildExchangeReceiptText(returnLines, exchangeLines, returnTotal, replacementTotal, amountCollected, creditBalance);
            var dlg = new InvoicePrintPreviewWindow(_services, text, printInvoiceEnabled: true)
            {
                Owner = Application.Current.MainWindow,
            };
            dlg.ShowDialog();
        }
        catch { }
    }

    private string BuildExchangeReceiptText(
        System.Collections.Generic.IReadOnlyList<SaleReturnLineItem> returnLines,
        System.Collections.Generic.IReadOnlyList<SaleExchangeLineItem> exchangeLines,
        decimal returnTotal,
        decimal replacementTotal,
        decimal amountCollected,
        decimal creditBalance)
    {
        var store = _services.ReceiptConfig.Current.Store;
        var sb = new StringBuilder();
        sb.AppendLine(store.StoreName);
        if (!string.IsNullOrWhiteSpace(store.Address)) sb.AppendLine(store.Address);
        if (!string.IsNullOrWhiteSpace(store.Gstin)) sb.AppendLine($"GSTIN: {store.Gstin}");
        sb.AppendLine("------------------------------------------");
        sb.AppendLine("SALE EXCHANGE RECEIPT");
        sb.AppendLine($"Return No: {ReturnNo}");
        sb.AppendLine($"Original Bill: {OriginalBillNo.Trim()}");
        sb.AppendLine($"Date: {DateTime.Now:dd-MMM-yyyy HH:mm}");
        sb.AppendLine("------------------------------------------");
        sb.AppendLine("OLD ITEM RETURNED");
        foreach (var line in returnLines)
            sb.AppendLine($"{line.ProductCode} {line.ReturnQty:0.###} x {line.Rate:0.00} = {line.ReturnAmount + line.TaxAmount:0.00}");
        sb.AppendLine("------------------------------------------");
        sb.AppendLine("NEW ITEM ISSUED");
        foreach (var line in exchangeLines)
            sb.AppendLine($"{line.ProductCode} {line.Qty:0.###} x {line.Rate:0.00} = {line.Total:0.00}");
        sb.AppendLine("------------------------------------------");
        sb.AppendLine($"Old return value : {returnTotal:0.00}");
        sb.AppendLine($"New product value: {replacementTotal:0.00}");
        sb.AppendLine($"Amount collected : {amountCollected:0.00}");
        sb.AppendLine($"Credit balance   : {creditBalance:0.00}");
        sb.AppendLine("------------------------------------------");
        sb.AppendLine("Thank you");
        return sb.ToString();
    }

    private static string FormatRupee(decimal value) => "₹ " + value.ToString("N2", InCulture);
}
