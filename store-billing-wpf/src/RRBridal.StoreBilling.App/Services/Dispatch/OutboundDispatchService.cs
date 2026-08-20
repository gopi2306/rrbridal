using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Api;
using RRBridal.StoreBilling.App.Services.Audit;
using RRBridal.StoreBilling.App.Services.Auth;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Expenses;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.Services.Dispatch;

public static class OutboundDispatchStatus
{
    public const string Draft = "Draft";
    public const string Ready = "Ready";
    public const string HandedOver = "HandedOver";
    public const string Delivered = "Delivered";
    public const string Cancelled = "Cancelled";
    public const string Returned = "Returned";

    public static bool IsActive(string? status) =>
        status is Draft or Ready or HandedOver;

    public static bool CanTransition(string? from, string? to) =>
        (from, to) switch
        {
            (Draft, Ready or Cancelled) => true,
            (Ready, HandedOver or Cancelled) => true,
            (HandedOver, Delivered or Returned) => true,
            (Delivered, Returned) => true,
            _ => false,
        };
}

public static class DispatchCarrierType
{
    public const string Courier = "courier";
    public const string Post = "post";
}

public static class DispatchFeePayer
{
    public const string Customer = "customer";
    public const string Store = "store";
}

public sealed record DispatchAddress(
    string Name,
    string Phone,
    string AddressLine1,
    string AddressLine2,
    string City,
    string State,
    string Pincode,
    string Landmark = "",
    string Source = "bill");

public sealed class OutboundDispatchDraft
{
    public required DispatchAddress ShipTo { get; init; }
    public string CarrierType { get; init; } = DispatchCarrierType.Courier;
    public string CarrierName { get; init; } = "";
    public string TrackingNo { get; init; } = "";
    public int PackageCount { get; init; } = 1;
    public string FeePayer { get; init; } = DispatchFeePayer.Store;
    public decimal Fee { get; init; }
    public string FeePaymentMode { get; init; } = ExpensePaymentMode.Cash;
    public string FeePaymentReference { get; init; } = "";
    public string BusinessDate { get; init; } = "";
}

public sealed class OutboundDispatchQuery
{
    public string? BusinessDate { get; init; }
    public string? Status { get; init; }
    public string? BatchNo { get; init; }
    public string? Search { get; init; }
    public int Limit { get; init; } = 100;
}

public static class DispatchAddressResolver
{
    public static DispatchAddress Resolve(
        BsonDocument bill,
        BsonDocument? customerMaster = null,
        DispatchAddress? manualOverride = null)
    {
        if (manualOverride != null)
            return manualOverride with { Source = "manual" };

        var source = customerMaster != null && HasAddress(customerMaster) ? customerMaster : bill;
        var sourceName = ReferenceEquals(source, customerMaster) ? "customer_master" : "bill";
        var nested = ReadDocument(source, "shipTo") ?? ReadDocument(source, "address");

        return new DispatchAddress(
            Read(source, "name", "customerName") ?? Read(bill, "customerName") ?? "",
            Read(source, "phone", "mobile", "customerPhone") ?? Read(bill, "customerPhone") ?? "",
            Read(nested, "addressLine1", "line1")
                ?? Read(source, "addressLine1", "street", "fullAddress", "customerAddress") ?? "",
            Read(nested, "addressLine2", "line2")
                ?? Read(source, "addressLine2", "place") ?? "",
            Read(nested, "city") ?? Read(source, "city") ?? "",
            Read(nested, "state") ?? Read(source, "state") ?? "",
            Read(nested, "pincode", "postalCode")
                ?? Read(source, "pincode", "postalCode") ?? "",
            Read(nested, "landmark") ?? Read(source, "landmark") ?? "",
            sourceName);
    }

    private static bool HasAddress(BsonDocument doc) =>
        ReadDocument(doc, "address") != null
        || ReadDocument(doc, "shipTo") != null
        || !string.IsNullOrWhiteSpace(Read(
            doc, "addressLine1", "street", "fullAddress", "customerAddress", "city", "pincode"));

    private static BsonDocument? ReadDocument(BsonDocument? doc, string field) =>
        doc != null && doc.TryGetValue(field, out var value) && value.IsBsonDocument
            ? value.AsBsonDocument
            : null;

    private static string? Read(BsonDocument? doc, params string[] fields)
    {
        if (doc == null) return null;
        foreach (var field in fields)
        {
            if (doc.TryGetValue(field, out var value) && !value.IsBsonNull)
            {
                var text = value.IsString ? value.AsString : value.ToString();
                if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
            }
        }
        return null;
    }
}

