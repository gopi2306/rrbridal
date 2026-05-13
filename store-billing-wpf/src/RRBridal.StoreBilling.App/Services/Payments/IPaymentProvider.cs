using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Payments;

public enum PaymentProviderKind
{
    Cash,
    PineLabs,
    Razorpay,
}

public sealed record PaymentRequest(
    string InvoiceNo,
    decimal Amount,
    string Currency);

public sealed record PaymentResult(
    PaymentProviderKind Provider,
    string ProviderReference,
    string Status,
    string RawResponseJson);

public interface IPaymentProvider
{
    PaymentProviderKind Kind { get; }
    Task<PaymentResult> PayAsync(PaymentRequest request, CancellationToken ct);
}

