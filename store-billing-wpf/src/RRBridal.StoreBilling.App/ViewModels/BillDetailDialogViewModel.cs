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
    public string Hsn { get; init; } = "";
    public decimal Qty { get; init; }
    public decimal Rate { get; init; }
    public decimal Amount { get; init; }
    public decimal AlterationAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal CashDiscountAmount { get; init; }
    public decimal SchemeDiscountAmount { get; init; }
    public decimal OriginalTaxAmount { get; init; }
    public decimal RevisedAmount { get; init; }
    public decimal RevisedTaxAmount { get; init; }
    public decimal Mrp { get; init; }
    public decimal TaxPercent { get; init; }
    public decimal CgstPercent { get; init; }
    public decimal SgstPercent { get; init; }
    public decimal IgstPercent { get; init; }
    public decimal TaxAmount { get; init; }
    public string LineTotalFormatted => MoneyMath.FormatRupee(Amount + TaxAmount);
}

public partial class BillDetailDialogViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly AppServices _services;
    private readonly StoreBillListService _billListService;
    private string _billNo = "";

    [ObservableProperty] private string _headerSummary = "";
    [ObservableProperty] private string _paymentSummary = "";
    [ObservableProperty] private string _returnSummary = "";
    [ObservableProperty] private string _adjustmentSummary = "";
    [ObservableProperty] private bool _hasReturn;
    [ObservableProperty] private bool _hasAdjustment;
    [ObservableProperty] private bool _hasCreditNoteReturn;
    [ObservableProperty] private string _returnCreditNoteNo = "";
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isNotFound;

    [ObservableProperty] private string _billDateDisplay = "";
    [ObservableProperty] private string _customerCode = "";
    [ObservableProperty] private string _customerPhone = "";
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _salesman = "";
    [ObservableProperty] private bool _holdBills;
    [ObservableProperty] private bool _doorDelivery;
    [ObservableProperty] private bool _onlineCodOrder;
    [ObservableProperty] private bool _stitching;
    [ObservableProperty] private bool _printInvoice;
    [ObservableProperty] private bool _isInterState;
    [ObservableProperty] private string _deliveryDateDisplay = "";
    [ObservableProperty] private string _counterDisplay = "";
    [ObservableProperty] private string _paymentMode = "";

    [ObservableProperty] private string _subTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _originalTaxTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _itemDiscountFormatted = "₹ 0.00";
    [ObservableProperty] private string _cashDiscAmountFormatted = "₹ 0.00";
    [ObservableProperty] private string _schemeDiscountFormatted = "₹ 0.00";
    [ObservableProperty] private string _revisedSubTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _cgstTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _sgstTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _igstTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _taxTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _grossTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _roundOffFormatted = "₹ 0.00";
    [ObservableProperty] private string _payableTotalFormatted = "₹ 0.00";
    [ObservableProperty] private string _totalLineQtyFormatted = "0";

    [ObservableProperty] private string _cashPaymentFormatted = "₹ 0.00";
    [ObservableProperty] private string _cardPaymentFormatted = "₹ 0.00";
    [ObservableProperty] private string _upiPaymentFormatted = "₹ 0.00";
    [ObservableProperty] private string _creditNotePaymentFormatted = "₹ 0.00";

    [ObservableProperty] private bool _hasAppliedCredit;
    [ObservableProperty] private string _selectedCreditNoteLabel = "";
    [ObservableProperty] private string _creditAppliedFormatted = "₹ 0.00";
    [ObservableProperty] private string _payableBeforeCreditFormatted = "₹ 0.00";

    public string LoadedBillNo => _billNo;

    public ObservableCollection<BillDetailLineRow> Lines { get; } = new();

    public Action<string>? NavigateToReturnForBill { get; set; }
    public Action<string>? NavigateToAdjustmentForBill { get; set; }
    public Action? RequestClose { get; set; }

    public BillDetailDialogViewModel(AppServices services, StoreBillListService billListService)
    {
        _services = services;
        _billListService = billListService;
    }

    public void Clear()
    {
        _billNo = "";
        OnPropertyChanged(nameof(LoadedBillNo));
        Lines.Clear();
        HasReturn = false;
        HasAdjustment = false;
        HasCreditNoteReturn = false;
        ReturnCreditNoteNo = "";
        IsLoaded = false;
        IsNotFound = false;
        HasAppliedCredit = false;
        HeaderSummary = "";
        PaymentSummary = "";
        ReturnSummary = "";
        AdjustmentSummary = "";
        ResetDisplayFields();
    }

    public async Task LoadAsync(string billNo)
    {
        _billNo = billNo.Trim();
        OnPropertyChanged(nameof(LoadedBillNo));
        Lines.Clear();
        HasReturn = false;
        HasAdjustment = false;
        HasCreditNoteReturn = false;
        ReturnCreditNoteNo = "";
        IsLoaded = false;
        IsNotFound = false;
        HasAppliedCredit = false;
        ReturnSummary = "";
        AdjustmentSummary = "";
        ResetDisplayFields();

        var doc = await _services.BillDocuments.GetByBillNoAsync(_billNo);
        if (doc == null)
        {
            IsNotFound = true;
            HeaderSummary = $"Bill {_billNo} not found.";
            PaymentSummary = "";
            return;
        }

        IsLoaded = true;
        PopulateFromDocument(doc);

        var storeId = _services.StoreContext.StoreId;
        var returnDoc = await _billListService.GetReturnByBillNoAsync(storeId, _billNo);
        if (returnDoc != null)
        {
            HasReturn = true;
            var returnMode = DayBillingCloseDocumentReader.ReadString(returnDoc, "returnMode") ?? "";
            ReturnCreditNoteNo = DayBillingCloseDocumentReader.ReadString(returnDoc, "creditNoteNo") ?? "";
            HasCreditNoteReturn = string.Equals(returnMode, "credit_note", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(ReturnCreditNoteNo);
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

    private void ResetDisplayFields()
    {
        BillDateDisplay = "";
        CustomerCode = "";
        CustomerPhone = "";
        CustomerName = "";
        Salesman = "";
        HoldBills = false;
        DoorDelivery = false;
        OnlineCodOrder = false;
        Stitching = false;
        PrintInvoice = false;
        IsInterState = false;
        DeliveryDateDisplay = "";
        CounterDisplay = "";
        PaymentMode = "";
        SubTotalFormatted = "₹ 0.00";
        OriginalTaxTotalFormatted = "₹ 0.00";
        ItemDiscountFormatted = "₹ 0.00";
        CashDiscAmountFormatted = "₹ 0.00";
        SchemeDiscountFormatted = "₹ 0.00";
        RevisedSubTotalFormatted = "₹ 0.00";
        CgstTotalFormatted = "₹ 0.00";
        SgstTotalFormatted = "₹ 0.00";
        IgstTotalFormatted = "₹ 0.00";
        TaxTotalFormatted = "₹ 0.00";
        GrossTotalFormatted = "₹ 0.00";
        RoundOffFormatted = "₹ 0.00";
        PayableTotalFormatted = "₹ 0.00";
        TotalLineQtyFormatted = "0";
        CashPaymentFormatted = "₹ 0.00";
        CardPaymentFormatted = "₹ 0.00";
        UpiPaymentFormatted = "₹ 0.00";
        CreditNotePaymentFormatted = "₹ 0.00";
        SelectedCreditNoteLabel = "";
        CreditAppliedFormatted = "₹ 0.00";
        PayableBeforeCreditFormatted = "₹ 0.00";
    }

    private void PopulateFromDocument(BsonDocument doc)
    {
        var payments = DayBillingCloseDocumentReader.SumBillPayments(doc);
        var payable = DayBillingCloseDocumentReader.ReadDecimal(doc, "payable");
        var pos = DayBillingCloseDocumentReader.ReadString(doc, "posCounter") ?? "";
        var dev = DayBillingCloseDocumentReader.ReadString(doc, "deviceId") ?? "";
        PaymentMode = DayBillingCloseDocumentReader.ReadString(doc, "paymentMode") ?? "";

        BillDateDisplay = DayBillingCloseDocumentReader.ReadString(doc, "billDate") ?? "—";
        CustomerCode = DayBillingCloseDocumentReader.ReadString(doc, "customerCode") ?? "";
        CustomerName = DayBillingCloseDocumentReader.ReadString(doc, "customerName") ?? "";
        CustomerPhone = DayBillingCloseDocumentReader.ReadString(doc, "customerPhone") ?? "";
        Salesman = DayBillingCloseDocumentReader.ReadString(doc, "salesman") ?? "";
        HoldBills = doc.GetValue("holdBills", false).ToBoolean();
        DoorDelivery = doc.GetValue("doorDelivery", false).ToBoolean();
        Stitching = doc.GetValue("stitching", false).ToBoolean();
        PrintInvoice = doc.GetValue("printInvoice", false).ToBoolean();
        IsInterState = doc.GetValue("isInterState", false).ToBoolean();
        DeliveryDateDisplay = DayBillingCloseDocumentReader.ReadString(doc, "deliveryDate") ?? "";
        CounterDisplay = CounterDisplayFormatter.Format(pos, dev);

        var salesChannel = DayBillingCloseDocumentReader.ReadString(doc, "salesChannel") ?? "store";
        OnlineCodOrder = string.Equals(salesChannel, OnlineCodDocumentReader.SalesChannelOnline, StringComparison.OrdinalIgnoreCase)
            || string.Equals(PaymentMode, "OnlineCod", StringComparison.OrdinalIgnoreCase);

        HeaderSummary =
            $"Bill: {_billNo}\n" +
            $"Date: {BillDateDisplay}\n" +
            $"Customer: {CustomerName}\n" +
            $"Phone: {CustomerPhone}\n" +
            $"Counter: {CounterDisplay}\n" +
            $"Payable: {MoneyMath.FormatRupee(payable)}\n" +
            $"Payment mode: {PaymentMode}";

        CashPaymentFormatted = MoneyMath.FormatRupee(payments.Cash);
        CardPaymentFormatted = MoneyMath.FormatRupee(payments.Card);
        UpiPaymentFormatted = MoneyMath.FormatRupee(payments.Upi);
        CreditNotePaymentFormatted = MoneyMath.FormatRupee(payments.CreditNote);

        PaymentSummary =
            $"Mode: {PaymentMode}\n" +
            $"Cash: {CashPaymentFormatted}\n" +
            $"Card: {CardPaymentFormatted}\n" +
            $"UPI: {UpiPaymentFormatted}\n" +
            $"Credit note: {CreditNotePaymentFormatted}";

        var schemeLine = DayBillingCloseDocumentReader.ReadDecimal(doc, "schemeLineDiscount");
        var schemeBill = DayBillingCloseDocumentReader.ReadDecimal(doc, "schemeBillDiscount");
        SubTotalFormatted = MoneyMath.FormatRupee(DayBillingCloseDocumentReader.ReadDecimal(doc, "subTotal"));
        OriginalTaxTotalFormatted = MoneyMath.FormatRupee(DayBillingCloseDocumentReader.ReadDecimal(doc, "originalTaxTotal"));
        ItemDiscountFormatted = MoneyMath.FormatRupee(DayBillingCloseDocumentReader.ReadDecimal(doc, "itemDiscount"));
        CashDiscAmountFormatted = MoneyMath.FormatRupee(DayBillingCloseDocumentReader.ReadDecimal(doc, "cashDiscAmount"));
        SchemeDiscountFormatted = MoneyMath.FormatRupee(schemeLine + schemeBill);
        RevisedSubTotalFormatted = MoneyMath.FormatRupee(DayBillingCloseDocumentReader.ReadDecimal(doc, "revisedSubTotal"));
        CgstTotalFormatted = MoneyMath.FormatRupee(DayBillingCloseDocumentReader.ReadDecimal(doc, "cgstTotal"));
        SgstTotalFormatted = MoneyMath.FormatRupee(DayBillingCloseDocumentReader.ReadDecimal(doc, "sgstTotal"));
        IgstTotalFormatted = MoneyMath.FormatRupee(DayBillingCloseDocumentReader.ReadDecimal(doc, "igstTotal"));
        TaxTotalFormatted = MoneyMath.FormatRupee(DayBillingCloseDocumentReader.ReadDecimal(doc, "taxTotal"));
        GrossTotalFormatted = MoneyMath.FormatRupee(DayBillingCloseDocumentReader.ReadDecimal(doc, "originalInclusiveTotal"));
        RoundOffFormatted = MoneyMath.FormatRupee(DayBillingCloseDocumentReader.ReadDecimal(doc, "roundOff"));
        PayableTotalFormatted = MoneyMath.FormatPayable(payable);

        var creditApplied = DayBillingCloseDocumentReader.ReadDecimal(doc, "creditApplied");
        var creditNoteNo = DayBillingCloseDocumentReader.ReadString(doc, "creditNoteNo") ?? "";
        if (creditApplied > 0 || !string.IsNullOrEmpty(creditNoteNo))
        {
            HasAppliedCredit = true;
            SelectedCreditNoteLabel = creditNoteNo;
            CreditAppliedFormatted = MoneyMath.FormatRupee(creditApplied);
            PayableBeforeCreditFormatted = MoneyMath.FormatRupee(DayBillingCloseDocumentReader.ReadDecimal(doc, "payableBeforeCredit"));
        }

        decimal totalQty = 0;
        if (doc.TryGetValue("lines", out var linesVal) && linesVal.IsBsonArray)
        {
            foreach (BsonDocument line in linesVal.AsBsonArray.OfType<BsonDocument>())
            {
                var amount = DayBillingCloseDocumentReader.ReadDecimal(line, "amount");
                var cgst = DayBillingCloseDocumentReader.ReadDecimal(line, "cgstAmount");
                var sgst = DayBillingCloseDocumentReader.ReadDecimal(line, "sgstAmount");
                var igst = DayBillingCloseDocumentReader.ReadDecimal(line, "igstAmount");
                var qty = DayBillingCloseDocumentReader.ReadDecimal(line, "qty");
                totalQty += qty;
                Lines.Add(new BillDetailLineRow
                {
                    LineNo = line.GetValue("lineNo", 0).ToInt32(),
                    Sku = DayBillingCloseDocumentReader.ReadString(line, "sku") ?? "",
                    Description = DayBillingCloseDocumentReader.ReadString(line, "description") ?? "",
                    Hsn = DayBillingCloseDocumentReader.ReadString(line, "hsn") ?? "",
                    Qty = qty,
                    Rate = DayBillingCloseDocumentReader.ReadDecimal(line, "rate"),
                    Amount = amount,
                    AlterationAmount = DayBillingCloseDocumentReader.ReadDecimal(line, "alterationAmount"),
                    DiscountAmount = DayBillingCloseDocumentReader.ReadDecimal(line, "discountAmount"),
                    CashDiscountAmount = DayBillingCloseDocumentReader.ReadDecimal(line, "cashDiscountAmount"),
                    SchemeDiscountAmount = DayBillingCloseDocumentReader.ReadDecimal(line, "schemeDiscountAmount"),
                    OriginalTaxAmount = DayBillingCloseDocumentReader.ReadDecimal(line, "originalTaxAmount"),
                    RevisedAmount = DayBillingCloseDocumentReader.ReadDecimal(line, "revisedAmount"),
                    RevisedTaxAmount = DayBillingCloseDocumentReader.ReadDecimal(line, "revisedTaxAmount"),
                    Mrp = DayBillingCloseDocumentReader.ReadDecimal(line, "mrp"),
                    TaxPercent = DayBillingCloseDocumentReader.ReadDecimal(line, "taxPercent"),
                    CgstPercent = DayBillingCloseDocumentReader.ReadDecimal(line, "cgstPercent"),
                    SgstPercent = DayBillingCloseDocumentReader.ReadDecimal(line, "sgstPercent"),
                    IgstPercent = DayBillingCloseDocumentReader.ReadDecimal(line, "igstPercent"),
                    TaxAmount = DayBillingCloseDocumentReader.ReadDecimal(line, "taxAmount"),
                });
            }
        }

        TotalLineQtyFormatted = totalQty.ToString("0.###", InCulture);
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
