using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services;

namespace RRBridal.StoreBilling.App.Services.PurchaseIntents;

public readonly record struct PurchaseIntentLineInput(string Sku, double RequestedQty, string? Note = null);

public sealed class PurchaseIntentPublisher
{
    private readonly IMongoCollection<BsonDocument> _localIntents;
    private readonly IMongoCollection<BsonDocument> _outbox;
    private readonly StoreContext _storeContext;

    public PurchaseIntentPublisher(IMongoDatabase localDb, StoreContext storeContext)
    {
        _localIntents = localDb.GetCollection<BsonDocument>("local_purchase_intents");
        _outbox = localDb.GetCollection<BsonDocument>("outbox_events");
        _storeContext = storeContext;
    }

    /// <summary>Returns the outbox eventId (also central sourceEventId).</summary>
    public async Task<string> SubmitAsync(
        IReadOnlyList<PurchaseIntentLineInput> lines,
        string? remarks,
        CancellationToken ct)
    {
        if (lines.Count == 0) throw new ArgumentException("At least one line is required.", nameof(lines));

        foreach (var l in lines)
        {
            if (string.IsNullOrWhiteSpace(l.Sku)) throw new ArgumentException("Each line needs a non-empty SKU.", nameof(lines));
            if (l.RequestedQty <= 0 || double.IsNaN(l.RequestedQty) || double.IsInfinity(l.RequestedQty))
                throw new ArgumentException("Each line needs requestedQty > 0.", nameof(lines));
        }

        var eventId = Guid.NewGuid().ToString();
        var storeId = _storeContext.StoreId;
        var deviceId = _storeContext.DeviceId;
        var createdAt = DateTime.UtcNow.ToString("O");

        var linesBson = new BsonArray();
        foreach (var l in lines)
        {
            var lineDoc = new BsonDocument
            {
                { "sku", l.Sku.Trim() },
                { "requestedQty", l.RequestedQty },
            };
            if (!string.IsNullOrWhiteSpace(l.Note)) lineDoc["note"] = l.Note.Trim();
            linesBson.Add(lineDoc);
        }

        var payload = new BsonDocument { { "lines", linesBson } };
        if (!string.IsNullOrWhiteSpace(remarks)) payload["remarks"] = remarks.Trim();

        var hash = JsonSerializer.Serialize(
            new
            {
                lines = lines.Select(l => new { sku = l.Sku.Trim(), qty = l.RequestedQty, note = l.Note }),
                remarks,
            });

        var localDoc = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "eventId", eventId },
            { "storeId", storeId },
            { "createdAt", createdAt },
            { "payload", payload },
            { "syncStatus", "pending" },
        };

        await _localIntents.InsertOneAsync(localDoc, cancellationToken: ct);

        var outboxEvent = new BsonDocument
        {
            { "eventId", eventId },
            { "storeId", storeId },
            { "deviceId", deviceId },
            { "type", "PurchaseIntentCreated" },
            { "createdAt", createdAt },
            { "payload", payload },
            { "hash", hash },
            { "status", "pending" },
        };

        await _outbox.InsertOneAsync(outboxEvent, cancellationToken: ct);
        return eventId;
    }
}
