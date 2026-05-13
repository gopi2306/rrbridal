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
using RRBridal.StoreBilling.App.Services.Payments;

namespace RRBridal.StoreBilling.App.ViewModels;

public enum PaymentMode
{
    Cash,
    Card,
    Upi,
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

    // --- Split ---
    public ObservableCollection<SplitPaymentLeg> SplitLegs { get; } = new();
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

        SplitLegs.CollectionChanged += (_, _) => RecalcSplitBalance();
    }

    partial void OnSelectedModeChanged(PaymentMode value)
    {
        ErrorMessage = "";
        Status = PaymentStatus.Idle;
    }

    partial void OnAmountReceivedChanged(decimal value)
    {
        var change = value - PayableAmount;
        ChangeDueFormatted = change >= 0 ? FormatRupee(change) : "-" + FormatRupee(Math.Abs(change));
        IsCashShortfall = change < 0;
    }

    [RelayCommand]
    private void AddSplitLeg()
    {
        var remaining = PayableAmount - SplitLegs.Sum(l => l.Amount);
        var leg = new SplitPaymentLeg { Method = PaymentProviderKind.Cash, Amount = Math.Max(0, remaining) };
        leg.PropertyChanged += (_, _) => RecalcSplitBalance();
        SplitLegs.Add(leg);
        RecalcSplitBalance();
    }

    [RelayCommand]
    private void RemoveSplitLeg(SplitPaymentLeg? leg)
    {
        if (leg != null)
        {
            SplitLegs.Remove(leg);
            RecalcSplitBalance();
        }
    }

    private void RecalcSplitBalance()
    {
        var allocated = SplitLegs.Sum(l => l.Amount);
        var remaining = PayableAmount - allocated;
        SplitRemainingFormatted = FormatRupee(remaining);
        IsSplitBalanced = remaining == 0 && SplitLegs.Count > 0 && SplitLegs.All(l => l.Amount > 0);
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

                case PaymentMode.Split:
                    if (!IsSplitBalanced)
                    {
                        ErrorMessage = "Split amounts must exactly equal the payable total.";
                        Status = PaymentStatus.Failed;
                        return;
                    }
                    foreach (var splitLeg in SplitLegs)
                    {
                        var splitResult = await _router.PayAndRecordAsync(
                            splitLeg.Method,
                            new PaymentRequest(_invoiceNo, splitLeg.Amount, "INR"),
                            CancellationToken.None);

                        splitLeg.Reference = splitResult.ProviderReference;
                        splitLeg.Status = splitResult.Status;

                        if (splitResult.Status != "Success" && splitResult.Status != "Pending")
                        {
                            ErrorMessage = $"{splitLeg.Method} leg failed: {splitResult.Status}";
                            Status = PaymentStatus.Failed;
                            return;
                        }

                        legs.Add(new PaymentLegResult
                        {
                            Provider = splitLeg.Method,
                            Amount = splitLeg.Amount,
                            Reference = splitResult.ProviderReference,
                            Status = splitResult.Status,
                        });
                    }
                    break;
            }

            Outcome = new PaymentOutcome { Confirmed = true, Legs = legs };
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
