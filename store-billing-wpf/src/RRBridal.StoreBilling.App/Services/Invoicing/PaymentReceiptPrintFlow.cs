using System.Threading.Tasks;
using RRBridal.StoreBilling.App.Services;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

/// <summary>Legacy thin wrapper — prefer <see cref="CreditReceiptPrintFlow"/>.</summary>
public sealed class PaymentReceiptPrintInput
{
    public required string ReceiptNo { get; init; }
    public required string BillNo { get; init; }
    public string CustomerName { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public decimal AmountPaid { get; init; }
    public decimal BalanceDue { get; init; }
    public string PaymentMode { get; init; } = "";
    public string Reference { get; init; } = "";
    public decimal TotalPayable { get; init; }
    public decimal CumulativeAmountPaid { get; init; }
    public decimal AdvanceAtPost { get; init; }
    public string Status { get; init; } = "";
    public string ReceivedBy { get; init; } = "";
    public string BillDate { get; init; } = "";
}

public static class PaymentReceiptPrintFlow
{
    public static Task<bool> ShowAsync(AppServices services, PaymentReceiptPrintInput input)
    {
        var config = services.ReceiptConfig.Current;
        var charWidth = config.Print.ReceiptCharWidth is >= 32 and <= 56
            ? config.Print.ReceiptCharWidth
            : 48;

        var creditInput = new CreditReceiptPrintInput
        {
            Kind = CreditReceiptKind.BalanceCollection,
            Store = config.Store,
            CharWidth = charWidth,
            BillNo = input.BillNo,
            BillDate = input.BillDate,
            ReceiptNo = input.ReceiptNo,
            CustomerName = input.CustomerName,
            CustomerPhone = input.CustomerPhone,
            TotalPayable = input.TotalPayable,
            AdvanceAtPost = input.AdvanceAtPost,
            AmountPaidThisTime = input.AmountPaid,
            CumulativeAmountPaid = input.CumulativeAmountPaid > 0
                ? input.CumulativeAmountPaid
                : input.AmountPaid,
            BalanceDue = input.BalanceDue,
            Status = input.Status,
            PaymentMode = input.PaymentMode,
            Reference = input.Reference,
            ReceivedBy = input.ReceivedBy,
        };

        return CreditReceiptPrintFlow.ShowAsync(services, creditInput);
    }
}