public static class OutboundDispatchDocumentMapper
{
    public static BsonDocument Build(
        string dispatchNo,
        string batchNo,
        string storeId,
        string deviceId,
        string posCounter,
        BsonDocument bill,
        OutboundDispatchDraft draft,
        string createdAtUtc)
    {
        ValidatePostedBill(bill);
        ValidateDraft(draft);

        var billNo = ReadString(bill, "billNo")
                     ?? ReadString(bill, "invoiceNo")
                     ?? throw new InvalidOperationException("Bill number is missing.");
        var lines = bill.TryGetValue("lines", out var lineValue) && lineValue.IsBsonArray
            ? (BsonArray)lineValue.AsBsonArray.DeepClone()
            : new BsonArray();

        return new BsonDocument
        {
            { "dispatchNo", dispatchNo.Trim() },
            { "batchNo", batchNo.Trim() },
            { "storeId", storeId.Trim() },
            { "deviceId", deviceId.Trim() },
            { "posCounter", posCounter.Trim() },
            { "billNo", billNo.Trim() },
            { "status", OutboundDispatchStatus.Draft },
            { "active", true },
            { "billSnapshot", new BsonDocument
                {
                    { "billNo", billNo.Trim() },
                    { "billDate", ReadString(bill, "billDate") ?? "" },
                    { "payable", ReadDecimal(bill, "payable") },
                    { "customer", new BsonDocument
                        {
                            { "customerCode", ReadString(bill, "customerCode") ?? "" },
                            { "name", ReadString(bill, "customerName") ?? "" },
                            { "phone", ReadString(bill, "customerPhone") ?? "" },
                        }
                    },
                    { "dispatchFeePayment", new BsonDocument
                        {
                            { "mode", draft.FeePaymentMode.Trim() },
                            { "reference", draft.FeePaymentReference.Trim() },
                        }
                    },
                }
            },
            { "lines", lines },
            { "shipTo", AddressToBson(draft.ShipTo) },
            { "carrierType", draft.CarrierType.Trim().ToLowerInvariant() },
            { "carrierName", draft.CarrierName.Trim() },
            { "trackingNo", draft.TrackingNo.Trim() },
            { "packageCount", draft.PackageCount },
            { "feePayer", draft.FeePayer.Trim().ToLowerInvariant() },
            { "dispatchFee", (double)Round(draft.Fee) },
            { "feePaymentMode", draft.FeePaymentMode.Trim() },
            { "feePaymentReference", draft.FeePaymentReference.Trim() },
            { "businessDate", ResolveBusinessDate(draft.BusinessDate, bill, createdAtUtc) },
            { "revision", 1 },
            { "createdAtUtc", createdAtUtc },
            { "updatedAtUtc", createdAtUtc },
        };
    }

    public static BsonDocument AddressToBson(DispatchAddress address) => new()
    {
        { "name", address.Name.Trim() },
        { "phone", address.Phone.Trim() },
        { "addressLine1", address.AddressLine1.Trim() },
        { "addressLine2", address.AddressLine2.Trim() },
        { "city", address.City.Trim() },
        { "state", address.State.Trim() },
        { "pincode", address.Pincode.Trim() },
        { "landmark", address.Landmark.Trim() },
        { "source", address.Source.Trim() },
    };

    /// <summary>
    /// Produces the flat payload accepted by central-backend. It also normalizes dispatches
    /// written by the first local implementation so pending outbox events remain syncable.
    /// </summary>
    public static BsonDocument ToCanonicalPayload(BsonDocument source)
    {
        var payload = (BsonDocument)source.DeepClone();
        if (payload.TryGetValue("carrier", out var carrierValue) && carrierValue.IsBsonDocument)
        {
            var carrier = carrierValue.AsBsonDocument;
            payload["carrierType"] = ReadString(carrier, "type") ?? "";
            payload["carrierName"] = ReadString(carrier, "name") ?? "";
            payload["trackingNo"] = ReadString(carrier, "trackingNo") ?? "";
            payload.Remove("carrier");
        }
        if (payload.TryGetValue("fee", out var feeValue) && feeValue.IsBsonDocument)
        {
            var fee = feeValue.AsBsonDocument;
            payload["feePayer"] = ReadString(fee, "payer") ?? DispatchFeePayer.Store;
            payload["dispatchFee"] = (double)ReadDecimal(fee, "amount");
            payload["feePaymentMode"] = ReadString(fee, "paymentMode") ?? "";
            payload["feePaymentReference"] = ReadString(fee, "paymentReference") ?? "";
            payload.Remove("fee");
        }
        if (!payload.Contains("lines")
            && payload.TryGetValue("billSnapshot", out var snapshotValue)
            && snapshotValue.IsBsonDocument
            && snapshotValue.AsBsonDocument.TryGetValue("lines", out var lines)
            && lines.IsBsonArray)
        {
            payload["lines"] = lines.DeepClone();
            snapshotValue.AsBsonDocument.Remove("lines");
        }
        if (payload.TryGetValue("billSnapshot", out var billSnapshot)
            && billSnapshot.IsBsonDocument
            && billSnapshot.AsBsonDocument.TryGetValue("dispatchFeePayment", out var payment)
            && payment.IsBsonDocument)
        {
            if (!payload.Contains("feePaymentMode"))
                payload["feePaymentMode"] = ReadString(payment.AsBsonDocument, "mode") ?? "";
            if (!payload.Contains("feePaymentReference"))
                payload["feePaymentReference"] = ReadString(payment.AsBsonDocument, "reference") ?? "";
        }
        if (payload.TryGetValue("linkedExpense", out var linkedExpense))
        {
            payload["expense"] = linkedExpense.DeepClone();
            if (linkedExpense.IsBsonDocument
                && linkedExpense.AsBsonDocument.TryGetValue("expenseNo", out var expenseNo))
                payload["expenseNo"] = expenseNo;
            payload.Remove("linkedExpense");
        }
        if (payload.TryGetValue("chargeReceipt", out var chargeReceipt)
            && chargeReceipt.IsBsonDocument
            && chargeReceipt.AsBsonDocument.TryGetValue("receiptNo", out var receiptNo))
            payload["chargeReceiptNo"] = receiptNo;
        if (payload.TryGetValue("auditHistory", out var auditHistory) && !payload.Contains("audit"))
        {
            payload["audit"] = auditHistory.DeepClone();
            payload.Remove("auditHistory");
        }
        return payload;
    }

