using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Payments;

/// <summary>Razorpay wired/wireless POS via Ezetap POS Bridge API.</summary>
public sealed class RazorpayPaymentProvider : IPaymentProvider
{
    private readonly RazorpayPosSettingsStore _settings;
    private readonly RazorpayPosBridgeClient _bridge;

    public RazorpayPaymentProvider(RazorpayPosSettingsStore settings, RazorpayPosBridgeClient? bridge = null)
    {
        _settings = settings;
        _bridge = bridge ?? new RazorpayPosBridgeClient();
    }

    public PaymentProviderKind Kind => PaymentProviderKind.Razorpay;

    public Task<PaymentResult> PayAsync(PaymentRequest request, CancellationToken ct)
    {
        _settings.Load();
        return _bridge.PayAndWaitAsync(_settings.Current, request, ct);
    }
}
