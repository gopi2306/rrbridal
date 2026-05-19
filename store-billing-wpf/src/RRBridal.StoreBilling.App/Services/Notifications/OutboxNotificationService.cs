using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services.Notifications;

public sealed class OutboxNotificationService
{
    private readonly IMongoCollection<BsonDocument> _outbox;
    private readonly IMongoCollection<BsonDocument> _syncState;
    private readonly StoreContext _storeContext;

    public OutboxNotificationService(IMongoDatabase localDb, StoreContext storeContext)
    {
        _outbox = localDb.GetCollection<BsonDocument>("outbox_events");
        _syncState = localDb.GetCollection<BsonDocument>("sync_state");
        _storeContext = storeContext;
    }

    public async Task<OutboxNotificationSnapshot> LoadAsync(CancellationToken ct = default)
    {
        var storeId = _storeContext.StoreId;
        var deviceId = _storeContext.DeviceId;

        var pendingFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("status", "pending"),
            Builders<BsonDocument>.Filter.Eq("storeId", storeId));

        var storeWideCount = (int)await _outbox.CountDocumentsAsync(pendingFilter, cancellationToken: ct);

        var thisCounterFilter = Builders<BsonDocument>.Filter.And(
            pendingFilter,
            Builders<BsonDocument>.Filter.Eq("deviceId", deviceId));

        var thisCounterCount = (int)await _outbox.CountDocumentsAsync(thisCounterFilter, cancellationToken: ct);

        var docs = await _outbox.Find(thisCounterFilter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("createdAt"))
            .Limit(100)
            .ToListAsync(ct);

        var items = docs.Select(MapItem).ToList();

        string? lastError = null;
        string? lastAutoSyncAt = null;
        var state = await _syncState.Find(FilterDefinition<BsonDocument>.Empty).FirstOrDefaultAsync(ct);
        if (state != null)
        {
            if (state.TryGetValue("lastError", out var err) && err.IsString && !string.IsNullOrWhiteSpace(err.AsString))
                lastError = err.AsString;
            if (state.TryGetValue("lastAutoSyncAt", out var auto) && auto.IsString)
                lastAutoSyncAt = auto.AsString;
        }

        return new OutboxNotificationSnapshot
        {
            ThisCounterPendingCount = thisCounterCount,
            StoreWidePendingCount = storeWideCount,
            LastSyncError = lastError,
            LastAutoSyncAt = lastAutoSyncAt,
            Items = items,
        };
    }

    public async Task<int> CountThisCounterPendingAsync(CancellationToken ct = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("status", "pending"),
            Builders<BsonDocument>.Filter.Eq("storeId", _storeContext.StoreId),
            Builders<BsonDocument>.Filter.Eq("deviceId", _storeContext.DeviceId));

        return (int)await _outbox.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    private NotificationItem MapItem(BsonDocument doc)
    {
        var type = ReadString(doc, "type") ?? "Event";
        var created = ReadString(doc, "createdAt");
        var createdDisplay = FormatCreatedAt(created);
        var detail = BuildDetail(doc);
        var eventId = ReadString(doc, "eventId") ?? "";
        var dev = ReadString(doc, "deviceId") ?? "";

        return new NotificationItem
        {
            EventId = eventId,
            Type = type,
            CreatedAtDisplay = createdDisplay,
            Detail = detail,
            DeviceId = dev,
        };
    }

    private static string BuildDetail(BsonDocument doc)
    {
        if (!doc.TryGetValue("payload", out var payload) || payload.IsBsonNull)
            return ReadString(doc, "eventId") ?? "";

        if (payload.IsBsonDocument)
        {
            var p = payload.AsBsonDocument;
            foreach (var key in new[] { "billNo", "invoiceNo", "BillNo", "InvoiceNo", "sku", "reference" })
            {
                if (p.TryGetValue(key, out var v) && v.IsString && !string.IsNullOrWhiteSpace(v.AsString))
                    return $"{key}: {v.AsString}";
            }
        }

        var text = payload.ToString() ?? "";
        return text.Length > 80 ? text[..80] + "…" : text;
    }

    private static string FormatCreatedAt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "—";

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture);

        return raw;
    }

    private static string? ReadString(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull)
            return null;
        return v.IsString ? v.AsString : v.ToString();
    }
}
