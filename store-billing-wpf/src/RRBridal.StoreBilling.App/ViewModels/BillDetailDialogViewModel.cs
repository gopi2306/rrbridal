using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Store;

namespace RRBridal.StoreBilling.App.ViewModels;

public sealed class BillDetailLineRow
{
    public int LineNo { get; init; }
    public string Sku { get; init; } = "";
    public string Description { get; init; } = "";
    public decimal Qty { get; init; }
    public decimal Rate { get; init; }
    public decimal Amount { get; init; }
    public decimal TaxAmount { get; init; }
    public string LineTotalFormatted => MoneyMath.FormatRupee(Amount + TaxAmount);
}

public partial class BillDetailDialogViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly StoreBillListService _billListService;
    private string _billNo = "";

    [ObservableProperty] private string _headerSummary = "";
    [ObservableProperty] private string _paymentSummary = "";
    [ObservableProperty] private string _returnSummary = "";
    [ObservableProperty] private string _adjustmentSummary = "";
    [ObservableProperty] private bool _hasReturn;
    [ObservableProperty] private bool _hasAdjustment;

    public ObservableCollection<BillDetailLineRow> Lines { get; } = new();

    public Action<string>? NavigateToReturnForBill { get; set; }
    public Action<string>? NavigateToAdjustmentForBill { get; set; }
    public Action? RequestClose { get; set; }

    public BillDetailDialogViewModel(AppServices services, StoreBillListService billListService)
    {
        _services = services;
        _billListService = billListService;
    }

    public async Task LoadAsync(string billNo)
    {
        _billNo = billNo.Trim();
        Lines.Clear();
        HasReturn = false;
        HasAdjustment = false;
        ReturnSummary = "";
        AdjustmentSummary = "";

        var doc = await _services.BillDocuments.GetByBillNoAsync(_billNo);
        if (doc == null)
        {
            HeaderSummary = $"Bill {_billNo} not found.";
            PaymentSummary = "";
            return;
        }

        var payments = DayBillingCloseDocumentReader.SumBillPayments(doc);
        var payable = DayBillingCloseDocumentReader.ReadDecimal(doc, "payable");
        var pos = DayBillingCloseDocumentReader.ReadString(doc, "posCounter") ?? "";
        var dev = DayBillingCloseDocumentReader.ReadString(doc, "deviceId") ?? "";
        var paymentMode = DayBillingCloseDocumentReader.ReadString(doc, "paymentMode") ?? "";

        HeaderSummary =
            $"Bill: {_billNo}\n" +
            $"Date: {DayBillingCloseDocumentReader.ReadString(doc, "billDate") ?? "—"}\n" +
            $"Customer: {DayBillingCloseDocumentReader.ReadString(doc, "customerName") ?? "—"}\n" +
            $"Phone: {DayBillingCloseDocumentReader.ReadString(doc, "customerPhone") ?? "—"}\n" +
            $"Counter: {CounterDisplayFormatter.Format(pos, dev)}\n" +
            $"Payable: {MoneyMath.FormatRupee(payable)}\n" +
            $"Payment mode: {paymentMode}";

        PaymentSummary =
            $"Cash: {MoneyMath.FormatRupee(payments.Cash)}\n" +
            $"Card: {MoneyMath.FormatRupee(payments.Card)}\n" +
            $"UPI: {MoneyMath.FormatRupee(payments.Upi)}\n" +
            $"Credit note: {MoneyMath.FormatRupee(payments.CreditNote)}";

        if (doc.TryGetValue("lines", out var linesVal) && linesVal.IsBsonArray)
        {
            foreach (BsonDocument line in linesVal.AsBsonArray.OfType<BsonDocument>())
            {
                var amount = DayBillingCloseDocumentReader.ReadDecimal(line, "amount");
                var cgst = DayBillingCloseDocumentReader.ReadDecimal(line, "cgstAmount");
                var sgst = DayBillingCloseDocumentReader.ReadDecimal(line, "sgstAmount");
                var igst = DayBillingCloseDocumentReader.ReadDecimal(line, "igstAmount");
                Lines.Add(new BillDetailLineRow
                {
                    LineNo = line.GetValue("lineNo", 0).ToInt32(),
                    Sku = DayBillingCloseDocumentReader.ReadString(line, "sku") ?? "",
                    Description = DayBillingCloseDocumentReader.ReadString(line, "description") ?? "",
                    Qty = DayBillingCloseDocumentReader.ReadDecimal(line, "qty"),
                    Rate = DayBillingCloseDocumentReader.ReadDecimal(line, "rate"),
                    Amount = amount,
                    TaxAmount = cgst + sgst + igst,
                });
            }
        }

        var storeId = _services.StoreContext.StoreId;
        var returnDoc = await _billListService.GetReturnByBillNoAsync(storeId, _billNo);
        if (returnDoc != null)
        {
            HasReturn = true;
            ReturnSummary =
                $"Return no: {DayBillingCloseDocumentReader.ReadString(returnDoc, "returnNo") ?? "—"}\n" +
                $"Mode: {DayBillingCloseDocumentReader.ReadString(returnDoc, "returnMode") ?? "—"}\n" +
                $"Total: {MoneyMath.FormatRupee(DayBillingCloseDocumentReader.ReadDecimal(returnDoc, "returnTotal"))}\n" +
                $"Reason: {DayBillingCloseDocumentReader.ReadString(returnDoc, "reason") ?? "—"}";
        }

        var adjDoc = await _billListService.GetAdjustmentByBillNoAsync(storeId, _billNo);
        if (adjDoc != null)
        {
            HasAdjustment = true;
            AdjustmentSummary =
                $"Adjustment no: {DayBillingCloseDocumentReader.ReadString(adjDoc, "adjustmentNo") ?? "—"}\n" +
                $"Diff payable: {MoneyMath.FormatRupee(DayBillingCloseDocumentReader.ReadDecimal(adjDoc, "diffPayable"))}\n" +
                $"Reason: {DayBillingCloseDocumentReader.ReadString(adjDoc, "reason") ?? "—"}";
        }
    }

    [RelayCommand]
    private void OpenReturn()
    {
        if (!HasReturn || string.IsNullOrWhiteSpace(_billNo))
            return;
        RequestClose?.Invoke();
        NavigateToReturnForBill?.Invoke(_billNo);
    }

    [RelayCommand]
    private void OpenAdjustment()
    {
        if (!HasAdjustment || string.IsNullOrWhiteSpace(_billNo))
            return;
        RequestClose?.Invoke();
        NavigateToAdjustmentForBill?.Invoke(_billNo);
    }

}