    public static void ValidatePostedBill(BsonDocument bill)
    {
        if (!string.Equals(ReadString(bill, "status") ?? "posted", "posted", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only a posted bill can become a dispatch.");
    }

    public static void ValidateDraft(OutboundDispatchDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.ShipTo.Name)) throw new InvalidOperationException("Ship-to name is required.");
        if (string.IsNullOrWhiteSpace(draft.ShipTo.AddressLine1)) throw new InvalidOperationException("Ship-to address is required.");
        if (draft.PackageCount < 1) throw new InvalidOperationException("Package count must be at least one.");
        if (draft.Fee < 0) throw new InvalidOperationException("Dispatch fee cannot be negative.");
        if (draft.CarrierType is not DispatchCarrierType.Courier and not DispatchCarrierType.Post)
            throw new InvalidOperationException("Carrier type must be courier or post.");
        if (draft.FeePayer is not DispatchFeePayer.Customer and not DispatchFeePayer.Store)
            throw new InvalidOperationException("Fee payer must be customer or store.");
    }

    public static string? ReadString(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) && !value.IsBsonNull
            ? value.IsString ? value.AsString : value.ToString()
            : null;

    public static decimal ReadDecimal(BsonDocument doc, string field)
    {
        if (!doc.TryGetValue(field, out var value) || value.IsBsonNull) return 0m;
        return value.BsonType switch
        {
            BsonType.Double => (decimal)value.AsDouble,
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            BsonType.Decimal128 => (decimal)value.AsDecimal128,
            _ => decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
                ? result
                : 0m,
        };
    }

