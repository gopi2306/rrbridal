using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Payments;

// Placeholder integration. Prefer calling CENTRAL to create Razorpay orders (keys stay server-side).
public sealed class RazorpayPaymentProvider : IPaymentProvider
{
    public PaymentProviderKind Kind => PaymentProviderKind.Razorpay;

    public Task<PaymentResult> PayAsync(PaymentRequest request, CancellationToken ct)
    {
        var raw = JsonSerializer.Serialize(new
        {
            provider = "Razorpay",
            invoiceNo = request.InvoiceNo,
            amount = request.Amount,
            currency = request.Currency,
            note = "stub",
        });

        return Task.FromResult(
            new PaymentResult(
                Provider: Kind,
                ProviderReference: $"RZP-{request.InvoiceNo}",
                Status: "Pending",
                RawResponseJson: raw));
    }
}

