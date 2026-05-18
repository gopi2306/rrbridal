using System.Collections.Generic;
using System.Linq;
using RRBridal.StoreBilling.App.Services.Payments;

namespace RRBridal.StoreBilling.App.Services.Invoicing;

public sealed class PaymentReceiptSnap
{
    public decimal CashPaid { get; init; }
    public decimal CardPaid { get; init; }
    public decimal UpiPaid { get; init; }
    public decimal CreditNotePaid { get; init; }
    public decimal? CashReceived { get; init; }
    public decimal? BalanceReturn { get; init; }
    public string PaymentModeSummary { get; init; } = "";
    public bool IsPreview { get; init; }

    public static PaymentReceiptSnap Preview() => new() { IsPreview = true, PaymentModeSummary = "Preview" };

    public static PaymentReceiptSnap FromLegs(
        IReadOnlyList<(PaymentProviderKind Provider, decimal Amount)> legs,
        string modeSummary,
        decimal? cashReceived = null,
        decimal? balanceReturn = null)
    {
        decimal cash = 0, card = 0, upi = 0, cn = 0;
        foreach (var (provider, amount) in legs)
        {
            switch (provider)
            {
                case PaymentProviderKind.Cash:
                    cash += amount;
                    break;
                case PaymentProviderKind.PineLabs:
                    card += amount;
                    break;
                case PaymentProviderKind.Razorpay:
                    upi += amount;
                    break;
                case PaymentProviderKind.CreditNote:
                    cn += amount;
                    break;
            }
        }

        return new PaymentReceiptSnap
        {
            CashPaid = cash,
            CardPaid = card,
            UpiPaid = upi,
            CreditNotePaid = cn,
            CashReceived = cashReceived,
            BalanceReturn = balanceReturn,
            PaymentModeSummary = modeSummary,
        };
    }
}
