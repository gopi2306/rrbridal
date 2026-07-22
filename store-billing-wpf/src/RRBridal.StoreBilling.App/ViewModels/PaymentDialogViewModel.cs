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
    OnlineCod,
    Credit,
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
    public decimal CreditNoteCashOutAmount { get; init; }
    public string? CreditNoteCashOutNo { get; init; }
    public bool IsCreditBilling { get; init; }
    public decimal CreditAdvanceAmount { get; init; }
    public decimal CreditBalanceDue { get; init; }
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
    private readonly RazorpayPosSettingsStore? _razorpayPosSettings;
    private readonly string _storeId;
    private readonly string _invoiceNo;
    private readonly string? _billingReservedCreditNoteNo;
    private readonly decimal _billingReservedCreditAmount;
    private readonly bool _skipPaymentOutbox;
    private readonly bool _allowCreditNoteRemainingCashout;
    private readonly string _posCounter;

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
    [ObservableProperty] private bool _canCashOutCreditNote;
    [ObservableProperty] private decimal _creditNoteCashOutAmount;
    [ObservableProperty] private string _creditNoteCashOutMaxFormatted = "";

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

    public bool IsRazorpayPosConfigured { get; private set; }
    public string CardModeLabel { get; private set; } = "Card";
    public string UpiModeLabel { get; private set; } = "UPI";
    public string CardPaymentTitle { get; private set; } = "Card payment";
    public string CardPaymentHelpText { get; private set; } = "";
    public string UpiPaymentTitle { get; private set; } = "UPI payment";
    public string UpiPaymentHelpText { get; private set; } = "";

    public Action<bool>? CloseDialog { get; set; }
    public PaymentOutcome Outcome { get; private set; } = new() { Confirmed = false };

    public PaymentDialogViewModel(IPaymentRouter router, string invoiceNo, decimal payableAmount, RazorpayPosSettingsStore? razorpayPosSettings = null)
        : this(router, null, "", invoiceNo, payableAmount, null, null, null, 0, false, false, "", razorpayPosSettings)
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
        bool skipPaymentOutbox = false,
        bool allowCreditNoteRemainingCashout = false,
        string? posCounter = null,
        RazorpayPosSettingsStore? razorpayPosSettings = null,
        bool allowCreditBilling = false,
        decimal creditBillingMinAdvancePercent = 0,
        decimal creditBillingMinAdvanceAmount = 0,
        bool creditBillingAllowZeroAdvance = true,
        decimal creditBillingMaxBalancePerBill = 0)
    {
        _router = router;
        _creditNotes = creditNotes;
        _razorpayPosSettings = razorpayPosSettings;
        _storeId = storeId ?? "";
        _invoiceNo = invoiceNo;
        _billingReservedCreditNoteNo = billingReservedCreditNoteNo?.Trim();
        _billingReservedCreditAmount = billingReservedCreditAmount;
        _skipPaymentOutbox = skipPaymentOutbox;
        _allowCreditNoteRemainingCashout = allowCreditNoteRemainingCashout;
        _posCounter = posCounter?.Trim() ?? "";
        _allowCreditBilling = allowCreditBilling;
        _creditBillingMinAdvancePercent = creditBillingMinAdvancePercent;
        _creditBillingMinAdvanceAmount = creditBillingMinAdvanceAmount;
        _creditBillingAllowZeroAdvance = creditBillingAllowZeroAdvance;
        _creditBillingMaxBalancePerBill = creditBillingMaxBalancePerBill;
        InvoicePayableAmount = payableAmount;
        PayableFormatted = MoneyMath.FormatPayable(payableAmount);
        AmountReceived = payableAmount;
        CreditNoteAmount = payableAmount;
        CreditAdvanceAmount = ComputeDefaultAdvance(payableAmount);

        HasBillingCreditReserved = billingReservedCreditAmount > 0
            && !string.IsNullOrEmpty(_billingReservedCreditNoteNo);
        if (HasBillingCreditReserved)
        {
            BillingCreditReservedBanner =
                $"Invoice credit {MoneyMath.FormatRupee(billingReservedCreditAmount)} from {_billingReservedCreditNoteNo} already applied.";
        }

        _customerCode = customerCode?.Trim() ?? "";
        _customerPhone = customerPhone?.Trim() ?? "";
        ReloadPosConfiguration();
        UpdateCollectibleAmounts();
        ApplyPaymentCreditToPaymentInputs();
        UpdateCreditBillingDisplays();
    }

    private readonly string _customerCode;
    private readonly string _customerPhone;
    private readonly bool _allowCreditBilling;
    private readonly decimal _creditBillingMinAdvancePercent;
    private readonly decimal _creditBillingMinAdvanceAmount;
    private readonly bool _creditBillingAllowZeroAdvance;
    private readonly decimal _creditBillingMaxBalancePerBill;

    public bool ShowCreditBillingOption => _allowCreditBilling;

    [ObservableProperty] private decimal _creditAdvanceAmount;
    [ObservableProperty] private string _creditAdvanceAmountText = "";
    [ObservableProperty] private string _creditBalanceDueFormatted = "₹ 0.00";
    [ObservableProperty] private string _creditBillingHint = "";
    [ObservableProperty] private PaymentMode _creditAdvanceMode = PaymentMode.Cash;

    partial void OnCreditAdvanceAmountChanged(decimal value)
    {
        UpdateCreditBillingDisplays();
        if (!_suppressCreditAdvanceTextSync)
            CreditAdvanceAmountText = value == 0 ? "" : MoneyMath.FormatEditableAmount(value);
        if (SelectedMode == PaymentMode.Credit)
            ApplyCreditAdvancePaymentInputs();
    }

    partial void OnCreditAdvanceModeChanged(PaymentMode value)
    {
        ErrorMessage = "";
        Status = PaymentStatus.Idle;
        if (SelectedMode == PaymentMode.Credit)
            ApplyCreditAdvancePaymentInputs();
    }

    partial void OnCreditAdvanceAmountTextChanged(string value)
    {
        if (_suppressCreditAdvanceTextSync)
            return;
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amt)
            || decimal.TryParse(value, NumberStyles.Number, InCulture, out amt))
        {
            CreditAdvanceAmount = MoneyMath.RoundDisplayAmount(Math.Max(0m, amt));
        }
        else if (string.IsNullOrWhiteSpace(value))
        {
            CreditAdvanceAmount = 0;
        }
    }

    private bool _suppressCreditAdvanceTextSync;

    private decimal ComputeDefaultAdvance(decimal payable)
    {
        var fromPercent = payable * (_creditBillingMinAdvancePercent / 100m);
        var min = Math.Max(fromPercent, _creditBillingMinAdvanceAmount);
        return MoneyMath.RoundDisplayAmount(Math.Min(payable, Math.Max(0m, min)));
    }

    private void UpdateCreditBillingDisplays()
    {
        var balance = Math.Max(0m, MoneyMath.RoundDisplayAmount(InvoicePayableAmount - CreditAdvanceAmount));
        CreditBalanceDueFormatted = MoneyMath.FormatRupee(balance);
        var parts = new List<string>();
        if (_creditBillingMinAdvancePercent > 0 || _creditBillingMinAdvanceAmount > 0)
        {
            var min = ComputeDefaultAdvance(InvoicePayableAmount);
            parts.Add($"Minimum advance: {MoneyMath.FormatRupee(min)}");
        }
        if (!_creditBillingAllowZeroAdvance)
            parts.Add("Zero advance not allowed");
        if (_creditBillingMaxBalancePerBill > 0)
            parts.Add($"Max balance: {MoneyMath.FormatRupee(_creditBillingMaxBalancePerBill)}");
        CreditBillingHint = string.Join(" · ", parts);
    }

    private bool ValidateCreditBilling(out string error)
    {
        error = "";
        if (!_allowCreditBilling)
        {
            error = "Credit billing is disabled.";
            return false;
        }

        var advance = MoneyMath.RoundDisplayAmount(CreditAdvanceAmount);
        if (advance < 0 || advance > InvoicePayableAmount)
        {
            error = "Advance must be between 0 and payable.";
            return false;
        }

        if (advance <= 0 && !_creditBillingAllowZeroAdvance)
        {
            error = "Advance is required for credit billing.";
            return false;
        }

        var min = ComputeDefaultAdvance(InvoicePayableAmount);
        if (advance + 0.009m < min)
        {
            error = $"Advance must be at least {MoneyMath.FormatRupee(min)}.";
            return false;
        }

        var balance = MoneyMath.RoundDisplayAmount(InvoicePayableAmount - advance);
        if (_creditBillingMaxBalancePerBill > 0 && balance > _creditBillingMaxBalancePerBill + 0.009m)
        {
            error = $"Balance due cannot exceed {MoneyMath.FormatRupee(_creditBillingMaxBalancePerBill)}.";
            return false;
        }

        if (balance <= 0.009m)
        {
            error = "Use Cash/Card/UPI for full payment. Credit billing requires a remaining balance.";
            return false;
        }

        if (advance > 0 && !ValidateCreditAdvancePayment(out error))
            return false;

        return true;
    }

    private decimal GetPaymentTargetAmount() =>
        SelectedMode == PaymentMode.Credit ? CreditAdvanceAmount : CollectibleAmount;

    private void ApplyCreditAdvancePaymentInputs()
    {
        switch (CreditAdvanceMode)
        {
            case PaymentMode.Cash:
                AmountReceived = CreditAdvanceAmount;
                UpdateCashChange();
                break;

            case PaymentMode.CreditNote:
                CreditNoteAmount = CreditAdvanceAmount;
                break;

            case PaymentMode.Split:
                if (PaymentCreditAmount > 0)
                {
                    SplitCreditNoteAmount = Math.Min(PaymentCreditAmount, CreditAdvanceAmount);
                    if (!string.IsNullOrEmpty(_selectedPaymentCreditNoteNo))
                        SplitCreditNoteReference = _selectedPaymentCreditNoteNo;
                }
                CapAndRecalcSplit(nameof(SplitCashAmount));
                break;
        }
    }

    private bool ValidateCreditAdvancePayment(out string error)
    {
        error = "";
        var advance = MoneyMath.RoundDisplayAmount(CreditAdvanceAmount);
        if (advance <= 0)
            return true;

        switch (CreditAdvanceMode)
        {
            case PaymentMode.Cash:
                if (AmountReceived < advance)
                {
                    error = "Amount received must cover the advance.";
                    return false;
                }
                return true;

            case PaymentMode.CreditNote:
                var cnRef = (CreditNoteReference ?? "").Trim();
                if (string.IsNullOrEmpty(cnRef))
                {
                    error = "Select a customer credit note or enter a reference for the advance.";
                    return false;
                }
                if (CreditNoteAmount <= 0 || CreditNoteAmount > advance + 0.009m)
                {
                    error = "Credit note advance must be greater than zero and not exceed the advance amount.";
                    return false;
                }
                if (Math.Abs(CreditNoteAmount - advance) > 0.009m)
                {
                    error = "Credit note amount must equal the advance amount.";
                    return false;
                }
                return true;

            case PaymentMode.Split:
                if (!IsSplitBalanced)
                {
                    error = "Split amounts must exactly equal the advance amount.";
                    return false;
                }
                if (SplitCreditNoteAmount > 0 && string.IsNullOrWhiteSpace(SplitCreditNoteReference))
                {
                    error = "Select a credit note or enter a reference for the credit note split amount.";
                    return false;
                }
                return true;

            case PaymentMode.Card:
            case PaymentMode.Upi:
                return true;

            default:
                error = "Select an advance payment mode.";
                return false;
        }
    }

    public async Task InitializeAsync()
    {
        ReloadPosConfiguration();

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
            CreditNoteCashOutAmount = 0;
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
            CanCashOutCreditNote = false;
            CreditNoteCashOutMaxFormatted = "";
            CreditNoteCashOutAmount = 0;
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
        PaymentOriginalCreditFormatted = MoneyMath.FormatRupee(selected.OriginalAmount);
        PaymentApplyingCreditFormatted = MoneyMath.FormatRupee(maxApply);
        PaymentRemainingAfterFormatted = MoneyMath.FormatRupee(selected.RemainingAfterApply);
        CanCashOutCreditNote = _allowCreditNoteRemainingCashout && selected.RemainingAfterApply > 0;
        CreditNoteCashOutMaxFormatted = CanCashOutCreditNote
            ? $"Max cash out: {MoneyMath.FormatRupee(selected.RemainingAfterApply)}"
            : "";
        if (!CanCashOutCreditNote)
            CreditNoteCashOutAmount = 0;
        else
            CapCreditNoteCashOutAmount();
        UpdateCollectibleAmounts();
    }

    partial void OnCreditNoteCashOutAmountChanged(decimal value) => CapCreditNoteCashOutAmount();

    private void CapCreditNoteCashOutAmount()
    {
        if (!CanCashOutCreditNote || string.IsNullOrEmpty(_selectedPaymentCreditNoteNo))
        {
            if (CreditNoteCashOutAmount != 0)
                CreditNoteCashOutAmount = 0;
            return;
        }

        var selected = AvailableCreditNotes.FirstOrDefault(n => n.CreditNoteNo == _selectedPaymentCreditNoteNo);
        if (selected == null)
            return;

        if (CreditNoteCashOutAmount < 0)
            CreditNoteCashOutAmount = 0;
        else if (CreditNoteCashOutAmount > selected.RemainingAfterApply)
            CreditNoteCashOutAmount = selected.RemainingAfterApply;
    }

    private void UpdateCollectibleAmounts()
    {
        PaymentCreditAmount = string.IsNullOrEmpty(_selectedPaymentCreditNoteNo)
            ? 0
            : GetMaxCreditForNote(_selectedPaymentCreditNoteNo);
        CollectibleAmount = Math.Max(0, InvoicePayableAmount - PaymentCreditAmount);
        CollectibleFormatted = MoneyMath.FormatRupee(CollectibleAmount);
        PaymentCreditAppliedFormatted = MoneyMath.FormatRupee(PaymentCreditAmount);
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
                CapAndRecalcSplit(nameof(SplitCashAmount));
                break;

            case PaymentMode.Card:
            case PaymentMode.Upi:
                break;
        }
    }

    private void UpdateCashChange()
    {
        var target = GetPaymentTargetAmount();
        var change = AmountReceived - target;
        ChangeDueFormatted = change >= 0 ? MoneyMath.FormatRupee(change) : "-" + MoneyMath.FormatRupee(Math.Abs(change));
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

        CreditNoteMaxFormatted = $"Max from note: {MoneyMath.FormatRupee(GetMaxCreditForNote(_selectedPaymentCreditNoteNo))}";
    }

    partial void OnSelectedModeChanged(PaymentMode value)
    {
        ErrorMessage = "";
        Status = PaymentStatus.Idle;

        if (value == PaymentMode.Split || (value == PaymentMode.Credit && CreditAdvanceMode == PaymentMode.Split))
        {
            SplitCashAmount = 0;
            SplitCardAmount = 0;
            SplitUpiAmount = 0;
        }

        ApplyPaymentCreditToPaymentInputs();
        if (value == PaymentMode.Credit)
            ApplyCreditAdvancePaymentInputs();
    }

    partial void OnAmountReceivedChanged(decimal value) => UpdateCashChange();

    private bool _isCappingSplit;

    partial void OnSplitCashAmountChanged(decimal value) => CapAndRecalcSplit(nameof(SplitCashAmount));
    partial void OnSplitCardAmountChanged(decimal value) => CapAndRecalcSplit(nameof(SplitCardAmount));
    partial void OnSplitUpiAmountChanged(decimal value) => CapAndRecalcSplit(nameof(SplitUpiAmount));
    partial void OnSplitCreditNoteAmountChanged(decimal value) => CapAndRecalcSplit(nameof(SplitCreditNoteAmount));

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
        var target = GetPaymentTargetAmount();
        var allocatedCashLike = SplitCashAmount + SplitCardAmount + SplitUpiAmount + SplitCreditNoteAmount;
        var remainingRaw = target - allocatedCashLike;
        var remainingDisplay = Math.Max(0m, remainingRaw);
        SplitRemainingFormatted = MoneyMath.FormatRupee(remainingDisplay);
        IsSplitBalanced = remainingRaw == 0
            && (SplitCashAmount > 0 || SplitCardAmount > 0 || SplitUpiAmount > 0 || SplitCreditNoteAmount > 0);
    }

    private void CapAndRecalcSplit(string editedProperty)
    {
        if (_isCappingSplit)
        {
            RecalcSplitBalance();
            return;
        }

        try
        {
            _isCappingSplit = true;

            if (SelectedMode == PaymentMode.Split || (SelectedMode == PaymentMode.Credit && CreditAdvanceMode == PaymentMode.Split))
            {
                if (HasPaymentCreditSelected && SelectedMode == PaymentMode.Split && SplitCreditNoteAmount != PaymentCreditAmount)
                    SplitCreditNoteAmount = PaymentCreditAmount;

                var maxCollect = Math.Max(0m, GetPaymentTargetAmount());
                var other = editedProperty switch
                {
                    nameof(SplitCashAmount) => SplitCardAmount + SplitUpiAmount + SplitCreditNoteAmount,
                    nameof(SplitCardAmount) => SplitCashAmount + SplitUpiAmount + SplitCreditNoteAmount,
                    nameof(SplitUpiAmount) => SplitCashAmount + SplitCardAmount + SplitCreditNoteAmount,
                    nameof(SplitCreditNoteAmount) => SplitCashAmount + SplitCardAmount + SplitUpiAmount,
                    _ => SplitCashAmount + SplitCardAmount + SplitUpiAmount + SplitCreditNoteAmount,
                };

                var maxForEdited = Math.Max(0m, maxCollect - other);

                if (editedProperty == nameof(SplitCashAmount) && SplitCashAmount > maxForEdited)
                    SplitCashAmount = maxForEdited;
                else if (editedProperty == nameof(SplitCardAmount) && SplitCardAmount > maxForEdited)
                    SplitCardAmount = maxForEdited;
                else if (editedProperty == nameof(SplitUpiAmount) && SplitUpiAmount > maxForEdited)
                    SplitUpiAmount = maxForEdited;
                else if (editedProperty == nameof(SplitCreditNoteAmount) && SplitCreditNoteAmount > maxForEdited)
                    SplitCreditNoteAmount = maxForEdited;
            }
        }
        finally
        {
            _isCappingSplit = false;
            RecalcSplitBalance();
        }
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
            ErrorMessage = $"Amount exceeds available credit ({MoneyMath.FormatRupee(max)}).";
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

    private async Task<bool> ProcessCreditNoteCashOutAsync()
    {
        if (CreditNoteCashOutAmount <= 0 || string.IsNullOrEmpty(_selectedPaymentCreditNoteNo))
            return true;

        if (!_allowCreditNoteRemainingCashout)
        {
            ErrorMessage = "Credit note cash payout is disabled in Settings.";
            return false;
        }

        if (_creditNotes == null)
        {
            ErrorMessage = "Credit note service is not available.";
            return false;
        }

        var selected = AvailableCreditNotes.FirstOrDefault(n => n.CreditNoteNo == _selectedPaymentCreditNoteNo);
        if (selected == null || CreditNoteCashOutAmount > selected.RemainingAfterApply)
        {
            ErrorMessage = "Cash out amount exceeds credit note remaining balance.";
            return false;
        }

        var ok = await _creditNotes.CashOutAsync(
            _selectedPaymentCreditNoteNo,
            CreditNoteCashOutAmount,
            _invoiceNo,
            _storeId,
            _posCounter);

        if (!ok)
        {
            ErrorMessage = "Could not cash out credit note (balance may have changed).";
            return false;
        }

        return true;
    }

    private void ReloadPosConfiguration()
    {
        _razorpayPosSettings?.Load();
        var configured = _razorpayPosSettings?.Current.IsConfigured ?? false;
        IsRazorpayPosConfigured = configured;

        CardModeLabel = configured ? "Card (Razorpay POS)" : "Card";
        UpiModeLabel = configured ? "UPI (Razorpay POS)" : "UPI";
        CardPaymentTitle = configured ? "Card payment (Razorpay POS)" : "Card payment";
        UpiPaymentTitle = configured ? "UPI payment (Razorpay POS)" : "UPI payment";
        CardPaymentHelpText = configured
            ? "Amount is sent to the wired Razorpay / Ezetap POS device. Complete payment on the terminal."
            : "POS integration is not configured. Payment will be recorded and the bill posted when you confirm.";
        UpiPaymentHelpText = configured
            ? "Amount is sent to the wired Razorpay / Ezetap POS device. Customer pays via UPI on the terminal."
            : "POS integration is not configured. Payment will be recorded and the bill posted when you confirm.";

        if (!configured && string.IsNullOrWhiteSpace(DeviceStatusText))
            DeviceStatusText = "Ready";

        OnPropertyChanged(nameof(IsRazorpayPosConfigured));
        OnPropertyChanged(nameof(CardModeLabel));
        OnPropertyChanged(nameof(UpiModeLabel));
        OnPropertyChanged(nameof(CardPaymentTitle));
        OnPropertyChanged(nameof(CardPaymentHelpText));
        OnPropertyChanged(nameof(UpiPaymentTitle));
        OnPropertyChanged(nameof(UpiPaymentHelpText));
    }

    private static string FormatPosErrorMessage(string message)
    {
        if (message.Contains("deviceid", StringComparison.OrdinalIgnoreCase)
            || message.Contains("device id", StringComparison.OrdinalIgnoreCase))
        {
            return "Razorpay device ID not recognized. Check Settings → Other → Razorpay POS → Device ID (serial|ezetap_android) and ensure the terminal is paired.";
        }

        return message;
    }

    private async Task<bool> ExecuteManualCardOrUpiPaymentAsync(
        PaymentProviderKind reportProvider,
        decimal amount,
        List<PaymentLegResult> legs)
    {
        DeviceStatusText = "Recorded manually (POS not configured)";

        var result = await _router.PayAndRecordAsync(
            reportProvider,
            new PaymentRequest(_invoiceNo, amount, "INR", ManualRecord: true),
            CancellationToken.None,
            enqueueOutbox: !_skipPaymentOutbox);

        if (result.Status != "Success")
        {
            ErrorMessage = $"{reportProvider} payment failed: {result.Status}";
            DeviceStatusText = "Failed";
            return false;
        }

        legs.Add(new PaymentLegResult
        {
            Provider = reportProvider,
            Amount = amount,
            Reference = result.ProviderReference,
            Status = result.Status,
        });
        return true;
    }

    private async Task<bool> ExecuteRazorpayPosPaymentAsync(
        RazorpayPosPayMode posMode,
        PaymentProviderKind reportProvider,
        decimal amount,
        List<PaymentLegResult> legs)
    {
        DeviceStatusText = posMode switch
        {
            RazorpayPosPayMode.Upi => "Waiting for UPI on Razorpay device...",
            RazorpayPosPayMode.Card => "Waiting for card on Razorpay device...",
            _ => "Waiting for Razorpay POS device...",
        };

        var result = await _router.PayAndRecordAsync(
            PaymentProviderKind.Razorpay,
            new PaymentRequest(_invoiceNo, amount, "INR", PosMode: posMode),
            CancellationToken.None,
            enqueueOutbox: !_skipPaymentOutbox);

        if (result.Status != "Success")
        {
            ErrorMessage = FormatPosErrorMessage($"POS payment failed: {result.Status}");
            DeviceStatusText = "Failed";
            return false;
        }

        DeviceStatusText = "Approved";
        legs.Add(new PaymentLegResult
        {
            Provider = reportProvider,
            Amount = amount,
            Reference = result.ProviderReference,
            Status = result.Status,
        });
        return true;
    }

    private async Task<bool> ProcessSplitLegsAsync(
        List<(PaymentProviderKind Provider, decimal Amount, string? Reference, RazorpayPosPayMode? PosMode)> splitEntries,
        List<PaymentLegResult> legs)
    {
        foreach (var (provider, amount, reference, posMode) in splitEntries)
        {
            PaymentResult splitResult;
            if (posMode.HasValue)
            {
                if (IsRazorpayPosConfigured)
                {
                    DeviceStatusText = posMode == RazorpayPosPayMode.Upi
                        ? "Waiting for UPI on Razorpay device..."
                        : "Waiting for card on Razorpay device...";
                    splitResult = await _router.PayAndRecordAsync(
                        PaymentProviderKind.Razorpay,
                        new PaymentRequest(_invoiceNo, amount, "INR", reference, posMode.Value),
                        CancellationToken.None,
                        enqueueOutbox: !_skipPaymentOutbox);
                }
                else
                {
                    DeviceStatusText = "Recorded manually (POS not configured)";
                    splitResult = await _router.PayAndRecordAsync(
                        provider,
                        new PaymentRequest(_invoiceNo, amount, "INR", reference, posMode.Value, ManualRecord: true),
                        CancellationToken.None,
                        enqueueOutbox: !_skipPaymentOutbox);
                }
            }
            else
            {
                splitResult = await _router.PayAndRecordAsync(
                    provider,
                    new PaymentRequest(_invoiceNo, amount, "INR", reference),
                    CancellationToken.None,
                    enqueueOutbox: !_skipPaymentOutbox);
            }

            if (splitResult.Status != "Success" && splitResult.Status != "Pending")
            {
                ErrorMessage = posMode.HasValue && IsRazorpayPosConfigured
                    ? FormatPosErrorMessage($"{provider} leg failed: {splitResult.Status}")
                    : $"{provider} leg failed: {splitResult.Status}";
                DeviceStatusText = "Failed";
                return false;
            }

            legs.Add(new PaymentLegResult
            {
                Provider = provider,
                Amount = amount,
                Reference = splitResult.ProviderReference,
                Status = splitResult.Status,
            });
        }

        return true;
    }

    private async Task<bool> ProcessCreditAdvancePaymentAsync(decimal advance, List<PaymentLegResult> legs)
    {
        switch (CreditAdvanceMode)
        {
            case PaymentMode.Cash:
            {
                var cashResult = await _router.PayAndRecordAsync(
                    PaymentProviderKind.Cash,
                    new PaymentRequest(_invoiceNo, advance, "INR"),
                    CancellationToken.None,
                    enqueueOutbox: !_skipPaymentOutbox);
                legs.Add(new PaymentLegResult
                {
                    Provider = PaymentProviderKind.Cash,
                    Amount = advance,
                    Reference = cashResult.ProviderReference,
                    Status = cashResult.Status,
                });
                return true;
            }

            case PaymentMode.Card:
                if (IsRazorpayPosConfigured)
                    return await ExecuteRazorpayPosPaymentAsync(
                        RazorpayPosPayMode.Card,
                        PaymentProviderKind.PineLabs,
                        advance,
                        legs);

                return await ExecuteManualCardOrUpiPaymentAsync(
                    PaymentProviderKind.PineLabs,
                    advance,
                    legs);

            case PaymentMode.Upi:
                if (IsRazorpayPosConfigured)
                    return await ExecuteRazorpayPosPaymentAsync(
                        RazorpayPosPayMode.Upi,
                        PaymentProviderKind.Razorpay,
                        advance,
                        legs);

                return await ExecuteManualCardOrUpiPaymentAsync(
                    PaymentProviderKind.Razorpay,
                    advance,
                    legs);

            case PaymentMode.CreditNote:
            {
                var cnRef = (CreditNoteReference ?? "").Trim();
                if (!await ValidateAndConsumeCreditAsync(cnRef, advance))
                    return false;

                var cnResult = await _router.PayAndRecordAsync(
                    PaymentProviderKind.CreditNote,
                    new PaymentRequest(_invoiceNo, advance, "INR", cnRef),
                    CancellationToken.None,
                    enqueueOutbox: !_skipPaymentOutbox);
                legs.Add(new PaymentLegResult
                {
                    Provider = PaymentProviderKind.CreditNote,
                    Amount = advance,
                    Reference = cnResult.ProviderReference,
                    Status = cnResult.Status,
                });
                return true;
            }

            case PaymentMode.Split:
            {
                if (SplitCreditNoteAmount > 0)
                {
                    if (!await ValidateAndConsumeCreditAsync(SplitCreditNoteReference.Trim(), SplitCreditNoteAmount))
                        return false;
                }

                var splitEntries = new List<(PaymentProviderKind Provider, decimal Amount, string? Reference, RazorpayPosPayMode? PosMode)>();
                if (SplitCashAmount > 0)
                    splitEntries.Add((PaymentProviderKind.Cash, SplitCashAmount, null, null));
                if (SplitCardAmount > 0)
                    splitEntries.Add((PaymentProviderKind.PineLabs, SplitCardAmount, null, RazorpayPosPayMode.Card));
                if (SplitUpiAmount > 0)
                    splitEntries.Add((PaymentProviderKind.Razorpay, SplitUpiAmount, null, RazorpayPosPayMode.Upi));
                if (SplitCreditNoteAmount > 0)
                    splitEntries.Add((PaymentProviderKind.CreditNote, SplitCreditNoteAmount, SplitCreditNoteReference.Trim(), null));

                return await ProcessSplitLegsAsync(splitEntries, legs);
            }

            default:
                ErrorMessage = "Select an advance payment mode.";
                return false;
        }
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
                    if (IsRazorpayPosConfigured)
                    {
                        if (!await ExecuteRazorpayPosPaymentAsync(
                                RazorpayPosPayMode.Card,
                                PaymentProviderKind.PineLabs,
                                CollectibleAmount,
                                legs))
                        {
                            Status = PaymentStatus.Failed;
                            return;
                        }
                    }
                    else if (!await ExecuteManualCardOrUpiPaymentAsync(
                                 PaymentProviderKind.PineLabs,
                                 CollectibleAmount,
                                 legs))
                    {
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    break;

                case PaymentMode.Upi:
                    if (!await ApplyPaymentCreditLegAsync(legs))
                    {
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    if (CollectibleAmount <= 0)
                        break;
                    if (IsRazorpayPosConfigured)
                    {
                        if (!await ExecuteRazorpayPosPaymentAsync(
                                RazorpayPosPayMode.Upi,
                                PaymentProviderKind.Razorpay,
                                CollectibleAmount,
                                legs))
                        {
                            Status = PaymentStatus.Failed;
                            return;
                        }
                    }
                    else if (!await ExecuteManualCardOrUpiPaymentAsync(
                                 PaymentProviderKind.Razorpay,
                                 CollectibleAmount,
                                 legs))
                    {
                        Status = PaymentStatus.Failed;
                        return;
                    }
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

                    var splitEntries = new List<(PaymentProviderKind Provider, decimal Amount, string? Reference, RazorpayPosPayMode? PosMode)>();
                    if (SplitCashAmount > 0)
                        splitEntries.Add((PaymentProviderKind.Cash, SplitCashAmount, null, null));
                    if (SplitCardAmount > 0)
                        splitEntries.Add((PaymentProviderKind.PineLabs, SplitCardAmount, null, RazorpayPosPayMode.Card));
                    if (SplitUpiAmount > 0)
                        splitEntries.Add((PaymentProviderKind.Razorpay, SplitUpiAmount, null, RazorpayPosPayMode.Upi));
                    if (SplitCreditNoteAmount > 0)
                        splitEntries.Add((PaymentProviderKind.CreditNote, SplitCreditNoteAmount, SplitCreditNoteReference.Trim(), null));

                    if (!await ProcessSplitLegsAsync(splitEntries, legs))
                    {
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    break;

                case PaymentMode.Credit:
                {
                    if (!ValidateCreditBilling(out var creditErr))
                    {
                        ErrorMessage = creditErr;
                        Status = PaymentStatus.Failed;
                        return;
                    }

                    var advance = MoneyMath.RoundDisplayAmount(CreditAdvanceAmount);
                    if (advance > 0)
                    {
                        if (!await ProcessCreditAdvancePaymentAsync(advance, legs))
                        {
                            Status = PaymentStatus.Failed;
                            return;
                        }
                    }

                    break;
                }
            }

            if (CreditNoteCashOutAmount > 0)
            {
                if (!await ProcessCreditNoteCashOutAsync())
                {
                    Status = PaymentStatus.Failed;
                    return;
                }
            }

            decimal? cashReceived = null;
            decimal? changeReturned = null;
            if (SelectedMode == PaymentMode.Cash)
            {
                cashReceived = AmountReceived;
                changeReturned = Math.Max(0m, AmountReceived - CollectibleAmount);
            }
            else if (SelectedMode == PaymentMode.Credit && CreditAdvanceMode == PaymentMode.Cash && CreditAdvanceAmount > 0)
            {
                cashReceived = AmountReceived;
                changeReturned = Math.Max(0m, AmountReceived - CreditAdvanceAmount);
            }

            Outcome = new PaymentOutcome
            {
                Confirmed = true,
                Legs = legs,
                Mode = SelectedMode,
                CashReceived = cashReceived,
                ChangeReturned = changeReturned,
                CreditNoteCashOutAmount = CreditNoteCashOutAmount,
                CreditNoteCashOutNo = CreditNoteCashOutAmount > 0 ? _selectedPaymentCreditNoteNo : null,
                IsCreditBilling = SelectedMode == PaymentMode.Credit,
                CreditAdvanceAmount = SelectedMode == PaymentMode.Credit
                    ? MoneyMath.RoundDisplayAmount(CreditAdvanceAmount)
                    : 0,
                CreditBalanceDue = SelectedMode == PaymentMode.Credit
                    ? MoneyMath.RoundDisplayAmount(InvoicePayableAmount - CreditAdvanceAmount)
                    : 0,
            };
            Status = PaymentStatus.Success;
            CloseDialog?.Invoke(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = FormatPosErrorMessage(ex.Message);
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
}
