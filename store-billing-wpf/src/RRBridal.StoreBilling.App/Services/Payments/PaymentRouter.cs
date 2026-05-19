using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services;

namespace RRBridal.StoreBilling.App.Services.Payments;

public interface IPaymentRouter
{
    Task<PaymentResult> PayAndRecordAsync(PaymentProviderKind provider, PaymentRequest request, CancellationToken ct);
}

internal sealed class LocalPaymentDoc
{
    [BsonId] public ObjectId Id { get; set; }
    public required string InvoiceNo { get; set; }
    public required string Provider { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string Status { get; set; }
    public required string ProviderReference { get; set; }
    public required string RawResponseJson { get; set; }
    public required DateTime CreatedAt { get; set; }
    public string? StoreId { get; set; }
    public string? DeviceId { get; set; }
    public string? PosCounter { get; set; }
}

public sealed class PaymentRouter : IPaymentRouter
{
    private readonly IPaymentProvider _pineLabs;
    private readonly IPaymentProvider _razorpay;
    private readonly IMongoCollection<LocalPaymentDoc> _payments;
    private readonly IMongoCollection<BsonDocument> _outbox;
    private readonly StoreContext _storeContext;

    public PaymentRouter(IPaymentProvider pineLabs, IPaymentProvider razorpay, IMongoDatabase localDb, StoreContext storeContext)
    {
        _pineLabs = pineLabs;
        _razorpay = razorpay;
        _payments = localDb.GetCollection<LocalPaymentDoc>("local_payments");
        _outbox = localDb.GetCollection<BsonDocument>("outbox_events");
        _storeContext = storeContext;
    }

    public async Task<PaymentResult> PayAndRecordAsync(PaymentProviderKind provider, PaymentRequest request, CancellationToken ct)
    {
        PaymentResult result;
        if (provider is PaymentProviderKind.Cash or PaymentProviderKind.CreditNote)
        {
            var providerName = provider == PaymentProviderKind.Cash ? "Cash" : "CreditNote";
            var reference = !string.IsNullOrWhiteSpace(request.Reference)
                ? request.Reference.Trim()
                : provider == PaymentProviderKind.Cash
                    ? $"CASH-{request.InvoiceNo}"
                    : $"CN-{request.InvoiceNo}";
            var raw = JsonSerializer.Serialize(new
            {
                provider = providerName,
                invoiceNo = request.InvoiceNo,
                amount = request.Amount,
                currency = request.Currency,
                reference,
            });
            result = new PaymentResult(
                Provider: provider,
                ProviderReference: reference,
                Status: "Success",
                RawResponseJson: raw);
        }
        else
        {
            var impl = provider switch
            {
                PaymentProviderKind.PineLabs => _pineLabs,
                PaymentProviderKind.Razorpay => _razorpay,
                _ => throw new ArgumentOutOfRangeException(nameof(provider)),
            };
            result = await impl.PayAsync(request, ct);
        }

        var paymentDoc = new LocalPaymentDoc
        {
            InvoiceNo = request.InvoiceNo,
            Provider = result.Provider.ToString(),
            Amount = request.Amount,
            Currency = request.Currency,
            Status = result.Status,
            ProviderReference = result.ProviderReference,
            RawResponseJson = result.RawResponseJson,
            CreatedAt = DateTime.UtcNow,
            StoreId = _storeContext.StoreId,
            DeviceId = _storeContext.DeviceId,
            PosCounter = _storeContext.PosCounter,
        };

        await _payments.InsertOneAsync(paymentDoc, cancellationToken: ct);

        var outboxEvent = new BsonDocument
        {
            { "eventId", Guid.NewGuid().ToString() },
            { "storeId", _storeContext.StoreId },
            { "deviceId", _storeContext.DeviceId },
            { "type", "PaymentRecorded" },
            { "createdAt", DateTime.UtcNow.ToString("O") },
            {
                "payload",
                new BsonDocument
                {
                    { "invoiceNo", request.InvoiceNo },
                    { "provider", result.Provider.ToString() },
                    { "amount", request.Amount },
                    { "currency", request.Currency },
                    { "status", result.Status },
                    { "providerReference", result.ProviderReference },
                }
            },
            { "hash", JsonSerializer.Serialize(new { request.InvoiceNo, request.Amount, request.Currency, result.Provider, result.ProviderReference }) },
            { "status", "pending" },
        };

        await _outbox.InsertOneAsync(outboxEvent, cancellationToken: ct);

        return result;
    }
}

