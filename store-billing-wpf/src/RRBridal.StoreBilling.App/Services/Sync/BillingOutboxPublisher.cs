using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services;

namespace RRBridal.StoreBilling.App.Services.Sync;

public static class OutboundDispatchEventType
{
    public const string Created = "OutboundDispatchCreated";
    public const string Updated = "OutboundDispatchUpdated";
    public const string StatusChanged = "OutboundDispatchStatusChanged";
    public const string ChargeReceived = "OutboundDispatchChargeReceived";
    public const string Cancelled = "OutboundDispatchCancelled";
}

public sealed class BillingOutboxPublisher
{
    private readonly IMongoCollection<BsonDocument> _outbox;
    private readonly StoreContext _storeContext;
    private Func<bool>? _isOnlineMode;
    private Func<string, BsonDocument, string, string, CancellationToken, Task>? _onlinePostAsync;
    private Func<CancellationToken, Task>? _pushPendingAsync;

    public BillingOutboxPublisher(IMongoDatabase localDb, StoreContext storeContext)
    {
        _outbox = localDb.GetCollection<BsonDocument>("outbox_events");
        _storeContext = storeContext;
    }

    public async Task<string> EnqueueAsync(
        string type,
        BsonDocument payload,
        string hash,
        CancellationToken ct = default,
        string? eventId = null)
    {
        var resolvedEventId = string.IsNullOrWhiteSpace(eventId)
            ? Guid.NewGuid().ToString()
            : eventId.Trim();

        // Pure Online: post to central only — never touch local outbox_events.
        if (_isOnlineMode?.Invoke() == true)
        {
            if (_onlinePostAsync == null)
                throw new InvalidOperationException("Online mode is enabled but central write dispatch is not configured.");

            await _onlinePostAsync(type, payload, hash, resolvedEventId, ct);
            return resolvedEventId;
        }

        var outboxEvent = new BsonDocument
        {
            { "eventId", resolvedEventId },
            { "storeId", _storeContext.StoreId },
            { "deviceId", _storeContext.DeviceId },
            { "type", type },
            { "createdAt", DateTime.UtcNow.ToString("O") },
            { "payload", payload },
            { "hash", hash },
            { "status", "pending" },
        };
        await _outbox.InsertOneAsync(outboxEvent, cancellationToken: ct);
        return resolvedEventId;
    }

    public void ConfigureOnlineDispatch(
        Func<bool> isOnlineMode,
        Func<CancellationToken, Task> pushPendingAsync,
        Func<string, BsonDocument, string, string, CancellationToken, Task>? onlinePostAsync = null)
    {
        _isOnlineMode = isOnlineMode;
        _pushPendingAsync = pushPendingAsync;
        _onlinePostAsync = onlinePostAsync;
    }

    public bool IsOnlineMode => _isOnlineMode?.Invoke() == true;

    public Task<string> PublishCustomEventAsync(
        string type,
        BsonDocument payload,
        string hash,
        CancellationToken ct = default) =>
        EnqueueAsync(type, payload, hash, ct);

    public Task<string> PublishInvoiceCreatedAsync(BsonDocument billDoc, CancellationToken ct = default)
    {
        var payload = (BsonDocument)billDoc.DeepClone();
        if (!payload.Contains("billNo") && payload.Contains("invoiceNo"))
            payload["billNo"] = payload["invoiceNo"];

        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync("InvoiceCreated", payload, hash, ct);
    }

    public Task<string> PublishInvoiceDeletedAsync(BsonDocument billDoc, CancellationToken ct = default)
    {
        var payload = (BsonDocument)billDoc.DeepClone();
        if (!payload.Contains("billNo") && payload.Contains("invoiceNo"))
            payload["billNo"] = payload["invoiceNo"];

        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync("InvoiceDeleted", payload, hash, ct);
    }

