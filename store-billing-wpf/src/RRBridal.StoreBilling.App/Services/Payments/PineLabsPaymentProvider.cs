using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Payments;

// Placeholder integration. Replace with Pine Labs SDK calls for your terminal model.
public sealed class PineLabsPaymentProvider : IPaymentProvider
{
    public PaymentProviderKind Kind => PaymentProviderKind.PineLabs;

    public Task<PaymentResult> PayAsync(PaymentRequest request, CancellationToken ct)
    {
        var raw = JsonSerializer.Serialize(new
        {
            provider = "PineLabs",
            invoiceNo = request.InvoiceNo,
            amount = request.Amount,
            currency = request.Currency,
            note = "stub",
        });

        return Task.FromResult(
            new PaymentResult(
                Provider: Kind,
                ProviderReference: $"PL-{request.InvoiceNo}",
                Status: "Success",
                RawResponseJson: raw));
    }
}

