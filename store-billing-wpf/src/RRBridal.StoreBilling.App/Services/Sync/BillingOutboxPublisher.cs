using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services;

namespace RRBridal.StoreBilling.App.Services.Sync;

public sealed class BillingOutboxPublisher
{
    private readonly IMongoCollection<BsonDocument> _outbox;
    private readonly StoreContext _storeContext;

    public BillingOutboxPublisher(IMongoDatabase localDb, StoreContext storeContext)
    {
        _outbox = localDb.GetCollection<BsonDocument>("outbox_events");
        _storeContext = storeContext;
    }

    public async Task<string> EnqueueAsync(
        string type,
        BsonDocument payload,
        string hash,
        CancellationToken ct = default)
    {
        var eventId = Guid.NewGuid().ToString();
        var outboxEvent = new BsonDocument
        {
            { "eventId", eventId },
            { "storeId", _storeContext.StoreId },
            { "deviceId", _storeContext.DeviceId },
            { "type", type },
            { "createdAt", DateTime.UtcNow.ToString("O") },
            { "payload", payload },
            { "hash", hash },
            { "status", "pending" },
        };
        await _outbox.InsertOneAsync(outboxEvent, cancellationToken: ct);
        return eventId;
    }

    public Task<string> PublishInvoiceCreatedAsync(BsonDocument billDoc, CancellationToken ct = default)
    {
        var payload = (BsonDocument)billDoc.DeepClone();
        if (!payload.Contains("billNo") && payload.Contains("invoiceNo"))
            payload["billNo"] = payload["invoiceNo"];

        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync("InvoiceCreated", payload, hash, ct);
    }

    public Task<string> PublishCreditNoteCreatedAsync(BsonDocument noteDoc, CancellationToken ct = default)
    {
        var payload = (BsonDocument)noteDoc.DeepClone();
        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync("CreditNoteCreated", payload, hash, ct);
    }

    public Task<string> PublishCreditNoteAppliedAsync(
        string creditNoteNo,
        string billNo,
        decimal amountApplied,
        decimal remainingAmount,
        string status,
        CancellationToken ct = default)
    {
        var payload = new BsonDocument
        {
            { "creditNoteNo", creditNoteNo.Trim() },
            { "billNo", billNo.Trim() },
            { "amountApplied", (double)amountApplied },
            { "remainingAmount", (double)remainingAmount },
            { "status", status },
        };
        var hash = JsonSerializer.Serialize(new
        {
            creditNoteNo = creditNoteNo.Trim(),
            billNo = billNo.Trim(),
            amountApplied,
            remainingAmount,
            status,
        });
        return EnqueueAsync("CreditNoteApplied", payload, hash, ct);
    }

    public Task<string> PublishCreditNoteCashedOutAsync(
        BsonDocument cashoutDoc,
        string creditNoteNo,
        string billNo,
        decimal cashRefunded,
        decimal remainingAmount,
        string status,
        CancellationToken ct = default)
    {
        var payload = (BsonDocument)cashoutDoc.DeepClone();
        payload["creditNoteNo"] = creditNoteNo.Trim();
        payload["billNo"] = billNo.Trim();
        payload["cashRefunded"] = (double)cashRefunded;
        payload["remainingAmount"] = (double)remainingAmount;
        payload["creditNoteStatus"] = status;
        payload["status"] = cashoutDoc.GetValue("status", "posted").AsString;

        var hash = JsonSerializer.Serialize(new
        {
            creditNoteNo = creditNoteNo.Trim(),
            billNo = billNo.Trim(),
            cashRefunded,
            remainingAmount,
            status,
            cashoutNo = cashoutDoc.GetValue("cashoutNo", "").AsString,
        });
        return EnqueueAsync("CreditNoteCashedOut", payload, hash, ct);
    }

    public Task<string> PublishDailyExpenseCreatedAsync(BsonDocument expenseDoc, CancellationToken ct = default)
    {
        var payload = (BsonDocument)expenseDoc.DeepClone();
        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync("DailyExpenseCreated", payload, hash, ct);
    }

    public Task<string> PublishCashMovementCreatedAsync(BsonDocument movementDoc, CancellationToken ct = default)
    {
        var payload = (BsonDocument)movementDoc.DeepClone();
        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync("CashMovementCreated", payload, hash, ct);
    }

    public Task<string> PublishDaySessionOpenedAsync(BsonDocument sessionDoc, CancellationToken ct = default)
    {
        var payload = (BsonDocument)sessionDoc.DeepClone();
        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync("DaySessionOpened", payload, hash, ct);
    }

    public Task<string> PublishDaySessionClosedAsync(BsonDocument sessionDoc, CancellationToken ct = default)
    {
        var payload = (BsonDocument)sessionDoc.DeepClone();
        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync("DaySessionClosed", payload, hash, ct);
    }

    public Task<string> PublishInvoiceCodPaymentReceivedAsync(BsonDocument billDoc, CancellationToken ct = default)
    {
        var payload = new BsonDocument
        {
            { "billNo", billDoc.GetValue("billNo", "").AsString },
            { "storeId", billDoc.GetValue("storeId", "").AsString },
            { "salesChannel", billDoc.GetValue("salesChannel", "").AsString },
            { "onlineCod", billDoc.GetValue("onlineCod", new BsonDocument()).DeepClone() },
            { "payments", billDoc.GetValue("payments", new BsonArray()).DeepClone() },
            { "paymentMode", billDoc.GetValue("paymentMode", "").AsString },
            { "payable", billDoc.GetValue("payable", 0) },
        };
        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync("InvoiceCodPaymentReceived", payload, hash, ct);
    }
}