    /// <summary>
    /// Removes pending InvoiceCreated events for a bill so a later sync cannot recreate a deleted invoice.
    /// </summary>
    public async Task<long> CancelPendingInvoiceCreatedAsync(string billNo, CancellationToken ct = default)
    {
        if (_isOnlineMode?.Invoke() == true)
            return 0;

        var trimmed = billNo.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return 0;

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", _storeContext.StoreId),
            Builders<BsonDocument>.Filter.Eq("type", "InvoiceCreated"),
            Builders<BsonDocument>.Filter.Eq("status", "pending"),
            Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Eq("payload.billNo", trimmed),
                Builders<BsonDocument>.Filter.Eq("payload.invoiceNo", trimmed)));

        var result = await _outbox.DeleteManyAsync(filter, ct);
        return result.DeletedCount;
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

    public Task<string> PublishDailyExpenseUpdatedAsync(BsonDocument expenseDoc, CancellationToken ct = default)
    {
        var payload = (BsonDocument)expenseDoc.DeepClone();
        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync("DailyExpenseUpdated", payload, hash, ct);
    }

    public Task<string> PublishDailyExpenseVoidedAsync(BsonDocument expenseDoc, CancellationToken ct = default)
    {
        var payload = (BsonDocument)expenseDoc.DeepClone();
        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync("DailyExpenseVoided", payload, hash, ct);
    }

    public Task<string> PublishOutboundDispatchCreatedAsync(BsonDocument dispatchDoc, CancellationToken ct = default) =>
        PublishDocumentAsync(OutboundDispatchEventType.Created, dispatchDoc, ct);

    public Task<string> PublishOutboundDispatchUpdatedAsync(BsonDocument dispatchDoc, CancellationToken ct = default) =>
        PublishDocumentAsync(OutboundDispatchEventType.Updated, dispatchDoc, ct);

    public Task<string> PublishOutboundDispatchStatusChangedAsync(
        BsonDocument dispatchDoc,
        CancellationToken ct = default) =>
        PublishDocumentAsync(OutboundDispatchEventType.StatusChanged, dispatchDoc, ct);

    public Task<string> PublishOutboundDispatchCancelledAsync(
        BsonDocument dispatchDoc,
        CancellationToken ct = default) =>
        PublishDocumentAsync(OutboundDispatchEventType.Cancelled, dispatchDoc, ct);

    public Task<string> PublishOutboundDispatchChargeReceivedAsync(
        string dispatchNo,
        BsonDocument receiptDoc,
        CancellationToken ct = default)
    {
        var payload = new BsonDocument
        {
            { "dispatchNo", dispatchNo.Trim() },
            { "chargeReceiptNo", receiptDoc.GetValue("receiptNo", "").ToString() },
            { "receipt", receiptDoc.DeepClone() },
        };
        return PublishDocumentAsync(OutboundDispatchEventType.ChargeReceived, payload, ct);
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

    public Task<string> PublishInvoiceCreditPaymentReceivedAsync(
        BsonDocument billDoc,
        BsonDocument receiptDoc,
        CancellationToken ct = default)
    {
        var payload = new BsonDocument
        {
            { "billNo", billDoc.GetValue("billNo", "").AsString },
            { "storeId", billDoc.GetValue("storeId", "").AsString },
            { "customerName", billDoc.GetValue("customerName", "").AsString },
            { "customerPhone", billDoc.GetValue("customerPhone", "").AsString },
            { "creditBilling", billDoc.GetValue("creditBilling", new BsonDocument()).DeepClone() },
            { "payments", billDoc.GetValue("payments", new BsonArray()).DeepClone() },
            { "paymentMode", billDoc.GetValue("paymentMode", "").AsString },
            { "payable", billDoc.GetValue("payable", 0) },
            { "receipt", receiptDoc.DeepClone() },
        };
        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync("InvoiceCreditPaymentReceived", payload, hash, ct);
    }

    public Task<string> PublishQuotationUpsertedAsync(BsonDocument quotationDoc, CancellationToken ct = default)
    {
        var payload = (BsonDocument)quotationDoc.DeepClone();
        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync("QuotationUpserted", payload, hash, ct);
    }

    public Task<string> PublishQuotationConvertedAsync(
        string quotationNo,
        string billNo,
        BsonDocument? quotationSnapshot = null,
        CancellationToken ct = default)
    {
        var payload = quotationSnapshot != null
            ? (BsonDocument)quotationSnapshot.DeepClone()
            : new BsonDocument();
        payload["quotationNo"] = quotationNo.Trim();
        payload["convertedBillNo"] = billNo.Trim();
        payload["status"] = "converted";
        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync("QuotationConverted", payload, hash, ct);
    }

    public Task<string> PublishQuotationCancelledAsync(
        string quotationNo,
        BsonDocument? quotationSnapshot = null,
        CancellationToken ct = default)
    {
        var payload = quotationSnapshot != null
            ? (BsonDocument)quotationSnapshot.DeepClone()
            : new BsonDocument();
        payload["quotationNo"] = quotationNo.Trim();
        payload["status"] = "cancelled";
        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync("QuotationCancelled", payload, hash, ct);
    }

    public Task<string> PublishInventoryAdjustmentCreatedAsync(
        string adjustmentNo,
        string reason,
        string sku,
        decimal qtyDelta,
        string? lineNote = null,
        CancellationToken ct = default)
    {
        var lineDoc = new BsonDocument
        {
            { "sku", sku.Trim() },
            { "qtyDelta", (double)qtyDelta },
        };
        if (!string.IsNullOrWhiteSpace(lineNote))
            lineDoc["note"] = lineNote.Trim();

        var payload = new BsonDocument
        {
            { "adjustmentNo", adjustmentNo.Trim() },
            { "locationKind", "store" },
            { "reason", reason.Trim() },
            { "lines", new BsonArray { lineDoc } },
        };

        var hash = JsonSerializer.Serialize(new
        {
            adjustmentNo = adjustmentNo.Trim(),
            locationKind = "store",
            reason = reason.Trim(),
            lines = new[] { new { sku = sku.Trim(), qtyDelta, note = lineNote } },
        });

        return EnqueueAsync("InventoryAdjustmentCreated", payload, hash, ct);
    }

    public Task<string> PublishPhysicalInventoryImportAsync(
        string adjustmentNo,
        string reason,
        IReadOnlyList<(string Sku, decimal NewQty)> lines,
        string eventId,
        CancellationToken ct = default)
    {
        if (lines.Count == 0)
            throw new ArgumentException("At least one inventory line is required.", nameof(lines));
        var lineArray = new BsonArray(lines.Select(line => new BsonDocument
        {
            { "sku", line.Sku.Trim() },
            { "newQty", (double)line.NewQty },
        }));
        var payload = new BsonDocument
        {
            { "adjustmentNo", adjustmentNo.Trim() },
            { "locationKind", "store" },
            { "reason", reason.Trim() },
            { "lines", lineArray },
        };
        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync("InventoryAdjustmentCreated", payload, hash, ct, eventId);
    }

    private Task<string> PublishDocumentAsync(
        string eventType,
        BsonDocument document,
        CancellationToken ct)
    {
        var payload = (BsonDocument)document.DeepClone();
        var hash = JsonSerializer.Serialize(BsonTypeMapper.MapToDotNetValue(payload));
        return EnqueueAsync(eventType, payload, hash, ct);
    }
}