    private static string ResolveBusinessDate(string requested, BsonDocument bill, string createdAtUtc)
    {
        if (DateOnly.TryParse(requested, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var billDate = ReadString(bill, "businessDate") ?? ReadString(bill, "billDate");
        if (DateOnly.TryParse(billDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return DateTime.Parse(createdAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}

public sealed class OutboundDispatchService
{
    private readonly IMongoCollection<BsonDocument> _dispatches;
    private readonly IMongoCollection<BsonDocument> _receipts;
    private readonly IMongoCollection<BsonDocument> _bills;
    private readonly StoreContext _store;
    private readonly BillNumberGenerator _numbers;
    private readonly BillingOutboxPublisher _outbox;
    private readonly DailyExpenseService _expenses;
    private readonly StoreAuditLogService _audit;
    private CentralOnlineModeService? _centralMode;
    private CentralStorePosClient? _storePos;

    public OutboundDispatchService(
        IMongoDatabase localDb,
        StoreContext store,
        BillNumberGenerator numbers,
        BillingOutboxPublisher outbox,
        DailyExpenseService expenses,
        StoreAuditLogService audit)
    {
        _dispatches = localDb.GetCollection<BsonDocument>("store_outbound_dispatches");
        _receipts = localDb.GetCollection<BsonDocument>("store_payment_receipts");
        _bills = localDb.GetCollection<BsonDocument>("store_bills");
        _store = store;
        _numbers = numbers;
        _outbox = outbox;
        _expenses = expenses;
        _audit = audit;
    }

    public void ConfigureOnline(CentralOnlineModeService centralMode, CentralStorePosClient storePos)
    {
        _centralMode = centralMode;
        _storePos = storePos;
    }

    private bool IsCentralOnline => _centralMode?.IsOnlineMode == true && _storePos != null;

    public async Task<BsonDocument> CreateAsync(
        BsonDocument postedBill,
        OutboundDispatchDraft draft,
        UserSession? session,
        CancellationToken ct = default)
    {
        OutboundDispatchDocumentMapper.ValidatePostedBill(postedBill);
        OutboundDispatchDocumentMapper.ValidateDraft(draft);
        var billNo = OutboundDispatchDocumentMapper.ReadString(postedBill, "billNo")
                     ?? throw new InvalidOperationException("Bill number is missing.");
        if (await GetActiveByBillNoAsync(billNo, ct) != null)
            throw new InvalidOperationException("This bill already has an active dispatch.");

        var dispatchNo = await _numbers.NextDispatchAsync(ct);
        var now = DateTime.UtcNow.ToString("O");
        var doc = OutboundDispatchDocumentMapper.Build(
            dispatchNo, "", _store.StoreId, _store.DeviceId, _store.PosCounter, postedBill, draft, now);
        AppendAudit(doc, "created", session, now);

        if (!IsCentralOnline)
        {
            await _dispatches.InsertOneAsync(doc, cancellationToken: ct);
            await UpdateLocalBillDispatchSnapshotAsync(doc, ct);
        }

        await _outbox.PublishOutboundDispatchCreatedAsync(
            OutboundDispatchDocumentMapper.ToCanonicalPayload(doc), ct);
        await LogAsync(doc, "created", session, ct);
        return doc;
    }

    public async Task<BsonDocument?> GetActiveByBillNoAsync(string billNo, CancellationToken ct = default)
    {
        if (IsCentralOnline)
        {
            using var json = await _storePos!.GetActiveOutboundDispatchByBillAsync(billNo.Trim(), ct);
            return MapCentralDocument(json.RootElement);
        }

        return await _dispatches.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
            Builders<BsonDocument>.Filter.Eq("billNo", billNo.Trim()),
            Builders<BsonDocument>.Filter.In(
                "status",
                new[] { OutboundDispatchStatus.Draft, OutboundDispatchStatus.Ready, OutboundDispatchStatus.HandedOver })))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<BsonDocument?> GetByDispatchNoAsync(string dispatchNo, CancellationToken ct = default)
    {
        var trimmed = dispatchNo.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (IsCentralOnline)
        {
            using var json = await _storePos!.GetOutboundDispatchAsync(trimmed, ct);
            return MapCentralDocument(json.RootElement);
        }
        return await _dispatches.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
            Builders<BsonDocument>.Filter.Eq("dispatchNo", trimmed))).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<BsonDocument>> ListAsync(
        OutboundDispatchQuery query,
        CancellationToken ct = default)
    {
        var limit = Math.Clamp(query.Limit, 1, 500);
        var fetchLimit = string.IsNullOrWhiteSpace(query.Search) ? limit : 500;
        List<BsonDocument> docs;
        if (IsCentralOnline)
        {
            using var json = await _storePos!.ListOutboundDispatchesAsync(
                query.BusinessDate, query.Status, query.BatchNo, fetchLimit, ct);
            docs = MapCentralDocuments(json.RootElement);
        }
        else
        {
            var filters = new List<FilterDefinition<BsonDocument>>
            {
                Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
            };
            if (!string.IsNullOrWhiteSpace(query.BusinessDate))
                filters.Add(Builders<BsonDocument>.Filter.Eq("businessDate", query.BusinessDate.Trim()));
            if (!string.IsNullOrWhiteSpace(query.Status))
                filters.Add(Builders<BsonDocument>.Filter.Eq("status", query.Status.Trim()));
            if (!string.IsNullOrWhiteSpace(query.BatchNo))
                filters.Add(Builders<BsonDocument>.Filter.Eq("batchNo", query.BatchNo.Trim()));
            docs = await _dispatches.Find(Builders<BsonDocument>.Filter.And(filters))
                .Sort(Builders<BsonDocument>.Sort.Descending("updatedAtUtc"))
                .Limit(fetchLimit)
                .ToListAsync(ct);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            docs = docs.Where(doc => MatchesSearch(doc, search)).Take(limit).ToList();
        }
        return docs;
    }

    public async Task<BsonDocument> UpdateDraftAsync(
        BsonDocument existing,
        OutboundDispatchDraft draft,
        UserSession? session,
        CancellationToken ct = default)
    {
        if (!string.Equals(ReadStatus(existing), OutboundDispatchStatus.Draft, StringComparison.Ordinal))
            throw new InvalidOperationException("Only a draft dispatch can be edited.");
        OutboundDispatchDocumentMapper.ValidateDraft(draft);
        var hasLinkedFee = !string.IsNullOrWhiteSpace(
                               OutboundDispatchDocumentMapper.ReadString(existing, "chargeReceiptNo"))
                           || !string.IsNullOrWhiteSpace(
                               OutboundDispatchDocumentMapper.ReadString(existing, "expenseNo"));
        if (hasLinkedFee
            && (!string.Equals(
                    OutboundDispatchDocumentMapper.ReadString(existing, "feePayer"),
                    draft.FeePayer,
                    StringComparison.OrdinalIgnoreCase)
                || OutboundDispatchDocumentMapper.ReadDecimal(existing, "dispatchFee")
                != Math.Round(draft.Fee, 2, MidpointRounding.AwayFromZero)
                || !string.Equals(
                    OutboundDispatchDocumentMapper.ReadString(existing, "feePaymentMode") ?? "",
                    draft.FeePaymentMode.Trim(),
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    OutboundDispatchDocumentMapper.ReadString(existing, "feePaymentReference") ?? "",
                    draft.FeePaymentReference.Trim(),
                    StringComparison.Ordinal)))
            throw new InvalidOperationException("Fee details cannot change after the dispatch fee is posted.");
        var doc = (BsonDocument)existing.DeepClone();
        doc["shipTo"] = OutboundDispatchDocumentMapper.AddressToBson(draft.ShipTo);
        doc["carrierType"] = draft.CarrierType;
        doc["carrierName"] = draft.CarrierName.Trim();
        doc["trackingNo"] = draft.TrackingNo.Trim();
        doc["packageCount"] = draft.PackageCount;
        doc["feePayer"] = draft.FeePayer.Trim().ToLowerInvariant();
        doc["dispatchFee"] = (double)Math.Round(draft.Fee, 2, MidpointRounding.AwayFromZero);
        doc["feePaymentMode"] = draft.FeePaymentMode.Trim();
        doc["feePaymentReference"] = draft.FeePaymentReference.Trim();
        var snapshot = doc.GetValue("billSnapshot", new BsonDocument()).AsBsonDocument;
        snapshot["dispatchFeePayment"] = new BsonDocument
        {
            { "mode", draft.FeePaymentMode.Trim() },
            { "reference", draft.FeePaymentReference.Trim() },
        };
        doc["billSnapshot"] = snapshot;
        doc["updatedAtUtc"] = DateTime.UtcNow.ToString("O");
        doc["revision"] = ReadRevision(existing) + 1;
        AppendAudit(doc, "updated", session, doc["updatedAtUtc"].AsString);
        await PersistUpdateAsync(doc, "updated", session, ct);
        return doc;
    }

    public async Task<IReadOnlyList<BsonDocument>> AssignCommonBatchAsync(
        IEnumerable<BsonDocument> source,
        UserSession? session,
        CancellationToken ct = default)
    {
        var dispatches = source
            .GroupBy(d => OutboundDispatchDocumentMapper.ReadString(d, "dispatchNo") ?? "",
                StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Where(d => !string.IsNullOrWhiteSpace(
                OutboundDispatchDocumentMapper.ReadString(d, "dispatchNo")))
            .ToList();
        if (dispatches.Count < 2)
            throw new InvalidOperationException("Select at least two dispatches to create a batch.");
        if (dispatches.Any(d => !OutboundDispatchStatus.IsActive(ReadStatus(d))))
            throw new InvalidOperationException("Only active dispatches can be assigned to a new batch.");

        var batchNo = await _numbers.NextDispatchBatchAsync(ct);
        var updated = new List<BsonDocument>(dispatches.Count);
        foreach (var existing in dispatches)
        {
            var doc = (BsonDocument)existing.DeepClone();
            var now = DateTime.UtcNow.ToString("O");
            doc["batchNo"] = batchNo;
            doc["updatedAtUtc"] = now;
            doc["revision"] = ReadRevision(doc) + 1;
            AppendAudit(doc, "batch_assigned", session, now);
            await PersistUpdateAsync(doc, "batch_assigned", session, ct);
            updated.Add(doc);
        }
        return updated;
    }

    public async Task<BsonDocument> TransitionAsync(
        BsonDocument existing,
        string nextStatus,
        UserSession? session,
        string reason = "",
        CancellationToken ct = default)
    {
        var current = ReadStatus(existing);
        if (!OutboundDispatchStatus.CanTransition(current, nextStatus))
            throw new InvalidOperationException($"Dispatch cannot transition from {current} to {nextStatus}.");
        if (nextStatus is OutboundDispatchStatus.Cancelled or OutboundDispatchStatus.Returned
            && string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException(
                nextStatus == OutboundDispatchStatus.Cancelled
                    ? "Cancellation reason is required."
                    : "Return reason is required.");

        var doc = (BsonDocument)existing.DeepClone();
        var now = DateTime.UtcNow.ToString("O");
        if (current == OutboundDispatchStatus.Draft && nextStatus == OutboundDispatchStatus.Ready)
        {
            var feeEventChangedRevision = await CreateFeeRecordAsync(doc, session, ct);
            if (feeEventChangedRevision)
                doc["updatedAtUtc"] = now;
        }
        doc["status"] = nextStatus;
        doc["active"] = OutboundDispatchStatus.IsActive(nextStatus);
        doc["updatedAtUtc"] = now;
        doc["revision"] = ReadRevision(doc) + 1;
        if (!string.IsNullOrWhiteSpace(reason)) doc["statusReason"] = reason.Trim();
        AppendAudit(doc, $"status_{nextStatus.ToLowerInvariant()}", session, now);

        if (nextStatus == OutboundDispatchStatus.Cancelled && !IsCentralOnline)
            await ReverseLinkedRecordsAsync(doc, session, reason, ct);
        if (nextStatus == OutboundDispatchStatus.Cancelled)
            doc["cancellationReversal"] = BuildCancellationReversal(doc, IsCentralOnline);

        if (!IsCentralOnline)
        {
            await ReplaceLocalAsync(doc, ct);
            await UpdateLocalBillDispatchSnapshotAsync(doc, ct);
        }
        var canonical = OutboundDispatchDocumentMapper.ToCanonicalPayload(doc);
        if (nextStatus == OutboundDispatchStatus.Cancelled)
        {
            canonical["reason"] = reason.Trim();
            await _outbox.PublishOutboundDispatchCancelledAsync(canonical, ct);
        }
        else
        {
            canonical["fromStatus"] = current;
            canonical["newStatus"] = nextStatus;
            if (!string.IsNullOrWhiteSpace(reason)) canonical["reason"] = reason.Trim();
            await _outbox.PublishOutboundDispatchStatusChangedAsync(canonical, ct);
        }
        await LogAsync(doc, "status_changed", session, ct);
        return doc;
    }

    private async Task<bool> CreateFeeRecordAsync(BsonDocument dispatch, UserSession? session, CancellationToken ct)
    {
        var amount = OutboundDispatchDocumentMapper.ReadDecimal(dispatch, "dispatchFee");
        if (amount <= 0) return false;
        var payer = OutboundDispatchDocumentMapper.ReadString(dispatch, "feePayer") ?? "";
        if (payer == DispatchFeePayer.Customer
            && !string.IsNullOrWhiteSpace(OutboundDispatchDocumentMapper.ReadString(dispatch, "chargeReceiptNo")))
            return false;
        if (payer == DispatchFeePayer.Store
            && !string.IsNullOrWhiteSpace(OutboundDispatchDocumentMapper.ReadString(dispatch, "expenseNo")))
            return false;
        dispatch["revision"] = ReadRevision(dispatch) + 1;
        if (payer == DispatchFeePayer.Customer)
            await CreateCustomerChargeAsync(dispatch, amount, session, ct);
        else
            await CreateStoreExpenseAsync(dispatch, amount, session, ct);
        return true;
    }

    private async Task CreateCustomerChargeAsync(
        BsonDocument dispatch,
        decimal amount,
        UserSession? session,
        CancellationToken ct)
    {
        var receiptNo = await _numbers.NextPaymentReceiptAsync(ct);
        var customer = dispatch["billSnapshot"]["customer"].AsBsonDocument;
        var receipt = new BsonDocument
        {
            { "receiptNo", receiptNo },
            { "kind", "dispatch_charge" },
            { "storeId", _store.StoreId },
            { "billNo", dispatch["billNo"].AsString },
            { "dispatchNo", dispatch["dispatchNo"].AsString },
            { "customerName", OutboundDispatchDocumentMapper.ReadString(customer, "name") ?? "" },
            { "customerPhone", OutboundDispatchDocumentMapper.ReadString(customer, "phone") ?? "" },
            { "amount", (double)amount },
            { "mode", OutboundDispatchDocumentMapper.ReadString(dispatch, "feePaymentMode") ?? "" },
            { "reference", OutboundDispatchDocumentMapper.ReadString(dispatch, "feePaymentReference") ?? "" },
            { "status", "posted" },
            { "createdAtUtc", DateTime.UtcNow.ToString("O") },
        };
        var (name, email) = StoreAuditLogService.ActorFromSession(session);
        receipt["receivedBy"] = name ?? email ?? "";
        if (!IsCentralOnline)
            await _receipts.InsertOneAsync(receipt, cancellationToken: ct);
        dispatch["chargeReceipt"] = receipt;
        dispatch["chargeReceiptNo"] = receiptNo;
        await _outbox.PublishOutboundDispatchChargeReceivedAsync(
            dispatch["dispatchNo"].AsString, receipt, ct);
    }

    private async Task CreateStoreExpenseAsync(
        BsonDocument dispatch,
        decimal amount,
        UserSession? session,
        CancellationToken ct)
    {
        var expense = await _expenses.CreateAsync(new DailyExpenseDraft
        {
            BusinessDate = dispatch["businessDate"].AsString,
            SupplierName = OutboundDispatchDocumentMapper.ReadString(dispatch, "carrierName") ?? "",
            Category = "Transport/Courier",
            Description = $"Dispatch {dispatch["dispatchNo"].AsString} for bill {dispatch["billNo"].AsString}",
            EnteredAmount = amount,
            Payments =
            [
                new DailyExpensePaymentLeg(
                    OutboundDispatchDocumentMapper.ReadString(dispatch, "feePaymentMode") ?? ExpensePaymentMode.Cash,
                    amount,
                    OutboundDispatchDocumentMapper.ReadString(dispatch, "feePaymentReference") ?? "")
            ],
            LinkedDispatchNo = dispatch["dispatchNo"].AsString,
            LinkedBillNo = dispatch["billNo"].AsString,
        }, session, persistLocally: !IsCentralOnline, ct);
        dispatch["expense"] = expense;
        dispatch["expenseNo"] = expense["expenseNo"];
        await _outbox.PublishOutboundDispatchUpdatedAsync(
            OutboundDispatchDocumentMapper.ToCanonicalPayload(dispatch), ct);
    }

    private async Task ReverseLinkedRecordsAsync(BsonDocument dispatch, UserSession? session, string reason, CancellationToken ct)
    {
        if (dispatch.TryGetValue("chargeReceipt", out var receiptValue) && receiptValue.IsBsonDocument)
        {
            var receiptNo = OutboundDispatchDocumentMapper.ReadString(receiptValue.AsBsonDocument, "receiptNo");
            if (!string.IsNullOrWhiteSpace(receiptNo))
            {
                await _receipts.UpdateOneAsync(
                    Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
                        Builders<BsonDocument>.Filter.Eq("receiptNo", receiptNo),
                        Builders<BsonDocument>.Filter.Eq("status", "posted")),
                    Builders<BsonDocument>.Update
                        .Set("status", "void")
                        .Set("voidReason", reason.Trim())
                        .Set("voidedAtUtc", DateTime.UtcNow.ToString("O")),
                    cancellationToken: ct);
                receiptValue.AsBsonDocument["status"] = "void";
            }
        }

        if (dispatch.TryGetValue("expense", out var expenseValue) && expenseValue.IsBsonDocument)
        {
            var expenseNo = OutboundDispatchDocumentMapper.ReadString(expenseValue.AsBsonDocument, "expenseNo");
            if (!string.IsNullOrWhiteSpace(expenseNo))
            {
                var expense = await _expensesCollection.Find(Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
                    Builders<BsonDocument>.Filter.Eq("expenseNo", expenseNo))).FirstOrDefaultAsync(ct);
                if (expense != null && !string.Equals(
                        OutboundDispatchDocumentMapper.ReadString(expense, "status"), "void", StringComparison.OrdinalIgnoreCase))
                {
                    var voided = await _expenses.VoidAsync(expense, reason, session, persistLocally: true, ct);
                    expenseValue.AsBsonDocument["status"] = voided["status"];
                }
            }
        }
    }

    private IMongoCollection<BsonDocument> _expensesCollection =>
        _dispatches.Database.GetCollection<BsonDocument>("store_daily_expenses");

    private async Task PersistUpdateAsync(
        BsonDocument doc,
        string eventAction,
        UserSession? session,
        CancellationToken ct)
    {
        if (!IsCentralOnline)
        {
            await ReplaceLocalAsync(doc, ct);
            await UpdateLocalBillDispatchSnapshotAsync(doc, ct);
        }
        await _outbox.PublishOutboundDispatchUpdatedAsync(
            OutboundDispatchDocumentMapper.ToCanonicalPayload(doc), ct);
        await LogAsync(doc, eventAction, session, ct);
    }

    private Task ReplaceLocalAsync(BsonDocument doc, CancellationToken ct) =>
        _dispatches.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
                Builders<BsonDocument>.Filter.Eq("dispatchNo", doc["dispatchNo"].AsString)),
            doc,
            new ReplaceOptions { IsUpsert = false },
            ct);

    private Task UpdateLocalBillDispatchSnapshotAsync(BsonDocument dispatch, CancellationToken ct)
    {
        var billNo = OutboundDispatchDocumentMapper.ReadString(dispatch, "billNo") ?? "";
        if (string.IsNullOrWhiteSpace(billNo))
            return Task.CompletedTask;

        var snapshot = new BsonDocument
        {
            { "dispatchNo", OutboundDispatchDocumentMapper.ReadString(dispatch, "dispatchNo") ?? "" },
            { "status", OutboundDispatchDocumentMapper.ReadString(dispatch, "status") ?? "" },
            { "carrierType", OutboundDispatchDocumentMapper.ReadString(dispatch, "carrierType") ?? "" },
            { "feePayer", OutboundDispatchDocumentMapper.ReadString(dispatch, "feePayer") ?? "" },
            { "updatedAtUtc", OutboundDispatchDocumentMapper.ReadString(dispatch, "updatedAtUtc") ?? "" },
        };
        return _bills.UpdateOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("storeId", _store.StoreId),
                Builders<BsonDocument>.Filter.Eq("billNo", billNo)),
            Builders<BsonDocument>.Update.Set("dispatch", snapshot),
            cancellationToken: ct);
    }

