using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private readonly string _invoiceNo;

    public decimal PayableAmount { get; }
    public string PayableFormatted { get; }

    [ObservableProperty] private PaymentMode _selectedMode = PaymentMode.Cash;

    // --- Cash ---
    [ObservableProperty] private decimal _amountReceived;
    [ObservableProperty] private string _changeDueFormatted = "₹ 0.00";
    [ObservableProperty] private bool _isCashShortfall;

    // --- Card / UPI single-mode ---
    [ObservableProperty] private string _deviceStatusText = "Ready";

    // --- Credit Note ---
    [ObservableProperty] private string _creditNoteReference = "";
    [ObservableProperty] private decimal _creditNoteAmount;

    // --- Split (fixed inputs) ---
    [ObservableProperty] private decimal _splitCashAmount;
    [ObservableProperty] private decimal _splitCardAmount;
    [ObservableProperty] private decimal _splitUpiAmount;
    [ObservableProperty] private decimal _splitCreditNoteAmount;
    [ObservableProperty] private string _splitCreditNoteReference = "";
    [ObservableProperty] private string _splitRemainingFormatted = "₹ 0.00";
    [ObservableProperty] private bool _isSplitBalanced;

    // --- Overall ---
    [ObservableProperty] private PaymentStatus _status = PaymentStatus.Idle;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _isProcessing;

    public Action<bool>? CloseDialog { get; set; }

    public PaymentOutcome Outcome { get; private set; } = new() { Confirmed = false };

    public PaymentDialogViewModel(IPaymentRouter router, string invoiceNo, decimal payableAmount)
    {
        _router = router;
        _invoiceNo = invoiceNo;
        PayableAmount = payableAmount;
        PayableFormatted = FormatRupee(payableAmount);
        AmountReceived = payableAmount;
        CreditNoteAmount = payableAmount;
        RecalcSplitBalance();
    }

    partial void OnSelectedModeChanged(PaymentMode value)
    {
        ErrorMessage = "";
        Status = PaymentStatus.Idle;

        if (value == PaymentMode.CreditNote)
            CreditNoteAmount = PayableAmount;

        if (value == PaymentMode.Split)
        {
            SplitCashAmount = 0;
            SplitCardAmount = 0;
            SplitUpiAmount = 0;
            SplitCreditNoteAmount = 0;
            SplitCreditNoteReference = "";
            RecalcSplitBalance();
        }
    }

    partial void OnAmountReceivedChanged(decimal value)
    {
        var change = value - PayableAmount;
        ChangeDueFormatted = change >= 0 ? FormatRupee(change) : "-" + FormatRupee(Math.Abs(change));
        IsCashShortfall = change < 0;
    }

    partial void OnSplitCashAmountChanged(decimal value) => RecalcSplitBalance();
    partial void OnSplitCardAmountChanged(decimal value) => RecalcSplitBalance();
    partial void OnSplitUpiAmountChanged(decimal value) => RecalcSplitBalance();
    partial void OnSplitCreditNoteAmountChanged(decimal value) => RecalcSplitBalance();

    private void RecalcSplitBalance()
    {
        var allocated = SplitCashAmount + SplitCardAmount + SplitUpiAmount + SplitCreditNoteAmount;
        var remaining = PayableAmount - allocated;
        SplitRemainingFormatted = FormatRupee(remaining);
        IsSplitBalanced = remaining == 0
            && (SplitCashAmount > 0 || SplitCardAmount > 0 || SplitUpiAmount > 0 || SplitCreditNoteAmount > 0);
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
                    if (AmountReceived < PayableAmount)
                    {
                        ErrorMessage = "Amount received is less than payable.";
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    var cashResult = await _router.PayAndRecordAsync(
                        PaymentProviderKind.Cash,
                        new PaymentRequest(_invoiceNo, PayableAmount, "INR"),
                        CancellationToken.None);
                    legs.Add(new PaymentLegResult
                    {
                        Provider = PaymentProviderKind.Cash,
                        Amount = PayableAmount,
                        Reference = cashResult.ProviderReference,
                        Status = cashResult.Status,
                    });
                    break;

                case PaymentMode.Card:
                    DeviceStatusText = "Waiting for POS device...";
                    var cardResult = await _router.PayAndRecordAsync(
                        PaymentProviderKind.PineLabs,
                        new PaymentRequest(_invoiceNo, PayableAmount, "INR"),
                        CancellationToken.None);
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
                        Amount = PayableAmount,
                        Reference = cardResult.ProviderReference,
                        Status = cardResult.Status,
                    });
                    break;

                case PaymentMode.Upi:
                    DeviceStatusText = "Processing UPI payment...";
                    var upiResult = await _router.PayAndRecordAsync(
                        PaymentProviderKind.Razorpay,
                        new PaymentRequest(_invoiceNo, PayableAmount, "INR"),
                        CancellationToken.None);
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
                        Amount = PayableAmount,
                        Reference = upiResult.ProviderReference,
                        Status = upiResult.Status,
                    });
                    break;

                case PaymentMode.CreditNote:
                    var cnRef = (CreditNoteReference ?? "").Trim();
                    if (string.IsNullOrEmpty(cnRef))
                    {
                        ErrorMessage = "Enter a credit note number or reference.";
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    if (CreditNoteAmount <= 0 || CreditNoteAmount > PayableAmount)
                    {
                        ErrorMessage = "Credit note amount must be greater than zero and not exceed payable.";
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    var cnResult = await _router.PayAndRecordAsync(
                        PaymentProviderKind.CreditNote,
                        new PaymentRequest(_invoiceNo, CreditNoteAmount, "INR", cnRef),
                        CancellationToken.None);
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
                        ErrorMessage = "Enter a credit note reference for the credit note split amount.";
                        Status = PaymentStatus.Failed;
                        return;
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
                            CancellationToken.None);

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
                changeReturned = Math.Max(0m, AmountReceived - PayableAmount);
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
