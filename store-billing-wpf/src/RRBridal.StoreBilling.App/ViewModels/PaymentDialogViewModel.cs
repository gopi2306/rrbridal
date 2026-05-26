using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Models;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Payments;

namespace RRBridal.StoreBilling.App.ViewModels;

public enum PaymentMode
{
    Cash,
    Card,
    Upi,
    CreditNote,
    Split,
}

public enum PaymentStatus
{
    Idle,
    Processing,
    Success,
    Failed,
}

public sealed class PaymentOutcome
{
    public bool Confirmed { get; init; }
    public List<PaymentLegResult> Legs { get; init; } = new();
    public PaymentMode Mode { get; init; }
    public decimal? CashReceived { get; init; }
    public decimal? ChangeReturned { get; init; }
}

public sealed class PaymentLegResult
{
    public PaymentProviderKind Provider { get; init; }
    public decimal Amount { get; init; }
    public string Reference { get; init; } = "";
    public string Status { get; init; } = "";
}

public partial class PaymentDialogViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly IPaymentRouter _router;
    private readonly CustomerCreditNoteService? _creditNotes;
    private readonly string _storeId;
    private readonly string _invoiceNo;
    private readonly string? _billingReservedCreditNoteNo;
    private readonly decimal _billingReservedCreditAmount;
    private readonly bool _skipPaymentOutbox;

    public decimal InvoicePayableAmount { get; }
    public string PayableFormatted { get; }

    [ObservableProperty] private decimal _paymentCreditAmount;
    [ObservableProperty] private decimal _collectibleAmount;
    [ObservableProperty] private string _collectibleFormatted = "";
    [ObservableProperty] private string _paymentCreditAppliedFormatted = "";
    [ObservableProperty] private bool _hasPaymentCreditApplied;

    public ObservableCollection<CustomerCreditNoteOption> AvailableCreditNotes { get; } = new();

    private string? _selectedPaymentCreditNoteNo;

    [ObservableProperty] private PaymentMode _selectedMode = PaymentMode.Cash;
    [ObservableProperty] private bool _hasAvailableCredit;
    [ObservableProperty] private bool _hasBillingCreditReserved;
    [ObservableProperty] private string _billingCreditReservedBanner = "";
    [ObservableProperty] private string _creditNoteMaxFormatted = "";
    [ObservableProperty] private string _selectedPaymentCreditLabel = "";
    [ObservableProperty] private string _paymentOriginalCreditFormatted = "";
    [ObservableProperty] private string _paymentApplyingCreditFormatted = "";
    [ObservableProperty] private string _paymentRemainingAfterFormatted = "";
    [ObservableProperty] private bool _hasPaymentCreditSelected;

    [ObservableProperty] private decimal _amountReceived;
    [ObservableProperty] private string _changeDueFormatted = "₹ 0.00";
    [ObservableProperty] private bool _isCashShortfall;
    [ObservableProperty] private string _deviceStatusText = "Ready";
    [ObservableProperty] private string _creditNoteReference = "";
    [ObservableProperty] private decimal _creditNoteAmount;
    [ObservableProperty] private decimal _splitCashAmount;
    [ObservableProperty] private decimal _splitCardAmount;
    [ObservableProperty] private decimal _splitUpiAmount;
    [ObservableProperty] private decimal _splitCreditNoteAmount;
    [ObservableProperty] private string _splitCreditNoteReference = "";
    [ObservableProperty] private string _splitRemainingFormatted = "₹ 0.00";
    [ObservableProperty] private bool _isSplitBalanced;
    [ObservableProperty] private PaymentStatus _status = PaymentStatus.Idle;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _isProcessing;

    public Action<bool>? CloseDialog { get; set; }
    public PaymentOutcome Outcome { get; private set; } = new() { Confirmed = false };

    public PaymentDialogViewModel(IPaymentRouter router, string invoiceNo, decimal payableAmount)
        : this(router, null, "", invoiceNo, payableAmount, null, null, null, 0, false)
    {
    }

    public PaymentDialogViewModel(
        IPaymentRouter router,
        CustomerCreditNoteService? creditNotes,
        string storeId,
        string invoiceNo,
        decimal payableAmount,
        string? customerCode,
        string? customerPhone,
        string? billingReservedCreditNoteNo,
        decimal billingReservedCreditAmount,
        bool skipPaymentOutbox = false)
    {
        _router = router;
        _creditNotes = creditNotes;
        _storeId = storeId ?? "";
        _invoiceNo = invoiceNo;
        _billingReservedCreditNoteNo = billingReservedCreditNoteNo?.Trim();
        _billingReservedCreditAmount = billingReservedCreditAmount;
        _skipPaymentOutbox = skipPaymentOutbox;
        InvoicePayableAmount = payableAmount;
        PayableFormatted = FormatRupee(payableAmount);
        AmountReceived = payableAmount;
        CreditNoteAmount = payableAmount;

        HasBillingCreditReserved = billingReservedCreditAmount > 0
            && !string.IsNullOrEmpty(_billingReservedCreditNoteNo);
        if (HasBillingCreditReserved)
        {
            BillingCreditReservedBanner =
                $"Invoice credit {FormatRupee(billingReservedCreditAmount)} from {_billingReservedCreditNoteNo} already applied.";
        }

        _customerCode = customerCode?.Trim() ?? "";
        _customerPhone = customerPhone?.Trim() ?? "";
        UpdateCollectibleAmounts();
        ApplyPaymentCreditToPaymentInputs();
    }

    private readonly string _customerCode;
    private readonly string _customerPhone;

    public async Task InitializeAsync()
    {
        if (_creditNotes == null)
            return;

        AvailableCreditNotes.Clear();
        HasAvailableCredit = false;

        if (string.IsNullOrEmpty(_customerPhone) && string.IsNullOrEmpty(_customerCode))
            return;

        var notes = await _creditNotes.ListAvailableForCustomerAsync(_storeId, _customerCode, _customerPhone);
        foreach (var note in notes)
            AvailableCreditNotes.Add(CustomerCreditNoteOption.FromRecord(note));

        HasAvailableCredit = AvailableCreditNotes.Count > 0;
        UpdateCreditNoteMaxDisplay();
        UpdateCollectibleAmounts();
        ApplyPaymentCreditToPaymentInputs();
    }

    [RelayCommand]
    private void TogglePaymentCreditNote(CustomerCreditNoteOption? option)
    {
        if (option == null)
            return;

        if (_selectedPaymentCreditNoteNo == option.CreditNoteNo)
        {
            _selectedPaymentCreditNoteNo = null;
            foreach (var note in AvailableCreditNotes)
                note.IsSelected = false;
            CreditNoteReference = "";
            SplitCreditNoteReference = "";
        }
        else
        {
            _selectedPaymentCreditNoteNo = option.CreditNoteNo;
            foreach (var note in AvailableCreditNotes)
                note.IsSelected = note.CreditNoteNo == _selectedPaymentCreditNoteNo;
            CreditNoteReference = option.CreditNoteNo;
            SplitCreditNoteReference = option.CreditNoteNo;
        }

        RefreshPaymentCreditBreakdown();
        UpdateCreditNoteMaxDisplay();
        UpdateCollectibleAmounts();
        ApplyPaymentCreditToPaymentInputs();
    }

    private void RefreshPaymentCreditBreakdown()
    {
        var selected = AvailableCreditNotes.FirstOrDefault(n => n.CreditNoteNo == _selectedPaymentCreditNoteNo);
        if (selected == null)
        {
            SelectedPaymentCreditLabel = "";
            PaymentOriginalCreditFormatted = "";
            PaymentApplyingCreditFormatted = "";
            PaymentRemainingAfterFormatted = "";
            HasPaymentCreditSelected = false;
            UpdateCollectibleAmounts();
            ApplyPaymentCreditToPaymentInputs();
            return;
        }

        HasPaymentCreditSelected = true;

        var maxApply = GetMaxCreditForNote(selected.CreditNoteNo);
        selected.ApplyingAmount = maxApply;
        selected.RemainingAfterApply = selected.RemainingAmount - maxApply;
        selected.RefreshDisplayLabel();

        SelectedPaymentCreditLabel = selected.CreditNoteNo;
        PaymentOriginalCreditFormatted = FormatRupee(selected.OriginalAmount);
        PaymentApplyingCreditFormatted = FormatRupee(maxApply);
        PaymentRemainingAfterFormatted = FormatRupee(selected.RemainingAfterApply);
        UpdateCollectibleAmounts();
    }

    private void UpdateCollectibleAmounts()
    {
        PaymentCreditAmount = string.IsNullOrEmpty(_selectedPaymentCreditNoteNo)
            ? 0
            : GetMaxCreditForNote(_selectedPaymentCreditNoteNo);
        CollectibleAmount = Math.Max(0, InvoicePayableAmount - PaymentCreditAmount);
        CollectibleFormatted = FormatRupee(CollectibleAmount);
        PaymentCreditAppliedFormatted = FormatRupee(PaymentCreditAmount);
        HasPaymentCreditApplied = PaymentCreditAmount > 0;
    }

    private void ApplyPaymentCreditToPaymentInputs()
    {
        switch (SelectedMode)
        {
            case PaymentMode.Cash:
                AmountReceived = CollectibleAmount;
                UpdateCashChange();
                break;

            case PaymentMode.CreditNote:
                CreditNoteAmount = PaymentCreditAmount > 0 ? PaymentCreditAmount : InvoicePayableAmount;
                break;

            case PaymentMode.Split:
                SplitCreditNoteAmount = PaymentCreditAmount;
                if (PaymentCreditAmount > 0 && !string.IsNullOrEmpty(_selectedPaymentCreditNoteNo))
                    SplitCreditNoteReference = _selectedPaymentCreditNoteNo;
                RecalcSplitBalance();
                break;

            case PaymentMode.Card:
            case PaymentMode.Upi:
                break;
        }
    }

    private void UpdateCashChange()
    {
        var change = AmountReceived - CollectibleAmount;
        ChangeDueFormatted = change >= 0 ? FormatRupee(change) : "-" + FormatRupee(Math.Abs(change));
        IsCashShortfall = change < 0;
    }

    private decimal GetMaxCreditForNote(string creditNoteNo)
    {
        var note = AvailableCreditNotes.FirstOrDefault(n => n.CreditNoteNo == creditNoteNo);
        if (note == null)
            return 0;

        var reserved = string.Equals(creditNoteNo, _billingReservedCreditNoteNo, StringComparison.OrdinalIgnoreCase)
            ? _billingReservedCreditAmount
            : 0;
        var available = Math.Max(0, note.RemainingAmount - reserved);
        return Math.Min(available, InvoicePayableAmount);
    }

    private void UpdateCreditNoteMaxDisplay()
    {
        if (string.IsNullOrEmpty(_selectedPaymentCreditNoteNo))
        {
            CreditNoteMaxFormatted = HasAvailableCredit ? "Select a credit note below" : "";
            return;
        }

        CreditNoteMaxFormatted = $"Max from note: {FormatRupee(GetMaxCreditForNote(_selectedPaymentCreditNoteNo))}";
    }

    partial void OnSelectedModeChanged(PaymentMode value)
    {
        ErrorMessage = "";
        Status = PaymentStatus.Idle;

        if (value == PaymentMode.Split)
        {
            SplitCashAmount = 0;
            SplitCardAmount = 0;
            SplitUpiAmount = 0;
        }

        ApplyPaymentCreditToPaymentInputs();
    }

    partial void OnAmountReceivedChanged(decimal value) => UpdateCashChange();

    partial void OnSplitCashAmountChanged(decimal value) => RecalcSplitBalance();
    partial void OnSplitCardAmountChanged(decimal value) => RecalcSplitBalance();
    partial void OnSplitUpiAmountChanged(decimal value) => RecalcSplitBalance();
    partial void OnSplitCreditNoteAmountChanged(decimal value) => RecalcSplitBalance();

    partial void OnCreditNoteReferenceChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var match = AvailableCreditNotes.FirstOrDefault(n =>
            string.Equals(n.CreditNoteNo, value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match != null && _selectedPaymentCreditNoteNo != match.CreditNoteNo)
            TogglePaymentCreditNote(match);
    }

    private void RecalcSplitBalance()
    {
        var allocated = SplitCashAmount + SplitCardAmount + SplitUpiAmount + SplitCreditNoteAmount;
        var remaining = InvoicePayableAmount - allocated;
        SplitRemainingFormatted = FormatRupee(remaining);
        IsSplitBalanced = remaining == 0
            && (SplitCashAmount > 0 || SplitCardAmount > 0 || SplitUpiAmount > 0 || SplitCreditNoteAmount > 0);
    }

    private async Task<bool> ApplyPaymentCreditLegAsync(List<PaymentLegResult> legs)
    {
        if (PaymentCreditAmount <= 0 || string.IsNullOrEmpty(_selectedPaymentCreditNoteNo))
            return true;

        if (!await ValidateAndConsumeCreditAsync(_selectedPaymentCreditNoteNo, PaymentCreditAmount))
            return false;

        var cnResult = await _router.PayAndRecordAsync(
            PaymentProviderKind.CreditNote,
            new PaymentRequest(_invoiceNo, PaymentCreditAmount, "INR", _selectedPaymentCreditNoteNo),
            CancellationToken.None,
            enqueueOutbox: !_skipPaymentOutbox);
        legs.Add(new PaymentLegResult
        {
            Provider = PaymentProviderKind.CreditNote,
            Amount = PaymentCreditAmount,
            Reference = cnResult.ProviderReference,
            Status = cnResult.Status,
        });
        return true;
    }

    private async Task<bool> ValidateAndConsumeCreditAsync(string creditNoteNo, decimal amount)
    {
        if (_creditNotes == null || amount <= 0)
            return true;

        var note = await _creditNotes.GetByCreditNoteNoAsync(creditNoteNo);
        if (note == null || note.Status != CustomerCreditNoteService.StatusAvailable)
        {
            ErrorMessage = "Credit note not found or not available.";
            return false;
        }

        var max = GetMaxCreditForNote(creditNoteNo);
        if (amount > max)
        {
            ErrorMessage = $"Amount exceeds available credit ({FormatRupee(max)}).";
            return false;
        }

        var consumed = await _creditNotes.ConsumeAsync(creditNoteNo, _invoiceNo, amount);
        if (!consumed)
        {
            ErrorMessage = "Could not apply credit note (balance may have changed).";
            return false;
        }

        return true;
    }

    [RelayCommand]
    private async Task ConfirmPayment()
    {
        ErrorMessage = "";
        Status = PaymentStatus.Processing;
        IsProcessing = true;

        try
        {
            var legs = new List<PaymentLegResult>();

            switch (SelectedMode)
            {
                case PaymentMode.Cash:
                    if (CollectibleAmount > 0 && AmountReceived < CollectibleAmount)
                    {
                        ErrorMessage = "Amount received is less than the amount to collect.";
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    if (!await ApplyPaymentCreditLegAsync(legs))
                    {
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    if (CollectibleAmount > 0)
                    {
                        var cashResult = await _router.PayAndRecordAsync(
                            PaymentProviderKind.Cash,
                            new PaymentRequest(_invoiceNo, CollectibleAmount, "INR"),
                            CancellationToken.None,
                            enqueueOutbox: !_skipPaymentOutbox);
                        legs.Add(new PaymentLegResult
                        {
                            Provider = PaymentProviderKind.Cash,
                            Amount = CollectibleAmount,
                            Reference = cashResult.ProviderReference,
                            Status = cashResult.Status,
                        });
                    }
                    break;

                case PaymentMode.Card:
                    if (!await ApplyPaymentCreditLegAsync(legs))
                    {
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    if (CollectibleAmount <= 0)
                        break;
                    DeviceStatusText = "Waiting for POS device...";
                    var cardResult = await _router.PayAndRecordAsync(
                        PaymentProviderKind.PineLabs,
                        new PaymentRequest(_invoiceNo, CollectibleAmount, "INR"),
                        CancellationToken.None,
                        enqueueOutbox: !_skipPaymentOutbox);
                    if (cardResult.Status != "Success")
                    {
                        ErrorMessage = $"POS transaction failed: {cardResult.Status}";
                        DeviceStatusText = "Failed";
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    DeviceStatusText = "Approved";
                    legs.Add(new PaymentLegResult
                    {
                        Provider = PaymentProviderKind.PineLabs,
                        Amount = CollectibleAmount,
                        Reference = cardResult.ProviderReference,
                        Status = cardResult.Status,
                    });
                    break;

                case PaymentMode.Upi:
                    if (!await ApplyPaymentCreditLegAsync(legs))
                    {
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    if (CollectibleAmount <= 0)
                        break;
                    DeviceStatusText = "Processing UPI payment...";
                    var upiResult = await _router.PayAndRecordAsync(
                        PaymentProviderKind.Razorpay,
                        new PaymentRequest(_invoiceNo, CollectibleAmount, "INR"),
                        CancellationToken.None,
                        enqueueOutbox: !_skipPaymentOutbox);
                    if (upiResult.Status != "Success" && upiResult.Status != "Pending")
                    {
                        ErrorMessage = $"UPI payment failed: {upiResult.Status}";
                        DeviceStatusText = "Failed";
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    DeviceStatusText = upiResult.Status == "Success" ? "Approved" : "Pending Confirmation";
                    legs.Add(new PaymentLegResult
                    {
                        Provider = PaymentProviderKind.Razorpay,
                        Amount = CollectibleAmount,
                        Reference = upiResult.ProviderReference,
                        Status = upiResult.Status,
                    });
                    break;

                case PaymentMode.CreditNote:
                    var cnRef = (CreditNoteReference ?? "").Trim();
                    if (string.IsNullOrEmpty(cnRef))
                    {
                        ErrorMessage = "Select a customer credit note or enter a reference.";
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    if (CreditNoteAmount <= 0 || CreditNoteAmount > InvoicePayableAmount)
                    {
                        ErrorMessage = "Credit note amount must be greater than zero and not exceed invoice total.";
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    if (!await ValidateAndConsumeCreditAsync(cnRef, CreditNoteAmount))
                    {
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    var cnResult = await _router.PayAndRecordAsync(
                        PaymentProviderKind.CreditNote,
                        new PaymentRequest(_invoiceNo, CreditNoteAmount, "INR", cnRef),
                        CancellationToken.None,
                        enqueueOutbox: !_skipPaymentOutbox);
                    legs.Add(new PaymentLegResult
                    {
                        Provider = PaymentProviderKind.CreditNote,
                        Amount = CreditNoteAmount,
                        Reference = cnResult.ProviderReference,
                        Status = cnResult.Status,
                    });
                    break;

                case PaymentMode.Split:
                    if (!IsSplitBalanced)
                    {
                        ErrorMessage = "Split amounts must exactly equal the payable total.";
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    if (SplitCreditNoteAmount > 0 && string.IsNullOrWhiteSpace(SplitCreditNoteReference))
                    {
                        ErrorMessage = "Select a credit note or enter a reference for the credit note split amount.";
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    if (SplitCreditNoteAmount > 0)
                    {
                        if (!await ValidateAndConsumeCreditAsync(SplitCreditNoteReference.Trim(), SplitCreditNoteAmount))
                        {
                            Status = PaymentStatus.Failed;
                            return;
                        }
                    }

                    var splitEntries = new List<(PaymentProviderKind Provider, decimal Amount, string? Reference)>();
                    if (SplitCashAmount > 0)
                        splitEntries.Add((PaymentProviderKind.Cash, SplitCashAmount, null));
                    if (SplitCardAmount > 0)
                        splitEntries.Add((PaymentProviderKind.PineLabs, SplitCardAmount, null));
                    if (SplitUpiAmount > 0)
                        splitEntries.Add((PaymentProviderKind.Razorpay, SplitUpiAmount, null));
                    if (SplitCreditNoteAmount > 0)
                        splitEntries.Add((PaymentProviderKind.CreditNote, SplitCreditNoteAmount, SplitCreditNoteReference.Trim()));

                    foreach (var (provider, amount, reference) in splitEntries)
                    {
                        var splitResult = await _router.PayAndRecordAsync(
                            provider,
                            new PaymentRequest(_invoiceNo, amount, "INR", reference),
                            CancellationToken.None,
                            enqueueOutbox: !_skipPaymentOutbox);

                        if (splitResult.Status != "Success" && splitResult.Status != "Pending")
                        {
                            ErrorMessage = $"{provider} leg failed: {splitResult.Status}";
                            Status = PaymentStatus.Failed;
                            return;
                        }

                        legs.Add(new PaymentLegResult
                        {
                            Provider = provider,
                            Amount = amount,
                            Reference = splitResult.ProviderReference,
                            Status = splitResult.Status,
                        });
                    }
                    break;
            }

            decimal? cashReceived = null;
            decimal? changeReturned = null;
            if (SelectedMode == PaymentMode.Cash)
            {
                cashReceived = AmountReceived;
                changeReturned = Math.Max(0m, AmountReceived - CollectibleAmount);
            }

            Outcome = new PaymentOutcome
            {
                Confirmed = true,
                Legs = legs,
                Mode = SelectedMode,
                CashReceived = cashReceived,
                ChangeReturned = changeReturned,
            };
            Status = PaymentStatus.Success;
            CloseDialog?.Invoke(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Status = PaymentStatus.Failed;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        Outcome = new PaymentOutcome { Confirmed = false };
        CloseDialog?.Invoke(false);
    }

    private static string FormatRupee(decimal value) => "₹ " + value.ToString("N2", InCulture);
}