    private async Task LogAsync(BsonDocument doc, string action, UserSession? session, CancellationToken ct)
    {
        var (name, email) = StoreAuditLogService.ActorFromSession(session);
        await _audit.LogEventAsync(new StoreAuditEvent
        {
            EntityType = "outbound_dispatch",
            EntityId = doc["dispatchNo"].AsString,
            Action = action,
            ActorName = name,
            ActorEmail = email,
            Metadata = new BsonDocument
            {
                { "billNo", doc["billNo"] },
                { "status", doc["status"] },
                { "revision", doc["revision"] },
            },
        }, ct);
    }

    private static void AppendAudit(BsonDocument doc, string action, UserSession? session, string atUtc)
    {
        var (name, email) = StoreAuditLogService.ActorFromSession(session);
        var history = doc.TryGetValue("audit", out var value) && value.IsBsonArray
            ? value.AsBsonArray
            : new BsonArray();
        history.Add(new BsonDocument
        {
            { "action", action },
            { "atUtc", atUtc },
            { "actorName", name ?? "" },
            { "actorEmail", email ?? "" },
        });
        doc["audit"] = history;
    }

    private static string ReadStatus(BsonDocument doc) =>
        OutboundDispatchDocumentMapper.ReadString(doc, "status") ?? "";

    private static int ReadRevision(BsonDocument doc) =>
        Math.Max(1, (int)OutboundDispatchDocumentMapper.ReadDecimal(doc, "revision"));

    private static BsonDocument? MapCentralDocument(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        BsonDocument? doc = null;
        if (element.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
            doc = BsonDocument.Parse(payload.GetRawText());
        else if (element.ValueKind == JsonValueKind.Object)
            doc = BsonDocument.Parse(element.GetRawText());
        if (doc == null) return null;
        if (!doc.Contains("storeId") && doc.TryGetValue("storeCode", out var storeCode))
            doc["storeId"] = storeCode;
        doc["active"] = OutboundDispatchStatus.IsActive(ReadStatus(doc));
        return OutboundDispatchDocumentMapper.ToCanonicalPayload(doc);
    }

    private static List<BsonDocument> MapCentralDocuments(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array) return [];
        return element.EnumerateArray()
            .Select(MapCentralDocument)
            .Where(doc => doc != null)
            .Cast<BsonDocument>()
            .ToList();
    }

    private static bool MatchesSearch(BsonDocument doc, string search)
    {
        var customer = doc.TryGetValue("billSnapshot", out var snapshot)
                       && snapshot.IsBsonDocument
                       && snapshot.AsBsonDocument.TryGetValue("customer", out var customerValue)
                       && customerValue.IsBsonDocument
            ? customerValue.AsBsonDocument
            : new BsonDocument();
        return new[]
        {
            OutboundDispatchDocumentMapper.ReadString(doc, "dispatchNo"),
            OutboundDispatchDocumentMapper.ReadString(doc, "billNo"),
            OutboundDispatchDocumentMapper.ReadString(doc, "batchNo"),
            OutboundDispatchDocumentMapper.ReadString(doc, "carrierName"),
            OutboundDispatchDocumentMapper.ReadString(doc, "trackingNo"),
            OutboundDispatchDocumentMapper.ReadString(customer, "name"),
            OutboundDispatchDocumentMapper.ReadString(customer, "phone"),
        }.Any(value => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static BsonDocument BuildCancellationReversal(BsonDocument dispatch, bool centralOnly)
    {
        var reversal = new BsonDocument
        {
            { "mode", centralOnly ? "requested_centrally" : "completed_locally" },
        };
        if (dispatch.TryGetValue("chargeReceipt", out var receipt) && receipt.IsBsonDocument)
        {
            reversal["chargeReceiptNo"] =
                OutboundDispatchDocumentMapper.ReadString(receipt.AsBsonDocument, "receiptNo") ?? "";
            reversal["chargeReceiptStatus"] = centralOnly
                ? "void_requested"
                : OutboundDispatchDocumentMapper.ReadString(receipt.AsBsonDocument, "status") ?? "void";
        }
        if (dispatch.TryGetValue("expense", out var expense) && expense.IsBsonDocument)
        {
            reversal["expenseNo"] =
                OutboundDispatchDocumentMapper.ReadString(expense.AsBsonDocument, "expenseNo") ?? "";
            reversal["expenseStatus"] = centralOnly
                ? "void_requested"
                : OutboundDispatchDocumentMapper.ReadString(expense.AsBsonDocument, "status") ?? "void";
        }
        return reversal;
    }
}
